using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockBottle : BlockLiquidContainerBase, IContainedMeshSource, IContainedCustomName
    {
        private LiquidTopOpenContainerProps props = new();
        protected virtual string MeshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected virtual AssetLocation EmptyShapeLoc => props.EmptyShapeLoc;
        protected virtual AssetLocation ContentShapeLoc => props.OpaqueContentShapeLoc;
        protected virtual AssetLocation LiquidContentShapeLoc => props.LiquidContentShapeLoc;
        public override float TransferSizeLitres => props.TransferSizeLitres;
        public override float CapacityLitres => props.CapacityLitres;
        public override bool CanDrinkFrom => true;
        public override bool IsTopOpened => true;
        public override bool AllowHeldLiquidTransfer => true;
        protected virtual float LiquidMaxYTranslate => props.LiquidMaxYTranslate;
        protected virtual float LiquidYTranslatePerLitre => LiquidMaxYTranslate / CapacityLitres;

        public AssetLocation liquidFillSoundLocation => new("game:sounds/effect/water-fill");
        public AssetLocation liquidDrinkSoundLocation => new("game:sounds/player/drink1");

        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            return GetContent(stack)?.Item?.LightHsv ?? base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
            }

            if (api is not ICoreClientAPI capi) return;;

            interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "bottle", () =>
            {
                List<ItemStack> liquidContainerStacks = [];

                foreach (var obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource or ILiquidSink or BlockWateringCan)
                    {
                        if (obj.GetHandBookStacks(capi) is not List<ItemStack> stacks) continue;

                        foreach (var stack in stacks)
                        {
                            stack.StackSize = 1;
                            liquidContainerStacks.Add(stack);
                        }
                    }
                }

                return
                [
                    new()
                    {
                        ActionLangCode = "blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [.. liquidContainerStacks]
                    }
                ];
            });
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Code.Path.Contains("clay")) return;

            Dictionary<int, MultiTextureMeshRef> meshrefs;
            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else capi.ObjectCache[MeshRefsCacheKey] = meshrefs = [];

            var contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            var hashcode = GetStackCacheHashCode(contentStack);
            if (!meshrefs.TryGetValue(hashcode, out var meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenMesh(capi, contentStack));
            }

            renderinfo.ModelRef = meshRef;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // This is a little odd - you have to sneak place but if there's something in a quadrant then you don't
            // Seems to be a vanilla thing (see bowl) - let's leave it as is for now
            if (byPlayer.Entity.Controls.Sneak) //sneak place only
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            // Apr11 2022 - added this line to prevent bottle disappearing when interacting
            // with another bottle in the bottle rack - SPANG
            if (world.BlockAccessor.GetBlock(blockSel.Position).Id == 0) return false;

            // not a fan of returning true here - if there's a problem this might be the cause
            return true;
        }

        protected int GetStackCacheHashCode(ItemStack contentStack)
        {
            return (contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString()).GetHashCode();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api is not ICoreClientAPI capi) return;

            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                var meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (var val in meshrefs) val.Value.Dispose();

                capi.ObjectCache.Remove(MeshRefsCacheKey);
            }
        }

        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = null;
            MeshData bucketmesh = null;

            var loc = EmptyShapeLoc;
            if (Code.Path.Contains("clay")) //override shape for fired clay bottle
            {
                loc = new AssetLocationAndSource("aculinaryartillery:block/bottle/bottle.json");
                var asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(this, shape, out bucketmesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }
            else if (contentStack == null) //empty bottle
            {
                var asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(this, shape, out bucketmesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }
            else
            {
                var props = GetContainableProps(contentStack); //bottle with liquid
                if (props is null)
                {
                    ACulinaryArtillery.logger.Error(string.Format("Bottle with Item {0} does not have waterTightProps and will not render or work correctly. This is usually caused by removing mods. If not, check with the items author.", contentStack.Item.Code.ToString()));
                }

                var contentSource = new BottleTextureSource(capi, contentStack, props?.Texture, this);

                var level = contentStack.StackSize / (props?.ItemsPerLitre ?? 1f);

                var basePath = "aculinaryartillery:shapes/block/bottle/glassbottle";

                //the > 0 because the oninteract logic below is a little bugged
                if (level <= 0.25f && level > 0) shape = capi.Assets.TryGet(basePath + "-1.json").ToObject<Shape>();
                else if (level <= 0.5f)shape = capi.Assets.TryGet(basePath + "-2.json").ToObject<Shape>();
                else if (level < 1) shape = capi.Assets.TryGet(basePath + "-3.json").ToObject<Shape>();
                else shape = capi.Assets.TryGet(basePath + ".json").ToObject<Shape>();

                capi.Tesselator.TesselateShape("bucket", shape, out bucketmesh, contentSource, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
                for (int i = 0; i < bucketmesh.Flags.Length; i++) bucketmesh.Flags[i] = bucketmesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                
                // Water flags
                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount) { Count = bucketmesh.FlagsCount };
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2) { Count = bucketmesh.FlagsCount * 2 };
                }
            }
            return bucketmesh;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            return GenMesh(api as ICoreClientAPI, GetContent(itemstack), forBlockPos);
        }

        public MeshData GenMeshSideways(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            if (capi.Assets.TryGet(EmptyShapeLoc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/")) is not IAsset asset) return new MeshData();

            var shape = asset.ToObject<Shape>();
            capi.Tesselator.TesselateShape(this, shape, out var bucketmesh, new(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            if (contentStack != null && (!Code.Path.Contains("clay")))
            {
                var props = GetContainableProps(contentStack);
                var contentSource = new BottleTextureSource(capi, contentStack, props?.Texture, this);

                var loc = props.IsOpaque ? ContentShapeLoc : LiquidContentShapeLoc;
                //now let's immediately override that loc.  I know, right?

                // unlike genmesh, were only rendering the contents at this point
                var level = contentStack.StackSize / (props?.ItemsPerLitre ?? 1f);
                var basePath = "aculinaryartillery:block/bottle/contents-";

                if (level <= 0.25f) loc = new AssetLocationAndSource(basePath + "side-1");
                else if (level <= 0.5f) loc = new AssetLocationAndSource(basePath + "side-2");
                else if (level < 1) loc = new AssetLocationAndSource(basePath + "side-3");
                else loc = new AssetLocationAndSource(basePath + "full");

                asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                if (asset == null) return bucketmesh;

                shape = asset.ToObject<Shape>();
                capi.Tesselator.TesselateShape(GetType().Name, shape, out var contentMesh, contentSource, new(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                for (int i = 0; i < contentMesh.Flags.Length; i++) contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                
                // Water flags
                if (forBlockPos != null)
                {
                    contentMesh.CustomInts = new CustomMeshDataPartInt(contentMesh.FlagsCount) { Count = contentMesh.FlagsCount };
                    contentMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    contentMesh.CustomFloats = new CustomMeshDataPartFloat(contentMesh.FlagsCount * 2) { Count = contentMesh.FlagsCount * 2 };

                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount) { Count = bucketmesh.FlagsCount };
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only
                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2) { Count = bucketmesh.FlagsCount * 2 };
                }
                bucketmesh.AddMeshData(contentMesh);

            }
            return bucketmesh;
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = GetContent(itemstack);
            return itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
        }

        public string GetContainedInfo(ItemSlot inSlot)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack contentStack = GetContent(inSlot.Itemstack);

            if (litres <= 0) return Lang.GetWithFallback("contained-empty-container", "{0} (Empty)", inSlot.Itemstack.GetName());

            string incontainername = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);

            return Lang.Get("contained-liquidcontainer-compact", inSlot.Itemstack.GetName(), litres, incontainername, PerishableInfoCompactContainer(api, inSlot));
        }


        public string GetContainedName(ItemSlot inSlot, int quantity)
        {
            return inSlot.Itemstack.GetName();
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (byEntity.Controls.Sneak)
            {
                // the base function is handling ground storable
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            //get info about target block
            Block targetBlock = null;
            BlockEntity targetBlockEntity = null;
            if (blockSel != null)
            {
                targetBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                targetBlockEntity = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            }

            //get bottle contents
            var content = GetContent(itemslot.Itemstack);

            if (targetBlockEntity != null)
            {
                //are we interacting with another liquid container?
                if (targetBlockEntity is BlockEntityLiquidContainer)
                {
                    // perform the default action for a liquid container
                    // note: this crashed once when trying f click with liquid into bucket
                    // hopefully this if statement prevents that!
                    if (blockSel != null && entitySel != null)
                    {
                        base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                        return;
                    }
                }
            }
            else if (blockSel != null)
            {
                var waterBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face)).LiquidCode == "water";
                if (waterBlock)
                {
                    if (content == null || content.GetName() == "water")
                    {
                        // interacting with in world water
                        var dummy = api.World.GetItem(new AssetLocation("game:waterportion"));
                        TryFillFromBlock(itemslot, byEntity, blockSel.Position.AddCopy(blockSel.Face));
                        api.World.PlaySoundAt(liquidFillSoundLocation, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, null);
                        itemslot.MarkDirty();
                        handHandling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }

            if (content != null && byEntity.Controls.Sprint)
            {
                // dump contents on the ground when sprinting
                SpillContents(itemslot, byEntity, blockSel);
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            if (CanDrinkFrom)
            {
                if (GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
                {
                    base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                    return;
                }
            }

            if (content?.Collectible.GetNutritionProperties(byEntity.World, content, byEntity) != null)
            {
                // drinking item stacks
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer player = null;
                        if (byEntity is EntityPlayer)
                        { player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID); }
                    }
                }, 500);

                //not sure that this next line really does anything
                byEntity.AnimManager?.StartAnimation("drink");
                handHandling = EnumHandHandling.PreventDefault;
            }

            if (AllowHeldLiquidTransfer || CanDrinkFrom)
            {
                // Prevent placing on normal use
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Controls.Sneak) return false; //sneak aborts

            if (GetContent(slot.Itemstack) is not ItemStack content) return false;

            var pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.X += byEntity.LocalEyePos.X;
            pos.Y += byEntity.LocalEyePos.Y - 0.4f;
            pos.Z += byEntity.LocalEyePos.Z;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                byEntity.World.SpawnCubeParticles(pos, content, 0.3f, 4, 0.5f, (byEntity as EntityPlayer)?.Player);
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                var tf = new ModelTransform();
                tf.EnsureDefaultValues();
                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y = Math.Min(0.02f, GameMath.Sin(20 * secondsUsed) / 10);
                }
                tf.Translation.X -= Math.Min(1f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.05f, secondsUsed * 2);
                tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(80f, secondsUsed * 350);
                return secondsUsed <= 1f;
            }
            return true;
        }
        
        protected override void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {

            var content = GetContent(slot.Itemstack);
            var nutriProps = GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);

            if (byEntity.World is IServerWorldAccessor && nutriProps != null && secondsUsed >= 0.95f)
            {
                var dummy = new DummySlot(content);
                var litres = GetCurrentLitres(slot.Itemstack);
                var litresToDrink = litres >= 0.25f ? 0.25f : litres;

                var state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                var spoilState = state != null ? state.TransitionLevel : 0;
                var satLossMul = (GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, byEntity));
                var healthLossMul = (GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, byEntity));

                var litresMult = 1.0f;

                if (litres == 1) litresMult = 0.25f;

                if (litres == 0.75) litresMult = 0.3333f;

                if (litres == 0.5) litresMult = 0.5f;

                byEntity.ReceiveSaturation(nutriProps.Satiety * litresMult * satLossMul, nutriProps.FoodCategory);
                IPlayer player = (byEntity as EntityPlayer)?.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                SplitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);

                if (nutriProps.Intoxication > 0f)
                {
                    var intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                    byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(litresToDrink, intox + (nutriProps.Intoxication * litresMult)));
                }

                var healthChange = nutriProps.Health * litresMult * healthLossMul;
                if (healthChange != 0)
                {
                    byEntity.ReceiveDamage(new DamageSource()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                    }, Math.Abs(healthChange));
                }

                slot.MarkDirty();
                player.InventoryManager.BroadcastHotbarSlot();

                if (GetCurrentLitres(slot.Itemstack) == 0) SetContent(slot.Itemstack, null); //null it out

                return;
            }
        }

        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            return Attributes[transType == EnumTransitionType.Perish ? "perishRate" : "cureRate"].AsFloat(1);
        }

        public override float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            return Attributes[transType == EnumTransitionType.Perish ? "perishRate" : "cureRate"].AsFloat(1);
        }

        public float SatMult=> Attributes?["satMult"].AsFloat(1f) ?? 1f;

        public FoodNutritionProperties[] GetPropsFromArray(float[] satieties)
        {
            if (satieties == null || satieties.Length < 6) return null;

            List<FoodNutritionProperties> props = [];

            for (int i = 1; i < satieties.Length; i++)
            {
                if (satieties[i] != 0) props.Add(new() { FoodCategory = (EnumFoodCategory)(i - 1), Satiety = satieties[i] * SatMult });
            }

            if (satieties[0] != 0 && props.Count > 0) props[0].Health = satieties[0] * SatMult;

            return [.. props];
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (GetContent(inSlot.Itemstack) is ItemStack content)
            {
                string contentPath = content.Collectible.Code.Path;
                string newDescription = content.Collectible.Code.Domain + ":itemdesc-" + contentPath;
                string finalDescription = Lang.GetMatching(newDescription);

                var dummy = new DummySlot(content);

                if (finalDescription != newDescription)
                {
                    dsc.AppendLine();
                    dsc.Append(finalDescription);
                }

                EntityPlayer entity = world.Side == EnumAppSide.Client ? (world as IClientWorldAccessor).Player.Entity : null;
                float spoilState = AppendPerishableInfoText(dummy, new StringBuilder(), world);

                var nutriProps = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, dummy, content, entity);

                FoodNutritionProperties[] addProps = GetPropsFromArray((content.Attributes["expandedSats"] as FloatArrayAttribute)?.value);

                if (nutriProps != null && addProps?.Length > 0)
                {
                    dsc.AppendLine();
                    dsc.AppendLine(Lang.Get("efrecipes:Extra Nutrients"));

                    foreach (FoodNutritionProperties props in addProps)
                    {
                        double liquidVolume = content.StackSize;
                        float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, content, entity);
                        float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, content, entity);

                        if (Math.Abs(props.Health * healthLossMul) > 0.001f)
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {2} sat, {1} hp", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10), 1), props.Health * healthLossMul * (liquidVolume / 10), props.FoodCategory.ToString()));
                        }
                        else
                        {
                            dsc.AppendLine(Lang.Get("efrecipes:- {0} {1} sat", Math.Round(props.Satiety * satLossMul * (liquidVolume / 10)), props.FoodCategory.ToString()));
                        }
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                var handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);

                if (handling is EnumHandHandling.PreventDefault or EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || hotbarSlot.Itemstack.Collectible is not ILiquidInterface) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            var obj = hotbarSlot.Itemstack.Collectible;
            var singleTake = byPlayer.WorldData.EntityControls.Sneak;
            var singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource && !singleTake)
            {
                var moved = TryPutLiquid(blockSel.Position, (obj as ILiquidSource).GetContent(hotbarSlot.Itemstack), singlePut ? 1 : 9999);

                if (moved > 0)
                {
                    (obj as ILiquidSource).TryTakeContent(hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            if (obj is ILiquidSink && !singlePut)
            {
                var owncontentStack = GetContent(blockSel.Position);
                int moved;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = (obj as ILiquidSink).TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    var containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutLiquid(containerStack, owncontentStack, singleTake ? 1 : 9999);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }
            return true;
        }


        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            if (!entityItem.Swimming || entityItem.World.Side != EnumAppSide.Server) return;

            var contents = GetContent(entityItem.Itemstack);
            if (contents?.Collectible.Code.Path == "rot")
            {
                entityItem.World.SpawnItemEntity(contents, entityItem.ServerPos.XYZ);
                SetContent(entityItem.Itemstack, null);
            }
        }


        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            if (blockSel == null) return false;

            var pos = blockSel.Position;
            var byPlayer = (byEntity as EntityPlayer)?.Player;
            var blockAcc = byEntity.World.BlockAccessor;
            var secondPos = blockSel.Position.AddCopy(blockSel.Face);
            var contentStack = GetContent(containerSlot.Itemstack);
            var props = GetContentProps(containerSlot.Itemstack);

            if (!(props?.WhenSpilled != null && props.AllowSpill)) return false;

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak)) return false;

            var action = props.WhenSpilled.Action;
            var currentlitres = GetCurrentLitres(containerSlot.Itemstack);

            if (currentlitres > 0 && currentlitres < 10) action = WaterTightContainableProps.EnumSpilledAction.DropContents;

            if (action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                var waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);
                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out var fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                if (blockAcc.GetBlock(pos).Replaceable >= 6000)
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
                    else return false;
                }
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");
                var stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = contentStack.StackSize;
                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }

            var moved = SplitStackAndPerformAction(byEntity, containerSlot, (stack) =>
            {
                SetContent(stack, null);
                return contentStack.StackSize;
            });

            DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Pour);
            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions;
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return
            [
                new()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => GetCurrentLitres(inSlot.Itemstack) > 0,
                },
                 new()
                {
                    ActionLangCode = "aculinaryartillery:heldhelp-drink",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => (GetContent(inSlot.Itemstack)?.GetName() is not null and not "Water") && GetCurrentLitres(inSlot.Itemstack) > 0,
                },
                new()
                {
                    ActionLangCode = "heldhelp-fill",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => bs != null && (api.World.BlockAccessor.GetBlock(bs.Position.AddCopy(bs.Face))?.Code.GetName().Contains("water-") == true) && GetCurrentLitres(inSlot.Itemstack) == 0,
                },
                new()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => true
                }
            ];
        }
    }

    /*************************************************************************************************************/
    public class BottleTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition contentTextPos;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;
        private readonly CompositeTexture contentTexture;

        public BottleTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block bottle)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "map");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "glass");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "map" && corkTextPos != null) return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null) return blockTextPos;

                if (contentTextPos == null)
                {
                    int textureSubId;
                    textureSubId = ObjectCacheUtil.GetOrCreate(capi, "contenttexture-" + contentTexture?.ToString() ?? "unkowncontent", () =>
                    {
                        var id = 0;
                        var bmp = capi.Assets.TryGet(contentTexture?.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png") ?? new AssetLocation("aculinaryartillery:textures/block/unknown.png"))?.ToBitmap(capi);

                        if (bmp != null)
                        {
                            //if (contentTexture.Alpha != 255)
                            //{ bmp.MulAlpha(contentTexture.Alpha); }

                            // for now, a try catch will have to suffice - barf
                            try
                            {
                                capi.BlockTextureAtlas.InsertTexture(bmp, out id, out var texPos);
                            }
                            catch
                            {

                            }
                            bmp.Dispose();
                        }
                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}