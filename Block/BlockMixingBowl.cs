﻿using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace ACulinaryArtillery
{
    public class BlockMixingBowl : BlockMPBase
    {
        public int CapacityLitres { get; set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            
            CapacityLitres = Attributes?["capacityLitres"]?.AsInt(CapacityLitres) ?? CapacityLitres;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                if (!tryConnect(world, byPlayer, blockSel.Position, BlockFacing.UP))
                {
                    tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
                }

                return true;
            }

            return false;
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMixingBowl beBowl)
            {
                if (byPlayer.Entity.Controls.Sprint &&(blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
                {
                    beBowl.ToggleLock(byPlayer);
                    return true;
                }

                if (beBowl.CanMix() && (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
                {
                    beBowl.SetPlayerMixing(byPlayer, true);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMixingBowl beBowl &&
                (blockSel.SelectionBoxIndex == 1 || beBowl.Inventory.openedByPlayerGUIds.Contains(byPlayer.PlayerUID)))
            {
                beBowl.IsMixing(byPlayer);
                return beBowl.CanMix();
            }

            return false;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl)?.SetPlayerMixing(byPlayer, false);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMixingBowl)?.SetPlayerMixing(byPlayer, false);

            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (selection.SelectionBoxIndex == 0)
            {
                return new WorldInteraction[] 
                {
                    new()
                    {
                        ActionLangCode = "blockhelp-quern-addremoveitems",
                        MouseButton = EnumMouseButton.Right
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
            else
            {
                return new WorldInteraction[]
                {
                    new()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-mix",
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>  (world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl)?.CanMix() == true
                    },
                    new()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autounlock",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        ShouldApply = (wi, bs, es) => (world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl)?.invLocked == true
                    },
                    new()
                    {
                        ActionLangCode = "aculinaryartillery:blockhelp-mixingbowl-autolock",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        ShouldApply = (wi, bs, es) => (world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityMixingBowl)?.invLocked == false
                    }
                }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
            }
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return face == BlockFacing.UP || face == BlockFacing.DOWN;
        }
    }
}