using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{

    [HarmonyPatch(typeof(GuiHandbookItemStackPage), nameof(GuiHandbookItemStackPage.PageCodeForStack))]
    public static class ExpandedFoodPageCodePatch
    {
        public static void Postfix(ref string __result, ItemStack stack)
        {
            if (stack.Collectible is ItemExpandedRawFood)
            {
                __result = stack.Class.Name() + "-" + stack.Collectible.Code.ToShortString();
            }
        }
    }

    [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), nameof(CollectibleBehaviorHandbookTextAndExtraInfo.GetHandbookInfo))]
    public static class GetHandbookInfoPatch
    {
        public static void Postfix(ref RichTextComponentBase[] __result, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<RichTextComponentBase> list = [.. __result];

            list.addMixingIngredientForInfo(inSlot, capi, allStacks, openDetailPageFor);
            list.addCreatedByMixingInfo(inSlot, capi, allStacks, openDetailPageFor);
            list.addSimmerIngredientForInfo(inSlot, capi, allStacks, openDetailPageFor);
            list.addCreatedBySimmeringInfo(inSlot, capi, allStacks, openDetailPageFor);
            __result = [.. list];
        }
    }

    [HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), "addIngredientForInfo")]
    public static class GetHandbookIngredientForPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            List<CodeInstruction> codes = [.. instructions];

            for (int i = 0; i < codes.Count; i++)
            {
                if (!codes[i].operand?.ToString()?.Equals("System.Collections.Generic.List`1[Vintagestory.GameContent.CookingRecipe] GetCookingRecipes(Vintagestory.API.Common.ICoreAPI)") ?? true)
                {
                    continue;
                }

                //codes[i] = new(OpCodes.Call, AccessTools.Method(typeof(ApiAdditions), nameof(ApiAdditions.GetMixingRecipes), [typeof(ICoreAPI)]));
                break;
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(ModSystemSurvivalHandbook), "onCreatePagesAsync")]
    public static class ModSystemSurvivalHandbookPatch
    {
        public static void Postfix(ref List<GuiHandbookPage> __result, ModSystemSurvivalHandbook __instance, ref ICoreClientAPI ___capi, ref ItemStack[] ___allstacks)
        {
            foreach (var recipe in ___capi.GetMixingRecipes())
            {
                if (___capi.IsShuttingDown) break;
                if (recipe.CooksInto == null)
                {
                    GuiHandbookMealRecipePage elem = new GuiHandbookMealRecipePage(___capi, recipe, ___allstacks, 6)
                    {
                        Visible = true
                    };


                    __result.Add(elem);
                }
            }
        }
    }

    [HarmonyPatch(typeof(InventorySmelting))]
    public class SmeltingInvPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetOutputText")]
        public static void displayFix(ref string __result, InventorySmelting __instance)
        {
            if (__instance[1].Itemstack?.Collectible is BlockSaucepan)
            {
                __result = (__instance[1].Itemstack.Collectible as BlockSaucepan).GetOutputText(__instance.Api.World, __instance);
            }
        }


        /// <summary>
        /// Turns the
        /// <code>
        ///     ...
        ///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockCookingContainer))
        ///	    {
        ///	        ...
        ///	    }  
        ///	    ...
        /// </code>
        /// block
        /// into
        /// <code>
        ///     ...
        ///	    if (targetSlot == this.slots[1] && (stack.Collectible is BlockSmeltingContainer || stack.Collectible is BlockSaucePan || stack.Collectible is BlockCookingContainer))
        ///	    {
        ///	        ...
        ///	    }  
        ///	    ...
        /// </code>
        /// to make saucepans/cauldrons prefer a firepit's input slot.
        /// </summary>
        /// 

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySmelting), nameof(InventorySmelting.GetSuitability))]
        public static bool Harmony_InventorySmelting_GetSuitability_Prefix(
            ItemSlot sourceSlot, ItemSlot targetSlot, ItemSlot[] ___slots, ref float __result)
        {
            var stack = sourceSlot.Itemstack;
            if (targetSlot == ___slots[1] && stack.Collectible is BlockSaucepan)
            {
                __result = 2.2f;
                return false;
            }
            return true;
        }
        // Thanks Apache!!!
    }

    [HarmonyPatch(typeof(CookingRecipe))]
    public class CookingRecipePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetOutputName")]
        public static bool recipeNameFix(IWorldAccessor worldForResolve, ItemStack[] inputStacks, ref string __result, CookingRecipe __instance)
        {
            bool rotten = inputStacks.Any((stack) => stack?.Collectible.Code.Path == "rot");
            if (rotten)
            {
                __result = Lang.Get("Rotten Food");
                return false;
            }

            if (CookingRecipe.NamingRegistry.TryGetValue(__instance.Code!, out ICookingRecipeNamingHelper? namer))
            {
                __result = namer.GetNameForIngredients(worldForResolve, __instance.Code!, inputStacks);
                return false;
            }

            __result = new acaRecipeNames().GetNameForIngredients(worldForResolve, __instance.Code, inputStacks);
            return false;
        }
    }

    [HarmonyPatch(typeof(CookingRecipeIngredient))]
    public class CookingIngredientPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("GetMatchingStack")]
        public static bool displayFix(ItemStack inputStack, ref CookingRecipeStack __result, CookingRecipeIngredient __instance)
        {
            if (inputStack == null)
            { 
                __result = null;
                return false;
            }

            string[] ignoredStackAttributes = [.. GlobalConstants.IgnoredStackAttributes, "madeWith", "expandedSats", "timeFrozen"];
            for (int i = 0; i < __instance.ValidStacks.Length; i++)
            {
                bool isWildCard = __instance.ValidStacks[i].Code.Path.Contains("*");
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(__instance.ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, ignoredStackAttributes))
                    || (__instance.ValidStacks[i].CookedStack?.ResolvedItemstack is ItemStack cookedStack && inputStack.Equals(__instance.world, cookedStack, ignoredStackAttributes))
                ;

                if (found)
                {
                    __result = __instance.ValidStacks[i];
                    return false;
                }
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockCookedContainerBase))]
    public class BlockMealContainerBasePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        public static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMealRecipe")]
        public static void mealFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }
    }

    [HarmonyPatch(typeof(BlockMeal))]
    public class BlockMealBowlBasePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        public static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            __result ??= world.Api.GetMixingRecipes().FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }


        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionProperties", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        public static bool nutriFix(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref FoodNutritionProperties[] __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            List<FoodNutritionProperties> props = new List<FoodNutritionProperties>();

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;
                props.AddRange(ItemExpandedRawFood.GetExpandedContentNutritionProperties(
                                                                                            world,
                                                                                            inSlot,
                                                                                            contentStacks[i],
                                                                                            forEntity,
                                                                                            mulWithStacksize,
                                                                                            nutritionMul,
                                                                                            healthMul
                                                                                            ));
            }

            __result = [.. props];
            return false;
        }


        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionFacts", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float))]
        public static bool nutriFactsFix(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref string __result, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            FoodNutritionProperties[] props;

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;
            float satLossMul = 1;
            float healthLossMul = 1;

            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null)
                    continue;
                DummySlot slot = new DummySlot(contentStacks[i], inSlotorFirstSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                props = ItemExpandedRawFood.GetExpandedContentNutritionProperties(world, inSlotorFirstSlot, contentStacks[i], forEntity, mulWithStacksize, nutritionMul, healthMul);
                for (int j = 0; j < props.Length; j++)
                {
                    FoodNutritionProperties prop = props[j];
                    if (prop == null)
                        continue;
                    float sat = 0;
                    totalSaturation.TryGetValue(prop.FoodCategory, out sat);
                    totalHealth += prop.Health * healthLossMul;
                    totalSaturation[prop.FoodCategory] = sat + prop.Satiety * satLossMul;
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Nutrition Facts"));

            foreach (var val in totalSaturation)
            {
                sb.AppendLine("- " + Lang.Get("" + val.Key) + ": " + Math.Round(val.Value) + " sat.");
            }
            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            __result = sb.ToString();
            return false;
        }
    }


    [HarmonyPatch(typeof(BlockEntityQuern))]
    public class BlockEntityQuernPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("grindInput")]
        public static bool grindInputWIthInheritedAttributes(ref int ___nowOutputFace, BlockEntityQuern __instance)
        {

            ItemStack grindedStack = __instance.InputGrindProps.GroundStack.ResolvedItemstack.Clone();
            IExpandedFood food;
            if ((food = grindedStack.Collectible as IExpandedFood) != null)
            {
                food.OnCreatedByGrinding(__instance.InputStack, grindedStack);
            }
            else
            {
                return true;
            }
            if (__instance.OutputSlot.Itemstack == null)
            {
                __instance.OutputSlot.Itemstack = grindedStack;
            }
            else
            {
                int mergableQuantity = __instance.OutputSlot.Itemstack.Collectible.GetMergableQuantity(__instance.OutputSlot.Itemstack, grindedStack, EnumMergePriority.AutoMerge);

                if (mergableQuantity > 0)
                {
                    __instance.OutputSlot.Itemstack.StackSize += grindedStack.StackSize;
                }
                else
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[___nowOutputFace];
                    ___nowOutputFace = (___nowOutputFace + 1) % 4;

                    Block block = __instance.Api.World.BlockAccessor.GetBlock(__instance.Pos.AddCopy(face));
                    if (block.Replaceable < 6000) return false;
                    __instance.Api.World.SpawnItemEntity(grindedStack, __instance.Pos.ToVec3d().Add(0.5 + face.Normalf.X * 0.7, 0.75, 0.5 + face.Normalf.Z * 0.7), new Vec3d(face.Normalf.X * 0.02f, 0, face.Normalf.Z * 0.02f));
                }
            }

            __instance.InputSlot.TakeOut(1);
            __instance.InputSlot.MarkDirty();
            __instance.OutputSlot.MarkDirty();
            return false;
        }
    }
    [HarmonyPatch(typeof(BlockEntityPie))]
    public class BlockEntityPiePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("TryAddIngredientFrom")]
        public static bool mulitPie(ref bool __result, ref InventoryBase ___inv, BlockEntityPie __instance, ItemSlot slot, IPlayer byPlayer = null)
        {
            ICoreClientAPI capi = __instance.Api as ICoreClientAPI;
            ILiquidSource container = slot.Itemstack.Collectible as ILiquidSource;
            ItemStack contentStack = container?.GetContent(slot.Itemstack) ?? slot.Itemstack;
            InPieProperties pieProps = contentStack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>(null, contentStack.Collectible.Code.Domain);

            if (pieProps == null)
            {
                if (byPlayer != null) capi?.TriggerIngameError(__instance, "notpieable", Lang.Get("This item can not be added to pies"));
                __result = false;
                return false;
            }

            float totalPortions = contentStack.StackSize / (container != null ? 2 : 20);
            if (totalPortions < 1)
            {
                if (byPlayer != null) capi?.TriggerIngameError(__instance, "notpieable", Lang.Get(container != null ? "Need at least 2 items each" : "Need at least 0.2L liquid"));
                __result = false;
                return false;
            }

            if (___inv[0].Itemstack.Block is not BlockPie pieBlock)
            {
                __result = false;
                return false;
            }

            ItemStack[] cStacks = pieBlock.GetContents(__instance.Api.World, ___inv[0].Itemstack);

            bool isFull = cStacks[1] != null && cStacks[2] != null && cStacks[3] != null && cStacks[4] != null;
            bool hasFilling = cStacks[1] != null || cStacks[2] != null || cStacks[3] != null || cStacks[4] != null;

            if (isFull)
            {
                if (pieProps.PartType == EnumPiePartType.Crust)
                {
                    if (cStacks[5] == null)
                    {
                        cStacks[5] = slot.TakeOut(2);
                        pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                    }
                    else
                    {
                        ItemStack stack = ___inv[0].Itemstack;
                        stack = BlockPie.CycleTopCrustType(stack);
                    }
                    __result = true;
                    return false;
                }
                if (byPlayer != null) capi?.TriggerIngameError(__instance, "piefullfilling", Lang.Get("Can't add more filling - already completely filled pie"));
                __result = false;
                return false;
            }

            if (pieProps.PartType != EnumPiePartType.Filling)
            {
                if (byPlayer != null) capi?.TriggerIngameError(__instance, "pieneedsfilling", Lang.Get("Need to add a filling next"));
                __result = false;
                return false;
            }


            if (!hasFilling)
            {

                cStacks[1] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
                pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            var foodCats = cStacks.Select(BlockPie.FillingFoodCategory).ToArray();
            var stackprops = cStacks.Select(stack => stack?.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain)).ToArray();

            ItemStack cstack = contentStack;
            EnumFoodCategory foodCat = BlockPie.FillingFoodCategory(contentStack);

            bool equal = true;
            bool foodCatEquals = true;

            for (int i = 1; equal && i < cStacks.Length - 1; i++)
            {
                if (cstack == null) continue;

                equal &= cStacks[i] == null || cstack.Equals(__instance.Api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;

                cstack = cStacks[i];
                foodCat = foodCats[i];
            }

            int emptySlotIndex = 2 + (cStacks[2] != null ? 1 + (cStacks[3] != null ? 1 : 0) : 0);

            if (equal)
            {
                cStacks[emptySlotIndex] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
                pieBlock.SetContents(___inv[0].Itemstack, cStacks);
                __result = true;
                return false;
            }

            if (!stackprops[1].AllowMixing)
            {
                if (byPlayer != null) capi?.TriggerIngameError(__instance, "piefullfilling", Lang.Get("You really want to mix these to ingredients?! That would taste horrible!"));
                __result = false;
                return false;
            }

            cStacks[emptySlotIndex] = container != null ? container.TryTakeContent(slot.Itemstack, 20) : slot.TakeOut(2);
            pieBlock.SetContents(___inv[0].Itemstack, cStacks);
            __result = true;
            return false;
        }
    }
}