﻿using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{

    public class ItemExpandedRawFood : Item, IExpandedFood, ITexPositionSource, IContainedMeshSource, IBakeableCallback
    {
        public Size2i AtlasSize => targetAtlas.Size;
        public TextureAtlasPosition this[string textureCode] => GetOrCreateTexPos(GetTexturePath(textureCode));
        protected ITextureAtlasAPI targetAtlas;
        protected Shape nowTesselatingShape;

        public override bool Satisfies(ItemStack thisStack, ItemStack otherStack)
        {
            return (thisStack.Class == otherStack.Class && thisStack.Id == otherStack.Id &&
                    !otherStack.Attributes.HasAttribute("madeWith") && thisStack.Attributes.HasAttribute("madeWith"))
                    || base.Satisfies(thisStack, otherStack);
        }

        public float SatMult => Attributes?["satMult"].AsFloat(1f) ?? 1f;

        protected AssetLocation GetTexturePath(string textureCode)
        {
            AssetLocation texturePath = null;
            CompositeTexture tex;

            if (Textures.TryGetValue(textureCode, out tex)) texturePath = tex.Baked.BakedName; // Prio 1: Get from collectible textures
            else if (Textures.TryGetValue("all", out tex)) texturePath = tex.Baked.BakedName; // Prio 2: Get from collectible textures, use "all" code
            else nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath); // Prio 3: Get from currently tesselating shape

            return texturePath ??= new AssetLocation(textureCode); // Prio 4: The code is the path
        }

        protected TextureAtlasPosition GetOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = targetAtlas[texturePath];
            if (texpos != null) return texpos;

            IAsset texAsset = (api as ICoreClientAPI).Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
            if (targetAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => texAsset?.ToBitmap(api as ICoreClientAPI))) return texpos;

            (api as ICoreClientAPI).World.Logger.Warning("Item {0} defined texture {1}, but no such texture was found.", Code, texturePath);
            return targetAtlas.UnknownTexturePosition;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, GridRecipe byRecipe)
        {
            base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);

            HashSet<string> ingredients = [];
            float[] sat = new float[6];

            foreach (ItemSlot slot in allInputslots)
            {
                if (slot.Itemstack == null) continue;

                CraftingRecipeIngredient match = byRecipe?.Ingredients?.Values.FirstOrDefault(ing => ing.SatisfiesAsIngredient(slot.Itemstack));

                if (slot.Itemstack.Collectible is ItemExpandedRawFood)
                {
                    string[] addIngs = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;

                    if (addSat?.Length == 6) sat = [.. sat.Zip(addSat, (x, y) => x + (y * match?.Quantity ?? 1))];
                    if (addIngs?.Length > 0) ingredients.AddRange(addIngs);
                }
                else
                {
                    var collObj = slot.Itemstack.Collectible;
                    GetNutrientsFromIngredient(ref sat, collObj, match?.Quantity ?? 1);
                    ingredients.Add(collObj.Code.Domain + ":" + collObj.Code.Path);
                }
            }

            outputSlot.Itemstack.Attributes["madeWith"] = new StringArrayAttribute([.. ingredients.Order()]);
            outputSlot.Itemstack.Attributes["expandedSats"] = new FloatArrayAttribute([.. sat]);
        }

        public void OnCreatedByKneading(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input, ItemStack output)
        {
            HashSet<string> ingredients = [];
            float[] sat = new float[6];

            foreach (var val in input)
            {
                if (val.Key.Itemstack.Collectible is ItemExpandedRawFood)
                {
                    var stack = val.Key.Itemstack;
                    string[] addIngs = (stack.Attributes["madeWith"] as StringArrayAttribute)?.value;
                    float[] addSat = (stack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;

                    if (addSat != null && addSat.Length == 6) sat = [.. sat.Zip(addSat, (x, y) => x + (y * (val.Value.Quantity / (stack.Collectible is ItemExpandedLiquid ? 10 : 1))))];
                    if (addIngs != null && addIngs.Length > 0) ingredients.AddRange(addIngs);
                }
                else
                {
                    var collObj = val.Key.Itemstack.Collectible;
                    GetNutrientsFromIngredient(ref sat, collObj, val.Value.Quantity);
                    ingredients.Add(collObj.Code.Domain + ":" + collObj.Code.Path);
                }
            }

            output.Attributes["madeWith"] = new StringArrayAttribute([.. ingredients.Order()]);
            output.Attributes["expandedSats"] = new FloatArrayAttribute([.. sat]);
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            if (inputStack.Collectible.CombustibleProps == null) return false;
            if (outputStack == null) return true;

            if (!inputStack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Equals(world, outputStack, [.. GlobalConstants.IgnoredStackAttributes, "madeWith", "expandedSats"])) return false;
            if (outputStack.StackSize >= outputStack.Collectible.MaxStackSize) return false;

            if (outputStack.Attributes["madeWith"] == null || !inputStack.Attributes["madeWith"].Equals(world, outputStack.Attributes["madeWith"])) return false;
            if (outputStack.Attributes["expandedSats"] == null || !inputStack.Attributes["expandedSats"].Equals(world, outputStack.Attributes["expandedSats"])) return false;

            return true;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack)) return;

            ItemStack smeltedStack = inputSlot.Itemstack.Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();

            // Copy over spoilage values but reduce them by a bit
            TransitionState state = UpdateAndGetTransitionState(world, new DummySlot(inputSlot.Itemstack), EnumTransitionType.Perish);

            if (state != null)
            {
                TransitionState smeltedState = smeltedStack.Collectible.UpdateAndGetTransitionState(world, new DummySlot(smeltedStack), EnumTransitionType.Perish);

                float nowTransitionedHours = state.TransitionedHours / (state.TransitionHours + state.FreshHours) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                smeltedStack.Collectible.SetTransitionState(smeltedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
            }

            string[] ingredients = (inputSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] satieties = (inputSlot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;


            if (ingredients != null) smeltedStack.Attributes["madeWith"] = new StringArrayAttribute(ingredients);
            if (satieties != null) smeltedStack.Attributes["expandedSats"] = new FloatArrayAttribute(satieties);

            // If the output slot isn't empty use TryMergeStacks to average spoilage rate and temperature
            outputSlot.Itemstack?.Collectible.TryMergeStacks(new(world, EnumMouseButton.Left, 0, EnumMergePriority.ConfirmedMerge, smeltedStack.StackSize)
            {
                SourceSlot = new DummySlot(smeltedStack),
                SinkSlot = outputSlot
            });
            outputSlot.Itemstack ??= smeltedStack; // Otherwise put the smelted stack into the output slot

            inputSlot.TakeOut(CombustibleProps.SmeltedRatio);
            outputSlot.MarkDirty();
        }

        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props)
        {
            string[] ings = (slot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] xNutr = (slot.Itemstack.Attributes["expandedSats"] as FloatArrayAttribute)?.value;

            ItemStack org = base.OnTransitionNow(slot, props);
            if (org?.Collectible is null or not ItemExpandedRawFood) return org;

            if (ings != null) org.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (xNutr?.Length > 0) org.Attributes["expandedSats"] = new FloatArrayAttribute(xNutr);

            return org;
        }

        public void GetNutrientsFromIngredient(ref float[] satHolder, CollectibleObject ing, int mult)
        {
            Dictionary<string, FoodNutritionProperties> expProps = Attributes?["expandedNutritionProps"]?.AsObject<Dictionary<string, FoodNutritionProperties>>();

            FoodNutritionProperties ingProps = null;
            expProps?.TryGetValue(FindMatch(ing.Code.Domain + ":" + ing.Code.Path, [.. expProps.Keys]), out ingProps);
            ingProps ??= ing.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
            ingProps ??= ing.NutritionProps;

            if (ingProps == null) return;

            if (ingProps.Health != 0) satHolder[(int)EnumNutritionMatch.Hp] += ingProps.Health * mult;

            int foodCat = (int)ingProps.FoodCategory + 1;
            if (foodCat >= 1 && foodCat <= 5) satHolder[foodCat] += ingProps.Satiety * mult;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string[] ings = [.. (inSlot.Itemstack.Attributes["madeWith"] as StringArrayAttribute)?.value
                                .Select(ing => new AssetLocation(ing))?
                                .Select(ing => Lang.GetIfExists("recipeingredient-block-" + ing.Path) ??
                                               Lang.GetIfExists("recipeingredient-item-" + ing.Path))?
                                .Where(ing => ing != null)
                                .Distinct() ?? []];

            if (ings?.Length > 0)
            {
                if (ings.Length == 1) dsc.AppendLine(Lang.Get("efrecipes:Made with ") + ings[0]);
                else dsc.AppendLine(Lang.Get("efrecipes:Made with ") + string.Join(", ", ings.Take(ings.Length - 1)) + Lang.Get("efrecipes:and ") + ings.Last());
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            RenderAlphaTest = 0.5f;
            string[] ings = (itemstack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null || ings.Length <= 0) return;

            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "expandedFoodGuiMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());

            string key = Code.ToShortString() + string.Join("|", ings);
            if (!meshrefs.TryGetValue(key, out var meshref))
            {
                if (GenMesh(capi.ItemTextureAtlas, ings, new Vec3f(0, 0, 0)) is not MeshData mesh) return;
                meshrefs[key] = meshref = capi.Render.UploadMultiTextureMesh(mesh);
            }

            if (meshref != null) renderinfo.ModelRef = meshref;
        }

        public virtual MeshData GenMesh(ITextureAtlasAPI targetAtlas, string[] ings, Vec3f rot = null, ITesselatorAPI tesselator = null)
        {
            this.targetAtlas = targetAtlas;
            nowTesselatingShape = null;
            tesselator ??= (api as ICoreClientAPI).Tesselator;

            var renderIngs = Attributes?["renderIngredients"]?.AsObject<TreeAttribute>();
            if (renderIngs == null) return null;

            List<AssetLocation> addShapes = [];
            List<Dictionary<string, string>> texureMappingsPerShape = [];
            for (int i = 0; i < ings.Length; i++)
            {
                Dictionary<string, string> textureMap = [];
                string match = FindMatch(ings[i], [.. renderIngs.Keys]);
                string path = renderIngs.GetAsString(match);
                if (renderIngs.GetTreeAttribute(match) is TreeAttribute keyValuePairs)
                {
                    string wildCard = WildcardUtil.GetWildcardValue(new AssetLocation(match), new AssetLocation(ings[i]));
                    path = keyValuePairs.GetAsString("shape");
                    TreeAttribute textureMappings = keyValuePairs.GetTreeAttribute("textureMap") as TreeAttribute;
                    foreach (var key in textureMappings?.Keys)
                    {
                        textureMap[key] = textureMappings.GetString(key).Replace("{" + keyValuePairs.GetAsString("name") + "}", wildCard);
                    }
                }

                if (path == null) continue;
                AssetLocation shape = new AssetLocation(path);
                if (shape != null)
                {
                    addShapes.Add(shape);
                    texureMappingsPerShape.Add(textureMap);
                }
            }

            if (addShapes.Count <= 0) return null;

            // Render first added ingredient before everything else to avoid transparent bread
            AssetLocation baseIngredient = addShapes.Last();
            Dictionary<string, string> baseMapping = texureMappingsPerShape.Last();

            MeshData mesh = null;
            float uvoffset = 0;
            for (int i = 0; i < addShapes.Count; i++)
            {
                if (!addShapes[i].Valid) continue;
                Shape addShape = (api as ICoreClientAPI).Assets.TryGet(addShapes[i]).ToObject<Shape>();
                if (addShape == null) continue;

                Shape clonedAddShape = addShape.Clone();
                if (addShape.Textures != null) clonedAddShape.Textures = new (addShape.Textures);

                MeshData addIng;
                if (addShape.Textures?.Keys is not null && texureMappingsPerShape[i].Count > 0)
                {
                    foreach (var key in texureMappingsPerShape[i].Keys)
                    {
                        string clonedKey = key;
                        if (clonedAddShape.Textures.ContainsKey(key)) clonedKey = texureMappingsPerShape[i][key];
                        clonedAddShape.Textures[key] = GetTexturePath(clonedKey); // path to desired texture
                    }

                    ShapeTextureSource textureSource = new(api as ICoreClientAPI, clonedAddShape, null);

                    if (addShapes.Where(x => x != null && x == addShapes[i]).Count() > 1)
                    {
                        int texHeight = clonedAddShape.TextureHeight;
                        int texWidth = clonedAddShape.TextureWidth;
                        foreach (ShapeElement elm in clonedAddShape.Elements)
                        {
                            foreach (ShapeElementFace face in elm.FacesResolved)
                            {
                                if (face != null)
                                {
                                    float faceWidth = Math.Abs(face.Uv[2] - face.Uv[0]);
                                    float faceHeight = Math.Abs(face.Uv[3] - face.Uv[1]);
                                    float ustart = (float)Math.Floor(uvoffset * (texWidth - faceWidth));
                                    float vstart = (float)Math.Floor(uvoffset * (texHeight - faceHeight));
                                    face.Uv[0] = ustart;
                                    face.Uv[1] = vstart;
                                    face.Uv[2] = ustart + faceWidth;
                                    face.Uv[3] = vstart + faceHeight;
                                }
                            }
                        }
                        uvoffset += 0.0625f;
                    }

                    tesselator.TesselateShape("ACA", clonedAddShape, out addIng, textureSource, rot);
                }
                else tesselator.TesselateShape("ACA", clonedAddShape, out addIng, this, rot);

                mesh?.AddMeshData(addIng);
                mesh ??= addIng;
            }

            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
            return mesh;
        }

        public virtual MeshData GenMesh(ItemStack stack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            this.targetAtlas = targetAtlas;
            nowTesselatingShape = null;
            var be = api.World.BlockAccessor.GetBlockEntity(atBlockPos);

            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings?.Length > 0) return GenMesh(targetAtlas, ings, new Vec3f(0, be.Block.Shape.rotateY, 0), (api as ICoreClientAPI).Tesselator);

            if (stack.Item?.Shape?.Base is AssetLocation loc) nowTesselatingShape = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(loc);
            (api as ICoreClientAPI).Tesselator.TesselateItem(stack.Item, out MeshData mesh, this);
            mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
            return mesh;
        }

        public string GetMeshCacheKey(ItemStack stack)
        {
            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null) return Code.ToShortString();

            return Code.ToShortString() + string.Join(",", ings);
        }

        public MeshData GenMesh(ICoreClientAPI capi, string[] ings, ItemStack stack, Vec3f rot = null, ITesselatorAPI tesselator = null)
        {
            tesselator ??= capi.Tesselator;

            TreeAttribute renderIngs = Attributes?["renderIngredients"]?.AsObject<TreeAttribute>();
            if (renderIngs == null) return null;

            List<AssetLocation> addShapes = [];
            for (int i = 0; i < ings.Length; i++)
            {
                if (renderIngs.GetAsString(FindMatch(ings[i], [.. renderIngs.Keys])) is not string path) continue;
                AssetLocation shape = new AssetLocation(path);
                if (shape != null) addShapes.Add(shape);
            }

            if (addShapes.Count <= 0) return new MeshData();

            MeshData mesh = null;
            for (int i = 0; i < addShapes.Count; i++)
            {
                Shape addShape;
                if (!addShapes[i].Valid || (addShape = capi.Assets.TryGet(addShapes[i]).ToObject<Shape>()) == null)
                    continue;

                if (addShape.Textures != null)
                {
                    stack.Item.Textures.TryGetValue(addShape.Textures.Keys.First(), out CompositeTexture tex2);
                    tesselator.TesselateShape("expandedfood", addShape, out var addIng, new EFTextureSource(capi, stack, tex2), rot);
                    mesh?.AddMeshData(addIng);
                    mesh ??= addIng;
                }
            }

            return mesh;
        }

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

        public string FindMatch(string needle, string[] haystack)
        {
            if (needle != null && haystack?.Length > 0)
            {
                foreach (string hay in haystack) if (hay == needle || WildcardUtil.Match(hay, needle)) return hay;
            }

            return "";
        }

        // Get Nutrition Properties for a SINGLE STACK
        // SPANG - March 13, 2022
        public static FoodNutritionProperties[] GetExpandedContentNutritionProperties(IWorldAccessor world, ItemSlot inSlot, ItemStack contentStack, EntityAgent? forEntity, bool mulWithStacksize = false, float nutritionMul = 1f, float healthMul = 1f)
        {
            List<FoodNutritionProperties> foodProps = [];

            CollectibleObject obj = contentStack.Collectible;
            FoodNutritionProperties stackProps;

            if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
            {
                stackProps = obj.CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible.GetNutritionProperties(world, obj.CombustibleProps.SmeltedStack.ResolvedItemstack, forEntity);
            }
            else
            {
                stackProps = obj.GetNutritionProperties(world, contentStack, forEntity);
            }

            if (obj.Attributes?["nutritionPropsWhenInMeal"].Exists == true)
            {
                stackProps = obj.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
            }
            if (obj.Attributes?["nutritionPropsWhenInPie"].Exists == true && mulWithStacksize)
            {
                stackProps = obj.Attributes?["nutritionPropsWhenInPie"].AsObject<FoodNutritionProperties>();
            }
            float satLossMul = 1.0f;
            float healthLoss = 1.0f;
            float mul = mulWithStacksize ? contentStack.StackSize : 1;
            if (BlockLiquidContainerBase.GetContainableProps(contentStack) != null && mulWithStacksize)
            {
                mul /= 10;
            }
            if (obj is ItemExpandedRawFood && (contentStack.Attributes["expandedSats"] as FloatArrayAttribute)?.value?.Length == 6)
            {
                FoodNutritionProperties[] exProps = (obj as ItemExpandedRawFood).GetPropsFromArray((contentStack.Attributes["expandedSats"] as FloatArrayAttribute).value);

                if (exProps != null || exProps.Length > 0)
                {
                    foreach (FoodNutritionProperties exProp in exProps)
                    {
                        exProp.Satiety *= satLossMul * nutritionMul * (obj is ItemExpandedLiquid ? contentStack.StackSize / 10 : 1 * mul);
                        exProp.Health *= healthLoss * healthMul * (obj is ItemExpandedLiquid ? contentStack.StackSize / 10 : 1 * mul);

                        foodProps.Add(exProp);
                    }
                }
                if (stackProps != null)
                {
                    FoodNutritionProperties props = stackProps.Clone();
                    props.Satiety *= satLossMul * nutritionMul * mul;
                    props.Health *= healthLoss * healthMul * mul;
                    foodProps.Add(props);
                }
            }
            else if (stackProps != null)
            {
                FoodNutritionProperties props = stackProps.Clone();

                DummySlot slot = new DummySlot(contentStack, inSlot.Inventory);
                TransitionState state = contentStack.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);
                props.Satiety *= satLossMul * nutritionMul * mul;
                props.Health *= healthLoss * healthMul * mul;
                foodProps.Add(props);
            }
            return foodProps.ToArray();
        }

        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack itemstack = inslot.Itemstack;

            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot.Itemstack, null);

            if (itemstack == null || propsm == null || propsm.Length == 0)
            {
                return null;
            }

            if (itemstack.Attributes == null)
            {
                itemstack.Attributes = new TreeAttribute();
            }

            if (!(itemstack.Attributes["transitionstate"] is ITreeAttribute))
            {
                itemstack.Attributes["transitionstate"] = new TreeAttribute();
            }

            ITreeAttribute attr = (ITreeAttribute)itemstack.Attributes["transitionstate"];

            //TransitionableProperties[] props = itemstack.Collectible.TransitionableProps; - WTF is this here for? we already have props

            float[] transitionedHours;
            float[] freshHours;
            float[] transitionHours;
            TransitionState[] states = new TransitionState[propsm.Length];

            if (!attr.HasAttribute("createdTotalHours"))
            {
                attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);

                freshHours = new float[propsm.Length];
                transitionHours = new float[propsm.Length];
                transitionedHours = new float[propsm.Length];

                for (int i = 0; i < propsm.Length; i++)
                {
                    transitionedHours[i] = 0;
                    if (propsm[i] != null)
                    {
                        freshHours[i] = propsm[i].FreshHours.nextFloat(1, world.Rand);
                        transitionHours[i] = propsm[i].TransitionHours.nextFloat(1, world.Rand);
                    }
                    else
                    {
                        freshHours[i] = 0;
                        transitionHours[i] = 0;
                    }
                }

                attr["freshHours"] = new FloatArrayAttribute(freshHours);
                attr["transitionHours"] = new FloatArrayAttribute(transitionHours);
                attr["transitionedHours"] = new FloatArrayAttribute(transitionedHours);
            }
            else
            {
                freshHours = (attr["freshHours"] as FloatArrayAttribute).value;
                transitionHours = (attr["transitionHours"] as FloatArrayAttribute).value;
                transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute).value;
            }

            double lastUpdatedTotalHours = attr.GetDouble("lastUpdatedTotalHours");
            double nowTotalHours = world.Calendar.TotalHours;


            bool nowSpoiling = false;

            float hoursPassed = (float)(nowTotalHours - lastUpdatedTotalHours);

            for (int i = 0; i < propsm.Length; i++)
            {
                TransitionableProperties prop = propsm[i];
                if (prop == null || i >= freshHours.Length)
                    continue;

                float transitionRateMul = GetTransitionRateMul(world, inslot, prop.Type);

                if (hoursPassed > 0.05f) // Maybe prevents us from running into accumulating rounding errors?
                {
                    float hoursPassedAdjusted = hoursPassed * transitionRateMul;
                    transitionedHours[i] += hoursPassedAdjusted;

                    /*if (api.World.Side == EnumAppSide.Server && inslot.Inventory.ClassName == "chest")
                    {
                        Console.WriteLine(hoursPassed + " hours passed. " + inslot.Itemstack.Collectible.Code + " spoil by " + transitionRateMul + "x. Is inside " + inslot.Inventory.ClassName + " {0}/{1}", transitionedHours[i], freshHours[i]);
                    }*/
                }

                float freshHoursLeft = Math.Max(0, freshHours[i] - transitionedHours[i]);
                float transitionLevel = Math.Max(0, transitionedHours[i] - freshHours[i]) / transitionHours[i];

                // Don't continue transitioning spoiled foods
                if (transitionLevel > 0)
                {
                    if (prop.Type == EnumTransitionType.Perish)
                    {
                        nowSpoiling = true;
                    }
                    else
                    {
                        if (nowSpoiling)
                            continue;
                    }
                }

                if (transitionLevel >= 1 && world.Side == EnumAppSide.Server)
                {
                    ItemStack newstack = OnTransitionNow(inslot, itemstack.Collectible.TransitionableProps[i]);

                    if (newstack.StackSize <= 0)
                    {
                        inslot.Itemstack = null;
                    }
                    else
                    {
                        itemstack.SetFrom(newstack);
                    }

                    inslot.MarkDirty();

                    // Only do one transformation, then do the next one next update
                    // This does fully not respect time-fast-forward, so that should be fixed some day
                    break;
                }

                states[i] = new TransitionState()
                {
                    FreshHoursLeft = freshHoursLeft,
                    TransitionLevel = Math.Min(1, transitionLevel),
                    TransitionedHours = transitionedHours[i],
                    TransitionHours = transitionHours[i],
                    FreshHours = freshHours[i],
                    Props = prop
                };

                //if (transitionRateMul > 0) break; // Only do one transformation at the time (i.e. food can not cure and perish at the same time) - Tyron 9/oct 2020, but why not at the same time? We need it for cheese ripening
            }

            if (hoursPassed > 0.05f)
            {
                attr.SetDouble("lastUpdatedTotalHours", nowTotalHours);
            }

            return states.Where(s => s != null).OrderBy(s => (int)s.Props.Type).ToArray();
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            TransitionState[] states = UpdateAndGetTransitionStates(world, inslot);
            TransitionableProperties[] propsm = GetTransitionableProperties(world, inslot?.Itemstack, null);
            if (propsm == null)
                return null;

            for (int i = 0; i < propsm.Length; i++)
            {
                if (i >= states.Length)
                    break;
                if (propsm[i]?.Type == type)
                    return states[i];
            }

            return null;
        }

        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            string[] ings = (oldStack?.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] sats = (oldStack?.Attributes["expandedSats"] as FloatArrayAttribute)?.value;
            if (ings != null) newStack.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (sats != null) newStack.Attributes["expandedSats"] = new FloatArrayAttribute(sats);
        }
        public void OnCreatedByGrinding(ItemStack input, ItemStack output)
        {
            string[] ings = (input?.Attributes["madeWith"] as StringArrayAttribute)?.value;
            float[] sats = (input?.Attributes["expandedSats"] as FloatArrayAttribute)?.value;
            if (ings != null) output.Attributes["madeWith"] = new StringArrayAttribute(ings);
            if (sats != null) output.Attributes["expandedSats"] = new FloatArrayAttribute(Array.ConvertAll(sats, i => i / output.StackSize));
        }
    }

    public interface IExpandedFood
    {
        void OnCreatedByKneading(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input, ItemStack output);
        void OnCreatedByGrinding(ItemStack input, ItemStack output);
    }

    public enum EnumNutritionMatch
    {
        Hp,
        Fruit,
        Grain,
        Vegetable,
        Protein,
        Dairy
    }



    /*************************************************************************************************************/
    public class EFTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private readonly ICoreClientAPI capi;
        private TextureAtlasPosition contentTextPos;
        private readonly CompositeTexture contentTexture;

        public EFTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (contentTextPos == null)
                {
                    int textureSubId;
                    textureSubId = ObjectCacheUtil.GetOrCreate(capi, "efcontenttexture-" + contentTexture.ToString(), () =>
                    {
                        var id = 0;
                        var bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);

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
    }
}