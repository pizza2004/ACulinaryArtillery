using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class BlockEntityBottle : BlockEntityContainer
    {
        public override InventoryBase Inventory => inv;
        private readonly InventoryGeneric inv;
        public override string InventoryClassName => "bottle";

        private BlockBottle ownBlock;
        private MeshData currentMesh;

        public BlockEntityBottle()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            ownBlock = Block as BlockBottle;
            Inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed1;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        private float Inventory_OnAcquireTransitionSpeed1(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            float mul = baseMul * ownBlock?.GetContainingTransitionModifierPlaced(Api.World, Pos, transType) ?? 1;
            return mul;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        internal MeshData GenMesh()
        {
            if (ownBlock == null || ownBlock.Code.Path.Contains("clay"))
            { return null; }

            var mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);
            return mesh;
        }

        public ItemStack GetContent()
        {
            return inv[0].Itemstack;
        }

        internal void SetContent(ItemStack stack)
        {
            inv[0].Itemstack = stack;
            MarkDirty(true);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null || ownBlock.Code.Path.Contains("clay"))
            { return false; }
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, 0));
            return true;
        }
    }
}