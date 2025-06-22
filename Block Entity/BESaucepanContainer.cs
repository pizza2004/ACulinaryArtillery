using Vintagestory.GameContent;

namespace ACulinaryArtillery.Block_Entity
{
    public class BESaucepanContainer : InWorldContainer
    {
        private BlockEntitySaucepan BESaucepan;

        public BESaucepanContainer(BlockEntitySaucepan blockEntitySaucepan, InventorySupplierDelegate inventorySupplier, string treeAttrKey) : base(inventorySupplier, treeAttrKey)
        {
            BESaucepan = blockEntitySaucepan;
        }

        public override float GetPerishRate()
        {
            return base.GetPerishRate() * (BESaucepan.isSealed ? BESaucepan.Block.Attributes["lidPerishRate"].AsFloat(0.5f) : 1f);
        }
    }
}