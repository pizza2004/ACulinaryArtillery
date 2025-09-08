using System;
using System.Collections.Generic;
using System.Linq;
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
        protected virtual AssetLocation EmptyShapeLoc => props.EmptyShapeLoc ?? Shape.Base;
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

        public override byte[]? GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack? stack = null)
        {
            return GetContent(stack)?.Item?.LightHsv ?? base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            props = Attributes?["liquidContainerProps"]?.AsObject(props, Code.Domain) ?? props;

            if (api is not ICoreClientAPI capi) return;

            interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(capi, "bottle", () => {
                ItemStack[] liquidContainerStacks = [.. capi.World.Collectibles.Where(obj => obj is ILiquidSource or ILiquidSink)?
                                                                               .SelectMany(obj => obj.GetHandBookStacks(capi))?
                                                                               .Where(stack => stack != null) ?? []];

                foreach (var stack in liquidContainerStacks) stack.StackSize = 1;

                return [ new() {
                    ActionLangCode = "blockhelp-behavior-rightclickpickup",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                }, new() {
                    ActionLangCode = "blockhelp-bucket-rightclick",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = liquidContainerStacks
                }];
            });
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Code.Path.Contains("clay")) return;

            Dictionary<int, MultiTextureMeshRef> meshrefs;
            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else capi.ObjectCache[MeshRefsCacheKey] = meshrefs = [];

            if (GetContent(itemstack) is not ItemStack contentStack) return;

            var hashcode = (contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString()).GetHashCode();
            if (!meshrefs.TryGetValue(hashcode, out var meshRef))
            {
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(GenMesh(capi, contentStack));
            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api is not ICoreClientAPI capi) return;

            if (capi.ObjectCache.TryGetValue(MeshRefsCacheKey, out var obj))
            {
                foreach (var val in obj as Dictionary<int, MultiTextureMeshRef> ?? []) val.Value.Dispose();

                capi.ObjectCache.Remove(MeshRefsCacheKey);
            }
        }

        public MeshData? GenMesh(ICoreClientAPI? capi, ItemStack? contentStack, BlockPos? forBlockPos = null)
        {
            if (capi == null) return null;
            MeshData? mesh = null;

            if (contentStack != null && (!Code.Path.Contains("clay")))
            {
                if (GetContainableProps(contentStack) is WaterTightContainableProps props)
                {
                    var level = contentStack.StackSize / props.ItemsPerLitre;
                    AssetLocation loc = (props.IsOpaque ? ContentShapeLoc : LiquidContentShapeLoc).Clone().WithPathPrefixOnce("shapes/").WithoutPathAppendix(".json");
                    loc.WithPathAppendix((level <= 0.25f && level > 0) ? "-1" : (level <= 0.5f ? "-2" : (level < 1 ? "-3" : ""))).WithPathAppendixOnce(".json");

                    capi.Tesselator.TesselateShape("bottle", capi.Assets.TryGet(loc).ToObject<Shape>(), out mesh, new BottleTextureSource(capi, contentStack, props.Texture, this), new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                    for (int i = 0; i < mesh.Flags.Length; i++) mesh.Flags[i] = mesh.Flags[i] & ~(1 << 12); // Remove water waving flag

                    // Water flags
                    if (forBlockPos != null)
                    {
                        mesh.CustomInts = new CustomMeshDataPartInt(mesh.FlagsCount) { Count = mesh.FlagsCount };
                        mesh.CustomInts.Values.Fill(0x4000000); // light foam only
                        mesh.CustomFloats = new CustomMeshDataPartFloat(mesh.FlagsCount * 2) { Count = mesh.FlagsCount * 2 };
                    }
                }
                else ACulinaryArtillery.logger?.Error($"Bottle with Item {contentStack.Item.Code} does not have waterTightProps and will not render or work correctly. This is usually caused by removing mods. If not, check with the items author.");
            }
            else capi.Tesselator.TesselateShape(this, capi.Assets.TryGet(EmptyShapeLoc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json")).ToObject<Shape>(), out mesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

            return mesh;
        }

        public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? forBlockPos = null)
        {
            return GenMesh(api as ICoreClientAPI, GetContent(itemstack), forBlockPos);
        }

        public MeshData GenMeshSideways(ICoreClientAPI capi, ItemStack? contentStack, BlockPos? forBlockPos = null)
        {
            if (capi.Assets.TryGet(EmptyShapeLoc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json")) is not IAsset asset) return new MeshData();

            capi.Tesselator.TesselateShape(this, asset.ToObject<Shape>(), out var mesh, new(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            if (contentStack != null && (!Code.Path.Contains("clay")))
            {
                if (GetContainableProps(contentStack) is WaterTightContainableProps props)
                {
                    // unlike genmesh, were only rendering the contents at this point
                    var level = contentStack.StackSize / props.ItemsPerLitre;
                    AssetLocation loc = "aculinaryartillery:block/bottle/contents-";
                    loc.WithPathAppendix((level <= 0.25f && level > 0) ? "side-1" : (level <= 0.5f ? "side-2" : (level < 1 ? "side-3" : "full")));

                    asset = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                    if (asset == null) return mesh;

                    capi.Tesselator.TesselateShape(GetType().Name, asset.ToObject<Shape>(), out var contentMesh, new BottleTextureSource(capi, contentStack, props.Texture, this), new(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
                    for (int i = 0; i < contentMesh.Flags.Length; i++) contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag

                    // Water flags
                    if (forBlockPos != null)
                    {
                        contentMesh.CustomInts = new CustomMeshDataPartInt(contentMesh.FlagsCount) { Count = contentMesh.FlagsCount };
                        contentMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                        contentMesh.CustomFloats = new CustomMeshDataPartFloat(contentMesh.FlagsCount * 2) { Count = contentMesh.FlagsCount * 2 };

                        mesh.CustomInts = new CustomMeshDataPartInt(mesh.FlagsCount) { Count = mesh.FlagsCount };
                        mesh.CustomInts.Values.Fill(0x4000000); // light foam only
                        mesh.CustomFloats = new CustomMeshDataPartFloat(mesh.FlagsCount * 2) { Count = mesh.FlagsCount * 2 };
                    }
                    mesh.AddMeshData(contentMesh);
                }
            }
            return mesh;
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = GetContent(itemstack);
            return itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
        }

        public string GetContainedInfo(ItemSlot inSlot)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack? contentStack = GetContent(inSlot.Itemstack);

            if (contentStack == null || litres <= 0) return Lang.GetWithFallback("contained-empty-container", "{0} (Empty)", inSlot.Itemstack.GetName());

            string incontainername = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);

            return Lang.Get("contained-liquidcontainer-compact", inSlot.Itemstack.GetName(), litres, incontainername, PerishableInfoCompactContainer(api, inSlot));
        }


        public string GetContainedName(ItemSlot inSlot, int quantity)
        {
            return inSlot.Itemstack.GetName();
        }

        protected override bool tryEatStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, ItemStack? content = null)
        {
            if (GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) == null) return false;

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
            if (secondsUsed < 0.95f || byEntity.World is not IServerWorldAccessor) return;
            if (GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) is not FoodNutritionProperties nutriProps) return;

            var litres = GetCurrentLitres(slot.Itemstack);
            var litresToDrink = litres >= 0.25f ? 0.25f : litres;

            var state = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);

            var litresMult = litres == 1 ? 0.25f : (litres == 0.75 ? 0.3333f : (litres == 0.5 ? 0.5f : 1.0f));

            byEntity.ReceiveSaturation(nutriProps.Satiety * litresMult * GlobalConstants.FoodSpoilageSatLossMul(state?.TransitionLevel ?? 0, slot.Itemstack, byEntity), nutriProps.FoodCategory);
            IPlayer? player = (byEntity as EntityPlayer)?.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            SplitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);

            if (nutriProps.Intoxication > 0f)
            {
                var intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(litresToDrink, intox + (nutriProps.Intoxication * litresMult)));
            }

            var healthMod = nutriProps.Health * litresMult * GlobalConstants.FoodSpoilageHealthLossMul(state?.TransitionLevel ?? 0, slot.Itemstack, byEntity);
            if (healthMod != 0) byEntity.ReceiveDamage(new() { Source = EnumDamageSource.Internal, Type = healthMod > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthMod));

            slot.MarkDirty();
            player?.InventoryManager.BroadcastHotbarSlot();

            if (GetCurrentLitres(slot.Itemstack) == 0) SetContent(slot.Itemstack, null); //null it out

            return;
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

        public FoodNutritionProperties[]? GetPropsFromArray(float[]? satieties)
        {
            if (satieties == null || satieties.Length < 6) return null;

            List<FoodNutritionProperties> props = [];
            for (int i = 1; i <= 5; i++)
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

                EntityPlayer? entity = (world as IClientWorldAccessor)?.Player.Entity;
                float spoilState = AppendPerishableInfoText(dummy, new StringBuilder(), world);

                var nutriProps = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, dummy, content, entity);

                FoodNutritionProperties[]? addProps = GetPropsFromArray((content.Attributes["expandedSats"] as FloatArrayAttribute)?.value);

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

            if (obj is ILiquidSource source && !singleTake)
            {
                var moved = TryPutLiquid(blockSel.Position, source.GetContent(hotbarSlot.Itemstack), singlePut ? 1 : 9999);

                if (moved > 0)
                {
                    source.TryTakeContent(hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            if (obj is ILiquidSink sink && !singlePut)
            {
                var owncontentStack = GetContent(blockSel.Position);
                int moved;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = sink.TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    var containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = sink.TryPutLiquid(containerStack, owncontentStack, singleTake ? 1 : 9999);

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
        private TextureAtlasPosition? contentTextPos;
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