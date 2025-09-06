using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockSaucepan : BlockLiquidContainerBase, ILiquidSource, ILiquidSink, IInFirepitRendererSupplier
    {
        public override bool CanDrinkFrom => true;
        public override bool IsTopOpened => true;
        public override bool AllowHeldLiquidTransfer => true;
        public AssetLocation liquidFillSoundLocation => new("game:sounds/effect/water-fill");
        private List<SimmerRecipe> simmerRecipes => api.GetSimmerRecipes();

        public bool isSealed;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return new SaucepanInFirepitRenderer(api as ICoreClientAPI, stack, firepit.Pos, forOutputSlot);
        }

        public EnumFirepitModel GetDesiredFirepitModel(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            return EnumFirepitModel.Wide;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            ItemStack[] liquidContainerStacks = [.. api.World.Collectibles.Where(obj => obj is BlockLiquidContainerTopOpened or ILiquidSource or ILiquidSink)?
                                                                          .Select(obj => obj?.GetHandBookStacks((ICoreClientAPI)api))?
                                                                          .SelectMany(stacks => stacks ?? [])?
                                                                          .Where(stack => stack != null) ?? []
                                                ];

            return
            [
                new WorldInteraction()
                {
                    ActionLangCode = "game:blockhelp-behavior-rightclickpickup",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-bucket-rightclick",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = liquidContainerStacks
                },
                new WorldInteraction()
                {
                    ActionLangCode = "aculinaryartillery:blockhelp-open", // json lang file. 
                    HotKeyCodes = ["sneak", "sprint"],
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => (world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySaucepan)?.isSealed == true
                },
                new WorldInteraction()
                {
                    ActionLangCode = "aculinaryartillery:blockhelp-close", // json lang file. 
                    HotKeyCodes = ["sneak", "sprint"],
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => (world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySaucepan)?.isSealed == false
                }
            ];
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            //if there is something in the output, or if your saucepan already contains stuff in it, you can't cook
            if (outputStack != null || GetContent(inputStack) != null) return false;

            //the cookingSlots are not necessarily filled in order. We just want the ones that are.
            List<ItemStack> stacks = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack.Clone())];

            //if it's just one stack, no need for an actual recipe, but we need to check the CombustibleProps 
            if (stacks.Count == 1)
            {
                var combustProps = stacks[0].Collectible?.CombustibleProps;
                if ((combustProps?.SmeltedStack?.ResolvedItemstack != null) && //there is an output item defined and correctly resolved
                    combustProps.RequiresContainer &&                          //it requires a container
                    (stacks[0].StackSize % combustProps.SmeltedRatio == 0))    //there is a round number of items to smelt
                {
                    return true;
                }
            }

            return stacks.Count != 0 && simmerRecipes?.Any(rec => rec.Match(stacks) >= 1) == true;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)]; //The inputSlots may not all be filled. This is more convenient.
            ItemStack product = null;

            if (contents.Count == 1)    //if there is only one ingredient, we have already checked it is adequate for smelting, so we immediately create the product using CombustibleProps
            {
                product = contents[0].Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();  //we create the unit output

                product.StackSize *= contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio;   //we multiply if there is enough for more than one unit output
            }
            else if (contents.Count > 1)
            {
                if (simmerRecipes.FirstOrDefault(rec => rec.Match(contents) > 0) is not SimmerRecipe match) return; // Make sure a recipe matches
                int amountForTheseIngredients = match.Match(contents); 
                
                match.Simmering.SmeltedStack.Resolve(world, "Saucepansimmerrecipesmeltstack");
                product = match.Simmering.SmeltedStack.ResolvedItemstack.Clone();

                product.StackSize *= amountForTheseIngredients;

                //if the recipe produces something from Expanded Foods
                if (product.Collectible is IExpandedFood prodObj)
                {
                    var alreadyfound = new List<ItemSlot>();
                    var input = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

                    foreach (CraftingRecipeIngredient ing in match.Ingredients) //for each ingredient in the recipe
                    {
                        foreach (ItemSlot slot in cookingSlotsProvider.Slots)
                        {
                            if (!alreadyfound.Contains(slot) && !slot.Empty && ing.SatisfiesAsIngredient(slot.Itemstack))
                            {
                                alreadyfound.Add(slot);
                                input.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(slot, ing));
                                break;
                            }
                        }
                    }

                    prodObj.OnCreatedByKneading(input, product);
                }
            }

            if (product == null) return; //if we have no output to give

            foreach (var slot in cookingSlotsProvider.Slots) slot.Itemstack = null;

            if (product.Collectible.Class == "ItemLiquidPortion" || product.Collectible is ItemExpandedLiquid or ItemTransLiquid)
            {
                outputSlot.Itemstack = inputSlot.TakeOut(1);
                (outputSlot.Itemstack.Collectible as BlockLiquidContainerBase).TryPutLiquid(outputSlot.Itemstack, product, product.StackSize);
            }
            else outputSlot.Itemstack = product;
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float speed = 10f;
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)];

            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps is CombustibleProperties combustProps) return combustProps.MeltingDuration * contents[0].StackSize / speed;
            else if (contents.Count > 1 && simmerRecipes.FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                return match.Simmering.MeltingDuration * match.Match(contents) / speed;
            }

            return 0;
        }

        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)];

            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps is CombustibleProperties combustProps) return combustProps.MeltingPoint;
            else if (contents.Count > 1 && simmerRecipes.FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                return match.Simmering.MeltingPoint;
            }

            return 0;
        }

        public static WaterTightContainableProps GetInContainerProps(ItemStack stack)
        {
            return stack?.ItemAttributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>(null, stack.Collectible.Code?.Domain ?? "game");
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntitySaucepan sp = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySaucepan;
            BlockPos pos = blockSel.Position;

            if (byPlayer.WorldData.EntityControls.Sneak && byPlayer.WorldData.EntityControls.Sprint)
            {
                if (sp != null && Attributes.IsTrue("canSeal"))
                {
                    world.PlaySoundAt(AssetLocation.Create(Attributes["lidSound"].AsString("sounds/block"), Code.Domain), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);
                    sp.isSealed = !sp.isSealed;
                    sp.RedoMesh();
                    sp.MarkDirty(true);
                }

                return true;
            }

            if (sp?.isSealed == true) return false;

            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (itemslot.Itemstack?.Attributes.GetBool("isSealed") == true) return;

            if (blockSel == null || byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            if (AllowHeldLiquidTransfer)
            {
                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

                ItemStack contentStack = GetContent(itemslot.Itemstack);
                WaterTightContainableProps props = contentStack == null ? null : GetContentProps(contentStack);

                Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                    byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                    return;
                }

                if (!TryFillFromBlock(itemslot, byEntity, blockSel.Position))
                {
                    BlockLiquidContainerTopOpened targetCntBlock = targetedBlock as BlockLiquidContainerTopOpened;
                    if (targetCntBlock != null)
                    {
                        if (targetCntBlock.TryPutLiquid(blockSel.Position, contentStack, targetCntBlock.CapacityLitres) > 0)
                        {
                            TryTakeContent(itemslot.Itemstack, 1);
                            byEntity.World.PlaySoundAt(props.FillSpillSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        }

                    }
                    else if (byEntity.Controls.Sprint) SpillContents(itemslot, byEntity, blockSel);
                }
            }

            if (CanDrinkFrom)
            {
                if (GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
                {
                    tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                    return;
                }
            }

            if (AllowHeldLiquidTransfer || CanDrinkFrom) handHandling = EnumHandHandling.PreventDefaultAction; // Prevent placing on normal use
        }

        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            WaterTightContainableProps props = GetContentProps(containerSlot.Itemstack);

            if (props == null || !props.AllowSpill || props.WhenSpilled == null) return false;

            BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);
            var byPlayer = (byEntity as EntityPlayer)?.Player;

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            var action = props.WhenSpilled.Action;
            float currentlitres = GetCurrentLitres(containerSlot.Itemstack);

            if (currentlitres > 0 && currentlitres < 10)
            {
                action = WaterTightContainableProps.EnumSpilledAction.DropContents;
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                Block waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);
                IBlockAccessor blockAcc = byEntity.World.BlockAccessor;
                BlockPos pos = blockSel.Position;

                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    JsonItemStack fillLevelStack;
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                Block currentblock = blockAcc.GetBlock(pos);
                if (currentblock.Replaceable >= 6000)
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.TriggerNeighbourBlockUpdate(pos);
                    blockAcc.MarkBlockDirty(pos);
                }
                else
                {
                    if (blockAcc.GetBlock(secondPos).Replaceable >= 6000)
                    {
                        blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                        blockAcc.TriggerNeighbourBlockUpdate(secondPos);
                        blockAcc.MarkBlockDirty(secondPos);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            var contentStack = GetContent(containerSlot.Itemstack);

            if (action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");

                ItemStack stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = contentStack.StackSize;

                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }

            int moved = SplitStackAndPerformAction(byEntity, containerSlot, (stack) => { SetContent(stack, null); return contentStack.StackSize; });

            DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Pour);
            return true;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs = null;
            bool isSealed = itemstack.Attributes.GetBool("isSealed");

            if (capi.ObjectCache.TryGetValue(Variant["metal"] + "MeshRefs", out object obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else capi.ObjectCache[Variant["metal"] + "MeshRefs"] = meshrefs = [];

            if (GetContent(itemstack) is not ItemStack contentStack) return;

            int hashcode = GetSaucepanHashCode(capi.World, contentStack, isSealed);

            if (!meshrefs.TryGetValue(hashcode, out MultiTextureMeshRef meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenRightMesh(capi, contentStack, null, isSealed));
            }

            if (meshRef != null) renderinfo.ModelRef = meshRef;
        }

        public string GetOutputText(IWorldAccessor world, InventorySmelting inv)
        {
            List<ItemStack> contents = [.. new[] { inv[3].Itemstack, inv[4].Itemstack, inv[5].Itemstack, inv[6].Itemstack }.Where(stack => stack != null)];
            ItemStack product = null;
            int amount = 0;

            if (contents.Count == 1)
            {
                product = contents[0].Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                if (product == null) return null;

                amount = contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio;
            }
            else if (contents.Count > 1 && simmerRecipes.FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                product = match.Simmering.SmeltedStack.ResolvedItemstack;

                if (product == null) return null;

                amount = match.Match(contents);
            }
            else return null;

            if (product?.Collectible.Attributes?["waterTightContainerProps"].Exists == true)
            {
                float litreFloat = amount * (product.StackSize / GetContainableProps(product).ItemsPerLitre);
                string litres;

                if (litreFloat < 0.1)
                {
                    litres = Lang.Get("{0} mL", (int)(litreFloat * 1000));
                }
                else
                {
                    litres = Lang.Get("{0:0.##} L", litreFloat);
                }

                return Lang.Get("mealcreation-nonfood-liquid", litres, product.GetName());
            }

            return Lang.Get("firepit-gui-willcreate", amount, product.GetName());
        }

        public MeshData GenRightMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null, bool isSealed = false)
        {
            Shape shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/" + (isSealed && Attributes.IsTrue("canSeal") ? "lid" : "empty") + ".json").ToObject<Shape>();
            capi.Tesselator.TesselateShape(this, shape, out MeshData mesh);

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);

                if (props.Texture == null) return null;

                string fullness = Math.Round(contentStack.StackSize / (props.ItemsPerLitre * CapacityLitres), 1, MidpointRounding.ToPositiveInfinity).ToString().Replace(",", ".");

                shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/contents" + "-" + fullness + ".json").ToObject<Shape>();

                capi.Tesselator.TesselateShape("saucepan", shape, out MeshData contentMesh, new ContainerTextureSource(capi, contentStack, props.Texture), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

                if (props.ClimateColorMap != null)
                {
                    byte[] rgba = ColorUtil.ToBGRABytes(forBlockPos == null ? capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false) :
                                                                              capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false));

                    for (int i = 0; i < contentMesh.Rgba.Length; i++) contentMesh.Rgba[i] = (byte)(contentMesh.Rgba[i] * rgba[i % 4] / 255);
                }

                for (int i = 0; i < contentMesh.Flags.Length; i++) contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag

                mesh.AddMeshData(contentMesh);

                // Water flags
                if (forBlockPos != null)
                {
                    mesh.CustomInts = new CustomMeshDataPartInt(mesh.FlagsCount) { Count = mesh.FlagsCount };
                    mesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    mesh.CustomFloats = new CustomMeshDataPartFloat(mesh.FlagsCount * 2) { Count = mesh.FlagsCount * 2 };
                }

            }

            return mesh;
        }

        public int GetSaucepanHashCode(IClientWorldAccessor world, ItemStack contentStack, bool isSealed)
        {
            return (contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString() + (isSealed ? "sealed" : "")).GetHashCode();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack drop = base.OnPickBlock(world, pos);

            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySaucepan)?.isSealed == true) drop.Attributes.SetBool("isSealed", true);

            return drop;
        }
    }
}