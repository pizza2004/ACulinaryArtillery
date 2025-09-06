﻿using ACulinaryArtillery.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class CollectibleBehaviorEggCrack : CollectibleBehaviorSqueezable
    {
        public float ContainedEggLitres { get; set; }
        public bool IsCrackableEggType { get; set; }
        public bool IsEggYolk { get; set; }
        protected AssetLocation? whiteLiquidCode { get; set; }
        protected AssetLocation? yolkLiquidCode { get; set; }
        protected AssetLocation? fullLiquidCode { get; set; }
        protected AssetLocation? eggYolkCode { get; set; }
        protected AssetLocation? eggShellCode { get; set; }

        public SimpleParticleProperties particles;
        Random rand = new Random();

        public CollectibleBehaviorEggCrack(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            SqueezeTime = properties["crackTime"].AsFloat(0.5f);
            SqueezedLitres = ContainedEggLitres = properties["containedEggLitres"].AsFloat(0.1f);
            IsCrackableEggType = properties["isCrackableEggType"].AsBool(true);
            IsEggYolk = properties["isEggYolk"].AsBool(false);
            AnimationCode = properties["AnimationCode"].AsString("eggcrackstart");

            string code = properties["crackingSound"].AsString("aculinaryartillery:sounds/player/eggcrack");
            if (code != null)
            {
                SqueezingSound = AssetLocation.Create(code, collObj.Code.Domain);
            }

            string eggVariant = collObj.Variant["source"] ?? collObj.FirstCodePart(1);
            code = properties["whiteLiquidCode"].AsString("aculinaryartillery:eggwhiteportion");
            if (code != null)
            {
                liquidItemCode = whiteLiquidCode = AssetLocation.Create(code, collObj.Code.Domain);
            }

            code = properties["yolkLiquidCode"].AsString("aculinaryartillery:eggyolkportion-" + eggVariant);
            if (code != null)
            {
                yolkLiquidCode = AssetLocation.Create(code, collObj.Code.Domain);
            }

            code = properties["fullLiquidCode"].AsString("aculinaryartillery:eggyolkfullportion-" + eggVariant);
            if (code != null)
            {
                fullLiquidCode = AssetLocation.Create(code, collObj.Code.Domain);
            }

            code = properties["eggYolkCode"].AsString("aculinaryartillery:eggyolk-" + eggVariant);
            if (code != null)
            {
                eggYolkCode = AssetLocation.Create(code, collObj.Code.Domain);
            }

            code = properties["eggShellCode"].AsString("aculinaryartillery:eggshell");
            if (code != null)
            {
                eggShellCode = AssetLocation.Create(code, collObj.Code.Domain);
            }
        }

        WorldInteraction[] interactions;
        public override void OnLoaded(ICoreAPI api)
        {
            Item item = api.World.GetItem(yolkLiquidCode);
            float yolkItemsPerLitre = item?.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>(null, item.Code.Domain)?.ItemsPerLitre ?? 100;

            item = api.World.GetItem(fullLiquidCode);
            float fullItemsPerLitre = item?.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>(null, item.Code.Domain)?.ItemsPerLitre ?? 100;

            ReturnStacks = [
                new () {Type = EnumItemClass.Item, Code = yolkLiquidCode, StackSize = (int)(ContainedEggLitres * yolkItemsPerLitre)},
                new () {Type = EnumItemClass.Item, Code = fullLiquidCode, StackSize = (int)(ContainedEggLitres * fullItemsPerLitre)},
                new () {Type = EnumItemClass.Item, Code = eggYolkCode},
                new () {Type = EnumItemClass.Item, Code = eggShellCode}
            ];

            base.OnLoaded(api);

            if (api is not ICoreClientAPI capi) return;

            interactions = ObjectCacheUtil.GetOrCreate<WorldInteraction[]>(api, "eggInteractions", () =>
            {
                ItemStack[] stacks = [.. api.World.Blocks.Where(block => block.Code != null && (block is BlockBarrel || CanSqueezeInto(capi.World, block, null))).Select(block => new ItemStack(block))];

                return
                [
                    new()
                    {
                        ActionLangCode = "heldhelp-crack",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks,
                    },
                    new()
                    {
                        ActionLangCode = "heldhelp-crack2",
                        HotKeyCodes = ["sneak", "sprint"],
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks,
                    }
                ];
            });
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Block != null && CanSqueezeInto(byEntity.World, blockSel.Block, blockSel))
            {
                handling = EnumHandling.PreventDefault;

                if (!byEntity.Controls.ShiftKey) return false;
                if (byEntity.World is IClientWorldAccessor)
                {
                    byEntity.StartAnimation(AnimationCode);

                    ModelTransform tf = new ModelTransform();
                    tf.EnsureDefaultValues();

                    tf.Translation.Set(Math.Min(0.6f, secondsUsed * 2), 0, 0);
                    tf.Rotation.Y = Math.Min(20, secondsUsed * 90 * 2f);

                    if (secondsUsed > SqueezeTime - 0.13f)
                    {
                        tf.Translation.X += (float)Math.Sin(secondsUsed / 60);
                    }

                    if (secondsUsed > SqueezeTime - 0.1f)
                    {
                        tf.Translation.X += (float)Math.Sin(Math.Min(1.0, secondsUsed) * 5) * 0.75f;
                    }

                    if (secondsUsed > SqueezeTime - 0.01f)
                    {
                        byEntity.AnimManager.StopAnimation(AnimationCode);
                    }
                }

                return secondsUsed < SqueezeTime;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            byEntity.AnimManager.StopAnimation(AnimationCode);

            if (blockSel == null) return;
            if (secondsUsed < SqueezeTime - 0.02f) return;

            IWorldAccessor world = byEntity.World;
            IBlockAccessor blockAccessor = world.BlockAccessor;
            Block block = blockAccessor.GetBlock(blockSel.Position);

            Item liquidItem = byEntity.Controls.Sprint switch
            {
                _ when IsEggYolk => world.GetItem(yolkLiquidCode),
                true when IsCrackableEggType => world.GetItem(fullLiquidCode),
                false when IsCrackableEggType => world.GetItem(whiteLiquidCode),
                _ => null
            };

            ItemStack liquid = liquidItem == null ? null : new(liquidItem, 99999);

            if (liquid == null || !CanSqueezeInto(world, block, blockSel)) return;

            if (world.Side == EnumAppSide.Client)
            {
                byEntity.World.PlaySoundAt(SqueezingSound, byEntity, null, true, 16, 0.5f);

                // Primary Particles
                var color = ColorUtil.ToRgba(255, 219, 206, 164);

                particles = new SimpleParticleProperties(
                    4, 6, // quantity
                    color,
                    // spawn particles above ellipses covering top plane of target block collision box:
                    //  - at the edge, on a line facing the aiming point
                    block
                        .GetCollisionBoxes(blockAccessor, null)
                        .OrderByDescending(cf => cf.MaxY)
                        .FirstOrDefault() switch
                    {
                        null => new Vec3d(0.35, 0.1, 0.35),
                        var b => b.TopFaceEllipsesLineIntersection(blockSel.HitPosition.ToVec3f())
                    },
                    new Vec3d(), //add position - see below
                    new Vec3f(0.2f, 0.5f, 0.2f), //min velocity
                    new Vec3f(), //add velocity - see below
                    (float)((rand.NextDouble() * 1f) + 0.25f), //life length
                    (float)((rand.NextDouble() * 0.05f) + 0.2f), //gravity effect 
                    0.25f, 0.5f, //size
                    EnumParticleModel.Cube // model
                    );

                particles.AddVelocity.Set(new Vec3f(-0.4f, 0.5f, -0.4f)); //add velocity
                particles.SelfPropelled = true;

                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);

                particles.MinPos.Add(blockSel.Position);                            // add selection position
                particles.MinPos.Add(block.TopMiddlePos); // add sub block selection position
                particles.AddPos.Set(new Vec3d(0, 0, 0)); //add position
                world.SpawnParticles(particles);
            }
            else
            {
                if (block is BlockLiquidContainerTopOpened blockCnt)
                {
                    if (blockCnt.TryPutLiquid(blockSel.Position, liquid, ContainedEggLitres) == 0) return;
                }
                else if (block is BlockBarrel blockBarrel && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb)
                {
                    if (beb.Sealed) return;
                    if (blockBarrel.TryPutLiquid(blockSel.Position, liquid, ContainedEggLitres) == 0) return;
                }
                else if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage beg &&
                        beg.GetSlotAt(blockSel) is ItemSlot squeezeIntoSlot &&
                        squeezeIntoSlot.Itemstack?.Block is BlockLiquidContainerTopOpened begCnt &&
                        CanSqueezeInto(world, begCnt, null))
                {
                    if (begCnt.TryPutLiquid(squeezeIntoSlot.Itemstack, liquid, ContainedEggLitres) == 0) return;
                    beg.MarkDirty(true);
                }

            }

            slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            ItemStack returnStack = new(world.GetItem(IsEggYolk ? eggYolkCode : eggShellCode));
            if (byPlayer?.InventoryManager.TryGiveItemstack(returnStack) == false)
            {
                byEntity.World.SpawnItemEntity(returnStack, byEntity.SidedPos.XYZ);
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot, ref handling));
        }

        protected override void AddSqueezableHandbookInfo(ICoreClientAPI capi)
        {
            JToken token;
            ExtraHandbookSection?[]? extraHandbookSections = collObj.Attributes?["handbook"]?["extraSections"]?.AsObject<ExtraHandbookSection[]>();

            if (extraHandbookSections?.FirstOrDefault(s => s?.Title == "aculinaryartillery:handbook-crackinghelp-title") != null) return;

            if (collObj.Attributes?["handbook"].Exists != true)
            {
                if (collObj.Attributes == null) collObj.Attributes = new JsonObject(JToken.Parse("{ handbook: {} }"));
                else
                {
                    token = collObj.Attributes.Token;
                    token["handbook"] = JToken.Parse("{ }");
                }
            }

            ExtraHandbookSection section = new ExtraHandbookSection() { Title = "aculinaryartillery:handbook-crackinghelp-title", Text = "aculinaryartillery:handbook-crackinghelp-text" };
            if (extraHandbookSections != null) extraHandbookSections.Append(section);
            else extraHandbookSections = [section];

            token = collObj.Attributes["handbook"].Token;
            token["extraSections"] = JToken.FromObject(extraHandbookSections);

        }
    }
}