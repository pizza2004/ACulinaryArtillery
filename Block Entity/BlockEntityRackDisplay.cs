using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public abstract class BlockEntityRackDisplay : BlockEntityContainer, ITexPositionSource
    {
        protected Item? nowTesselatingItem;
        protected Shape? nowTesselatingShape;
        protected ICoreClientAPI? capi;
        protected MeshData?[] meshes;
        protected MealMeshCache ms = null!;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public virtual string AttributeTransformCode => "onDisplayTransform";

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation? assetLocation = null;
                CompositeTexture? compositeTexture;

                if (nowTesselatingItem.Textures.TryGetValue(textureCode, out compositeTexture)) assetLocation = compositeTexture.Baked.BakedName;
                else if (nowTesselatingItem.Textures.TryGetValue("all", out compositeTexture)) assetLocation = compositeTexture.Baked.BakedName;
                else nowTesselatingShape?.Textures.TryGetValue(textureCode, out assetLocation);

                assetLocation ??= new AssetLocation(textureCode);

                TextureAtlasPosition texPos = capi.BlockTextureAtlas[assetLocation];
                if (texPos == null)
                {
                    if (capi.Assets.TryGet(assetLocation.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png")) != null)
                    {
                        capi.BlockTextureAtlas.GetOrInsertTexture(assetLocation, out int _, out texPos);
                    }
                    else capi.World.Logger.Warning("For render in block {3}, item {0} defined texture {1}, not no such texture found.", nowTesselatingItem.Code, assetLocation, Block.Code?.ToString());
                }

                return texPos;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.ms = api.ModLoader.GetModSystem<MealMeshCache>();
            this.capi = api as ICoreClientAPI;

            if (this.capi == null) return;

            updateMeshes();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            for (int index = 0; index < meshes.Length; ++index)
            {
                if (meshes[index] != null) mesher.AddMeshData(meshes[index]);
            }

            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            
            if (Api is not ICoreClientAPI)  return;
            
            updateMeshes();
        }

        protected virtual void updateMeshes()
        {
            for (int index = 0; index < meshes.Length; ++index) updateMesh(index);
        }

        protected virtual void updateMesh(int index)
        {
            if (Api is not ICoreClientAPI) return;

            if (Inventory[index].Empty) meshes[index] = null;
            else
            {
                MeshData? mesh = genMesh(Inventory[index].Itemstack, index);
                translateMesh(mesh, index);
                meshes[index] = mesh;
            }
        }

        protected virtual MeshData? genMesh(ItemStack stack, int index)
        {
            if (capi == null) return null;

            MeshData? modeldata;
            if (stack.Class == EnumItemClass.Block)
            {
                modeldata = !(stack.Block is BlockPie) ? capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone() : ms.GetPieMesh(stack);
            }
            else
            {
                nowTesselatingItem = stack.Item;

                if (stack.Item.Shape != null) nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);

                capi.Tesselator.TesselateItem(stack.Item, out modeldata, this);

                if (stack.Collectible.Attributes?[AttributeTransformCode]?.AsObject<ModelTransform>() is ModelTransform transform)
                {
                    transform.EnsureDefaultValues();
                    modeldata.ModelTransform(transform);
                }

                if (stack.Item.Shape?.VoxelizeTexture != false)
                {
                    modeldata.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 1.570796f, 0.0f, 0.0f);
                    modeldata.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.33f, 0.5f, 0.33f);
                    modeldata.Translate(0.0f, -15f / 32f, 0.0f);
                }

                modeldata.RenderPassesAndExtraBits.Fill<short>(2);
            }
            return modeldata;
        }

        protected virtual void translateMesh(MeshData? mesh, int index)
        {

        }
    }
}