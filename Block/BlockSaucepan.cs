﻿using System;
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

        public IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot)
        {
            if (api is not ICoreClientAPI capi) throw new Exception("Saucepan block firepit renderer attempted to run on the server for some reason");
            return new SaucepanInFirepitRenderer(capi, stack, firepit.Pos, forOutputSlot);
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

            return [ new () {
                ActionLangCode = "game:blockhelp-behavior-rightclickpickup",
                MouseButton = EnumMouseButton.Right,
                RequireFreeHand = true
            }, new () {
                ActionLangCode = "blockhelp-bucket-rightclick",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = liquidContainerStacks
            }, new () {
                ActionLangCode = "aculinaryartillery:blockhelp-open", // json lang file. 
                HotKeyCodes = ["sneak", "sprint"],
                MouseButton = EnumMouseButton.Right,
                ShouldApply = (wi, bs, es) => GetBlockEntity<BlockEntitySaucepan>(bs.Position)?.isSealed == true
            }, new () {
                ActionLangCode = "aculinaryartillery:blockhelp-close", // json lang file. 
                HotKeyCodes = ["sneak", "sprint"],
                MouseButton = EnumMouseButton.Right,
                ShouldApply = (wi, bs, es) => GetBlockEntity<BlockEntitySaucepan>(bs.Position)?.isSealed == false
            }];
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

            return stacks.Count != 0 && api.GetSimmerRecipes().Any(rec => rec.Match(stacks) >= 1);
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)]; //The inputSlots may not all be filled. This is more convenient.
            ItemStack? product = null;

            if (contents.Count == 1)    //if there is only one ingredient, we have already checked it is adequate for smelting, so we immediately create the product using CombustibleProps
            {
                product = contents[0].Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();  //we create the unit output

                product.StackSize *= contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio;   //we multiply if there is enough for more than one unit output
            }
            else if (contents.Count > 1)
            {
                if (api.GetSimmerRecipes().FirstOrDefault(rec => rec.Match(contents) > 0) is not SimmerRecipe match) return; // Make sure a recipe matches
                int amountForTheseIngredients = match.Match(contents); 
                
                match.Simmering.SmeltedStack.Resolve(world, "Saucepansimmerrecipesmeltstack");
                product = match.Simmering.SmeltedStack.ResolvedItemstack.Clone();

                product.StackSize *= amountForTheseIngredients;

                //if the recipe produces something from Expanded Foods
                if (product.Collectible is IExpandedFood prodObj)
                {
                    var input = new Dictionary<ItemSlot, CraftingRecipeIngredient>();

                    foreach (CraftingRecipeIngredient ing in match.Ingredients) //for each ingredient in the recipe
                    {
                        if (cookingSlotsProvider.Slots.All(input.ContainsKey)) break;

                        foreach (ItemSlot slot in cookingSlotsProvider.Slots)
                        {
                            if (input.ContainsKey(slot)) continue;

                            if (ing.SatisfiesAsIngredient(slot.Itemstack))
                            {
                                input[slot] = ing;
                                break;
                            }
                        }
                    }

                    prodObj.OnCreatedByKneading(input, product);
                }
            }

            if (product == null) return; //if we have no output to give

            foreach (var slot in cookingSlotsProvider.Slots) slot.Itemstack = null;

            if (GetContainableProps(product)?.Containable == true)
            {
                outputSlot.Itemstack = inputSlot.TakeOut(1);
                (outputSlot.Itemstack.Collectible as BlockLiquidContainerBase)?.TryPutLiquid(outputSlot.Itemstack, product, product.StackSize);
            }
            else outputSlot.Itemstack = product;
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float speed = 10f;
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)];

            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps is CombustibleProperties combustProps) return combustProps.MeltingDuration * contents[0].StackSize / speed;
            else if (contents.Count > 1 && api.GetSimmerRecipes().FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                return match.Simmering.MeltingDuration * match.Match(contents) / speed;
            }

            return 0;
        }

        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            List<ItemStack> contents = [.. cookingSlotsProvider.Slots.Where(slot => !slot.Empty).Select(slot => slot.Itemstack)];

            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps is CombustibleProperties combustProps) return combustProps.MeltingPoint;
            else if (contents.Count > 1 && api.GetSimmerRecipes().FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                return match.Simmering.MeltingPoint;
            }

            return 0;
        }

        public static WaterTightContainableProps? GetInContainerProps(ItemStack stack)
        {
            return stack?.ItemAttributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps?>(null, stack.Collectible.Code?.Domain ?? GlobalConstants.DefaultDomain);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntitySaucepan? sp = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySaucepan;
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

            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MultiTextureMeshRef>? meshrefs = null;
            bool isSealed = itemstack.Attributes.GetBool("isSealed");

            if (capi.ObjectCache.TryGetValue(Variant["metal"] + "MeshRefs", out object? obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else capi.ObjectCache[Variant["metal"] + "MeshRefs"] = meshrefs = [];

            if (GetContent(itemstack) is not ItemStack contentStack) return;

            int hashcode = (contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString() + (isSealed ? "sealed" : "")).GetHashCode();

            if (!meshrefs.TryGetValue(hashcode, out MultiTextureMeshRef? meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenRightMesh(capi, contentStack, null, isSealed));
            }

            if (meshRef != null) renderinfo.ModelRef = meshRef;
        }

        public string? GetOutputText(IWorldAccessor world, InventorySmelting inv)
        {
            List<ItemStack> contents = [.. inv.Skip(3).Select(slot => slot.Itemstack).Where(stack => stack != null)];
            ItemStack? product = null;
            int amount = 0;

            if (contents.Count == 1)
            {
                product = contents[0].Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                if (product == null) return null;

                amount = contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio;
            }
            else if (contents.Count > 1 && api.GetSimmerRecipes().FirstOrDefault(rec => rec.Match(contents) > 0) is SimmerRecipe match)
            {
                product = match.Simmering.SmeltedStack.ResolvedItemstack;

                if (product == null) return null;

                amount = match.Match(contents);
            }
            else return null;

            if (GetContainableProps(product) is WaterTightContainableProps props)
            {
                float litreFloat = amount * (product.StackSize / props.ItemsPerLitre);

                return Lang.Get("mealcreation-nonfood-liquid", litreFloat < 0.1 ? Lang.Get("{0} mL", (int)(litreFloat * 1000)) : Lang.Get("{0:0.##} L", litreFloat), product.GetName());
            }

            return Lang.Get("firepit-gui-willcreate", amount, product.GetName());
        }

        public MeshData? GenRightMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos? forBlockPos = null, bool isSealed = false)
        {
            Shape shape = capi.Assets.TryGet("aculinaryartillery:shapes/block/" + FirstCodePart() + "/" + (isSealed && Attributes.IsTrue("canSeal") ? "lid" : "empty") + ".json").ToObject<Shape>();
            capi.Tesselator.TesselateShape(this, shape, out MeshData mesh);

            if (contentStack != null && GetInContainerProps(contentStack) is WaterTightContainableProps props)
            {
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

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack drop = base.OnPickBlock(world, pos);

            if (GetBlockEntity<BlockEntitySaucepan>(pos)?.isSealed == true) drop.Attributes.SetBool("isSealed", true);

            return drop;
        }
    }
}