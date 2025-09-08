using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ACulinaryArtillery
{
    public class acaRecipeNames : VanillaCookingRecipeNames, ICookingRecipeNamingHelper
    {
        public new string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            Vintagestory.API.Datastructures.OrderedDictionary<ItemStack, int> quantitiesByStack = new();
            quantitiesByStack = mergeStacks(worldForResolve, stacks);

            CookingRecipe recipe = worldForResolve.Api.GetCookingRecipe(recipeCode) ?? worldForResolve.Api.GetMixingRecipes().FirstOrDefault((CookingRecipe rec) => recipeCode == rec.Code);

            if (recipeCode == null || recipe == null || quantitiesByStack.Count == 0) return Lang.Get("unknown");

            return GetNameForMergedIngredients(worldForResolve, recipe, quantitiesByStack);
        }

        protected override string GetNameForMergedIngredients(IWorldAccessor worldForResolve, CookingRecipe recipe, OrderedDictionary<ItemStack, int> quantitiesByStack)
        {
            string recipeCode = recipe.Code!;

            switch (recipeCode)
            {
                /*case "soup":
                    {
                        List<string> BoiledIngredientNames = [];
                        List<string> StewedIngredientNames = [];
                        CookingRecipeIngredient? ingred = null;
                        ItemStack? stockStack = null;
                        ItemStack? creamStack = null;
                        ItemStack? mainStack = null;
                        string itemName = string.Empty;
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            if (val.Key.Collectible.Code.Path.Contains("waterportion")) continue;

                            ItemStack? stack = val.Key;
                            ingred = recipe.GetIngrendientFor(stack);
                            if (ingred?.Code == "cream")
                            {
                                creamStack = stack;
                                continue;
                            }
                            else if (ingred?.Code == "stock")
                            {
                                stockStack = stack;
                                continue;
                            }
                            else if (max < val.Value)
                            {
                                max = val.Value;
                                stack = mainStack;
                                mainStack = val.Key;
                            }

                            if (stack == null) continue;

                            itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable ||
                                stack.Collectible.FirstCodePart().Contains("egg"))
                            {
                                if (!BoiledIngredientNames.Contains(itemName)) BoiledIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!StewedIngredientNames.Contains(itemName)) StewedIngredientNames.Add(itemName);
                            }
                        }

                        List<string> MainIngredientNames = [];
                        string MainIngredientFormat = "{0}";

                        if (creamStack != null)
                        {
                            if (stockStack != null) itemName = getMainIngredientName(stockStack, "soup");
                            else if (mainStack != null)
                            {
                                itemName = getMainIngredientName(mainStack, "soup");
                            }
                            MainIngredientNames.Add(itemName);
                            MainIngredientNames.Add(getMainIngredientName(creamStack, "soup", true));
                            MainIngredientFormat = "meal-soup-in-cream-format";
                        }
                        else if (stockStack != null)
                        {
                            if (mainStack != null)
                            {
                                itemName = getMainIngredientName(mainStack, "soup");
                            }
                            MainIngredientNames.Add(itemName);
                            MainIngredientNames.Add(getMainIngredientName(stockStack, "soup", true));
                            MainIngredientFormat = "meal-soup-in-stock-format";
                        }
                        else if (mainStack != null)
                        {
                            MainIngredientNames.Add(getMainIngredientName(mainStack, "soup"));
                        }

                        string ExtraIngredientsFormat = "meal-adds-soup-boiled";
                        if (StewedIngredientNames.Count > 0)
                        {
                            if (BoiledIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-soup-boiled-and-stewed";
                            else ExtraIngredientsFormat = "meal-adds-soup-stewed";
                        }

                        string MealFormat = getMaxMealFormat("meal", "soup", max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, BoiledIngredientNames, StewedIngredientNames));
                        return MealFormat.Trim().UcFirst();
                    }

                case "porridge":
                    {
                        string MealFormat = "meal";
                        List<string> MainIngredientNames = [];
                        List<string> MashedIngredientNames = [];
                        List<string> FreshIngredientNames = [];
                        string ToppingName = string.Empty;
                        string itemName = string.Empty;
                        int typesOfGrain = quantitiesByStack.Where(val => recipe.GetIngrendientFor(val.Key)?.Code == "grain-base").Count();
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(val.Key);
                            if (ingred?.Code == "topping")
                            {
                                ToppingName = ingredientName(val.Key, EnumIngredientNameType.Topping);
                                continue;
                            }

                            if (ingred?.Code == "grain-base")
                            {
                                if (typesOfGrain < 3)
                                {
                                    if (MainIngredientNames.Count < 2)
                                    {
                                        itemName = getMainIngredientName(val.Key, recipeCode, MainIngredientNames.Count > 0);
                                        if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                    }
                                }
                                else
                                {
                                    itemName = ingredientName(val.Key);
                                    if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                }

                                max += val.Value;
                                continue;
                            }

                            itemName = ingredientName(val.Key, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, val.Key, ingred) == EnumFoodCategory.Vegetable)
                            {
                                if (!MashedIngredientNames.Contains(itemName)) MashedIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!FreshIngredientNames.Contains(itemName)) FreshIngredientNames.Add(itemName);
                            }
                        }

                        string ExtraIngredientsFormat = "meal-adds-porridge-mashed";
                        if (FreshIngredientNames.Count > 0)
                        {
                            if (MashedIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-porridge-mashed-and-fresh";
                            else ExtraIngredientsFormat = "meal-adds-porridge-fresh";
                        }

                        string MainIngredientFormat = "{0}";
                        if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";
                        MealFormat = getMaxMealFormat(MealFormat, recipeCode, max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, MashedIngredientNames, FreshIngredientNames));
                        if (ToppingName != string.Empty) MealFormat = Lang.Get("meal-topping-ingredient-format", ToppingName, MealFormat);
                        return MealFormat.Trim().UcFirst();
                    }

                case "meatystew":
                case "vegetablestew":
                    {
                        ItemStack[] requiredStacks = new ItemStack[quantitiesByStack.Count];
                        int vegetableCount = 0;
                        int proteinCount = 0;

                        foreach (var ingred in recipe.Ingredients!)
                        {
                            if (ingred.Code.Contains("base"))
                            {
                                for (int i = 0; i < quantitiesByStack.Count; i++)
                                {
                                    var stack = quantitiesByStack.GetKeyAtIndex(i);
                                    if (!ingred.Matches(stack)) continue;
                                    if (requiredStacks.Contains(stack)) continue;

                                    requiredStacks[i] = stack;
                                    if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable) vegetableCount++;
                                    if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Protein) proteinCount++;
                                }
                            }
                        }

                        List<string> MainIngredientNames = [];
                        List<string> BoiledIngredientNames = [];
                        List<string> StewedIngredientNames = [];
                        string ToppingName = string.Empty;
                        string itemName = string.Empty;
                        EnumFoodCategory primaryCategory = EnumFoodCategory.Protein;
                        int max = 0;

                        if (vegetableCount > proteinCount) primaryCategory = EnumFoodCategory.Vegetable;
                        for (int i = 0; i < quantitiesByStack.Count; i++)
                        {
                            var stack = quantitiesByStack.GetKeyAtIndex(i);
                            int quantity = quantitiesByStack.GetValueAtIndex(i);

                            CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(stack);
                            if (ingred?.Code == "topping")
                            {
                                ToppingName = ingredientName(stack, EnumIngredientNameType.Topping);
                                continue;
                            }

                            var cat = getFoodCat(worldForResolve, requiredStacks[i], ingred);
                            if ((cat is EnumFoodCategory.Vegetable or EnumFoodCategory.Protein && quantitiesByStack.Count <= 2) || cat == primaryCategory)
                            {
                                max += quantity;

                                if (MainIngredientNames.Count < 2)
                                {
                                    itemName = getMainIngredientName(stack, "stew", MainIngredientNames.Count > 0);
                                    if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                    continue;
                                }
                            }

                            itemName = ingredientName(stack, EnumIngredientNameType.InsturmentalCase);
                            if (getFoodCat(worldForResolve, stack, ingred) == EnumFoodCategory.Vegetable ||
                                stack.Collectible.FirstCodePart().Contains("egg"))
                            {
                                if (!BoiledIngredientNames.Contains(itemName)) BoiledIngredientNames.Add(itemName);
                            }
                            else
                            {
                                if (!StewedIngredientNames.Contains(itemName)) StewedIngredientNames.Add(itemName);
                            }
                        }

                        string ExtraIngredientsFormat = "meal-adds-stew-boiled";
                        if (StewedIngredientNames.Count > 0)
                        {
                            if (BoiledIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-stew-boiled-and-stewed";
                            else ExtraIngredientsFormat = "meal-adds-stew-stewed";
                        }

                        string MainIngredientFormat = "{0}";
                        if (MainIngredientNames.Count == 2) MainIngredientFormat = "multi-main-ingredients-format";
                        string MealFormat = getMaxMealFormat("meal", "stew", max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, MainIngredientFormat), getMealAddsString(ExtraIngredientsFormat, BoiledIngredientNames, StewedIngredientNames));
                        if (ToppingName != string.Empty) MealFormat = Lang.Get("meal-topping-ingredient-format", ToppingName, MealFormat);
                        return MealFormat.Trim().UcFirst();
                    }

                case "scrambledeggs":
                    {
                        List<string> MainIngredientNames = [];
                        List<string> FreshIngredientNames = [];
                        List<string> MeltedIngredientNames = [];
                        string itemName = string.Empty;
                        int max = 0;

                        foreach (var val in quantitiesByStack)
                        {
                            if (recipe.GetIngrendientFor(val.Key)?.Code == "egg-base")
                            {
                                itemName = getMainIngredientName(val.Key, recipeCode);
                                if (!MainIngredientNames.Contains(itemName)) MainIngredientNames.Add(itemName);
                                max += val.Value;
                                continue;
                            }

                            itemName = ingredientName(val.Key, EnumIngredientNameType.InsturmentalCase);

                            if (val.Key.Collectible.FirstCodePart() == "cheese")
                            {
                                if (!MeltedIngredientNames.Contains(itemName)) MeltedIngredientNames.Add(itemName);
                                continue;
                            }

                            if (!FreshIngredientNames.Contains(itemName)) FreshIngredientNames.Add(itemName);
                        }

                        string ExtraIngredientsFormat = "meal-adds-scrambledeggs-fresh";
                        if (MeltedIngredientNames.Count > 0)
                        {
                            if (FreshIngredientNames.Count > 0) ExtraIngredientsFormat = "meal-adds-scrambledeggs-melted-and-fresh";
                            else ExtraIngredientsFormat = "meal-adds-scrambledeggs-melted";
                        }

                        string MealFormat = getMaxMealFormat("meal", recipeCode, max);
                        MealFormat = Lang.Get(MealFormat, getMainIngredientsString(MainIngredientNames, "{0}"), getMealAddsString(ExtraIngredientsFormat, MeltedIngredientNames, FreshIngredientNames));
                        return MealFormat.Trim().UcFirst();
                    }*/

                default:
                    return base.GetNameForMergedIngredients(worldForResolve, recipe, quantitiesByStack);
            }
        }
    }

    public class DoughRecipe : IByteSerializable
    {
        public string Code = "something";
        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public DoughIngredient[] Ingredients;

        public JsonItemStack Output;

        public ItemStack TryCraftNow(ICoreAPI api, ItemSlot[] inputslots)
        {
            var matched = pairInput(inputslots);
            
            ItemStack mixedStack = Output.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize <= 0) return null;

            IExpandedFood food;
            if ((food = mixedStack.Collectible as IExpandedFood) != null) food.OnCreatedByKneading(matched, mixedStack);

            foreach (var val in matched)
            {
                val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Output.StackSize));
                val.Key.MarkDirty();
            }
            
            return mixedStack;
        }

        public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
        {
            int outputStackSize = 0;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = pairInput(inputSlots);
            if (matched == null) return false;

            outputStackSize = getOutputSize(matched);

            return outputStackSize >= 0;
        }

        List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            List<int> alreadyFound = new List<int>();

            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Enqueue(val);

            if (inputSlotsList.Count != Ingredients.Length) return null;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);

                    if (ingred != null && !alreadyFound.Contains(i))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                        alreadyFound.Add(i);
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            // We're missing ingredients
            if (matched.Count != Ingredients.Length)
            {
                return null;
            }

            return matched;
        }


        int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;
                int posChange = inputSlot.StackSize / ingred.Quantity;

                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }

            if (outQuantityMul == -1)
            {
                return -1;
            }


            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;


                // Must have same or more than the total crafted amount
                if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;

            }

            outQuantityMul = 1;
            return Output.StackSize * outQuantityMul;
        }

        public string GetOutputName()
        {
            return Lang.Get("aculinaryartillery:Will make {0}", Output.ResolvedItemstack.GetName());
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            ok &= Output.Resolve(world, sourceForErrorLogging);


            return ok;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new DoughIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new DoughIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Dough Recipe (FromBytes)");
            }

            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "Dough Recipe (FromBytes)");
        }

        public DoughRecipe Clone()
        {
            DoughIngredient[] ingredients = new DoughIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new DoughRecipe()
            {
                Output = Output.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                Ingredients = ingredients
            };
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingreds in Ingredients)
            {
                if (ingreds.Inputs.Length <= 0) continue;
                CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    for (int i = 0; i < world.Blocks.Count; i++)
                    {
                        if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                        {
                            string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < world.Items.Count; i++)
                    {
                        if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                        {
                            string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name] = codes.ToArray();
            }

            return mappings;
        }
    }
    public class SimmerRecipe : IByteSerializable
    {
        public string Code = "something";
        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public CraftingRecipeIngredient[] Ingredients;

        public CombustibleProperties Simmering;

        public ItemStack TryCraftNow(ICoreAPI api, ItemSlot[] inputslots)
        {
            var matched = pairInput(inputslots);

            ItemStack mixedStack = Simmering.SmeltedStack.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize <= 0) return null;

            foreach (var val in matched)
            {
                val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Simmering.SmeltedStack.StackSize));
                val.Key.MarkDirty();
            }

            return mixedStack;
        }

        public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
        {
            var matched = pairInput(inputSlots);
            if (matched == null) return false;

            return getOutputSize(matched) >= 0;
        }

        /// <summary>
        /// Match a list of ingredients against the recipe and give back the amount that can be made with what's given
        /// Will return 0 if the ingredients are NOT in the right proportions!
        /// </summary>
        /// <param name="Inputs">a list of item stacks</param>
        /// <returns>the amount of the recipe that can be made</returns>
        public int Match(List<ItemStack> Inputs)
        {
            if (Inputs.Count != Ingredients.Length) return 0; //not the correct amount of ingredients for that recipe

            var matched = new List<CraftingRecipeIngredient>();
            int amountForTheRecipe = -1;

            foreach (ItemStack input in Inputs)
            {
                // First check if we have a matching ingredient, and whether we've already matched that ingredient before
                var match = Ingredients.FirstOrDefault(ing => (ing.ResolvedItemstack != null || ing.IsWildCard) && matched.Contains(ing) && ing.SatisfiesAsIngredient(input));

                if (match == null) return 0; // didn't find a match for the input in previous step
                if (input.StackSize % match.Quantity != 0) return 0; //this particular ingredient is not in enough quantity for full portions
                if (input.StackSize / match.Quantity % Simmering.SmeltedRatio != 0) return 0; //same but taking the smeltedRatio into account ? would love to see an example where that's needed

                int amountForThisIngredient = input.StackSize / match.Quantity / Simmering.SmeltedRatio;

                if (amountForThisIngredient > 0)    //the ingredient can at least produce a portion
                {
                    if (amountForTheRecipe == -1) amountForTheRecipe = amountForThisIngredient;   // on the first match we set the target amount of portions

                    if (amountForThisIngredient != amountForTheRecipe) return 0;   //we only want perfectly proportioned ingredients
                    else matched.Add(match); //this ingredient matches the target amount, add it 
                }
                else return 0;      //we need at least a full portion!
            }

            return amountForTheRecipe;
        }

        List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            var inputSlotsList = new Queue<ItemSlot>(inputStacks.Where(val => !val.Empty));

            if (inputSlotsList.Count != Ingredients.Length) return null;

            var alreadyFound = new HashSet<int>();
            var matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    if (Ingredients[i].SatisfiesAsIngredient(inputSlot.Itemstack) && !alreadyFound.Contains(i))
                    {
                        matched.Add(new(inputSlot, Ingredients[i]));
                        alreadyFound.Add(i);
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }
            
            if (matched.Count != Ingredients.Length) return null; // We're missing ingredients

            return matched;
        }

        int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                int posChange = val.Key.StackSize / val.Value.Quantity;

                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }

            if (outQuantityMul == -1) return -1;

            foreach (var val in matched)
            {
                // Must have same or more than the total crafted amount
                if (val.Key.StackSize < val.Value.Quantity * outQuantityMul) return -1;
            }

            return Simmering.SmeltedStack.StackSize;
        }

        public string GetOutputName()
        {
            return Lang.Get("aculinaryartillery:Will make {0}", Simmering.SmeltedStack.ResolvedItemstack.GetName());
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }

            ok &= Simmering.SmeltedStack.Resolve(world, sourceForErrorLogging);

            return ok;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }
            writer.Write(this.Simmering.MeltingPoint);
            writer.Write(this.Simmering.MeltingDuration);
            writer.Write(this.Simmering.SmeltedRatio);
            writer.Write((ushort)this.Simmering.SmeltingType);
            CombustibleProperties simmering = this.Simmering;
            writer.Write(((simmering != null) ? simmering.SmeltedStack : null) != null);
            CombustibleProperties simmering2 = this.Simmering;
            if (((simmering2 != null) ? simmering2.SmeltedStack : null) != null)
            {
                this.Simmering.SmeltedStack.ToBytes(writer);
            }
            writer.Write(this.Simmering.RequiresContainer);
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CraftingRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Simmer Recipe (FromBytes)");
            }

            this.Simmering = new CombustibleProperties();
            this.Simmering.MeltingPoint = reader.ReadInt32();
            this.Simmering.MeltingDuration = reader.ReadSingle();
            this.Simmering.SmeltedRatio = reader.ReadInt32();
            this.Simmering.SmeltingType = (EnumSmeltType)reader.ReadUInt16();
            if (reader.ReadBoolean())
            {
                this.Simmering.SmeltedStack = new JsonItemStack();
                this.Simmering.SmeltedStack.FromBytes(reader, resolver.ClassRegistry);
                this.Simmering.SmeltedStack.Resolve(resolver, "Simmer Recipe (FromBytes)", true);
            }
            this.Simmering.RequiresContainer = reader.ReadBoolean();
        }

        public SimmerRecipe Clone()
        {
            return new()
            {
                Simmering = Simmering.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                Ingredients = [.. Ingredients.Select(ingred => ingred.Clone())]
            };
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            var mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingred in Ingredients)
            {
                if (ingred?.Name == null || !ingred.Code.Path.Contains("*")) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                mappings[ingred.Name] = [.. world.Collectibles.Where(obj => obj.ItemClass == ingred.Type && obj.Code != null && !obj.IsMissing && WildcardUtil.Match(ingred.Code, obj.Code))
                                                              .Select(obj => obj.Code.Path.Substring(wildcardStartLen))
                                                              .Select(code => code.Substring(0, code.Length - wildcardEndLen))
                                                              .Where(codepart => ingred.AllowedVariants?.Contains(codepart) != false)];
            }

            return mappings;
        }
    }

    public class DoughIngredient : IByteSerializable
    {
        public CraftingRecipeIngredient[] Inputs;

        public CraftingRecipeIngredient GetMatch(ItemStack stack, bool checkStackSize = true)
        {
            if (stack == null) return null;

            for (int i = 0; i < Inputs.Length; i++)
            {
                if (Inputs[i].SatisfiesAsIngredient(stack, checkStackSize)) return Inputs[i];
            }

            return null;
        }

        public bool Resolve(IWorldAccessor world, string debug)
        {
            bool ok = true;

            for (int i = 0; i < Inputs.Length; i++)
            {
                ok &= Inputs[i].Resolve(world, debug);
            }

            return ok;
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i] = new CraftingRecipeIngredient();
                Inputs[i].FromBytes(reader, resolver);
                Inputs[i].Resolve(resolver, "Dough Ingredient (FromBytes)");
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Inputs.Length);
            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i].ToBytes(writer);
            }

        }

        public DoughIngredient Clone()
        {
            CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];

            for (int i = 0; i < Inputs.Length; i++)
            {
                newings[i] = Inputs[i].Clone();
            }

            return new DoughIngredient()
            {
                Inputs = newings
            };
        }
    }

    public class ACARecipeRegistrySystem : ModSystem
    {
        public static bool canRegister = true;

        public List<CookingRecipe> MixingRecipes = new List<CookingRecipe>();

        public List<DoughRecipe> DoughRecipes = new List<DoughRecipe>();

        public List<SimmerRecipe> SimmerRecipes = new List<SimmerRecipe>();

        public override double ExecuteOrder()
        {
            return 1.0;
        }

        public override void StartPre(ICoreAPI api)
        {
            canRegister = true;
        }

        public override void Start(ICoreAPI api)
        {
            MixingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<CookingRecipe>>("mixingrecipes").Recipes;
            DoughRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<DoughRecipe>>("doughrecipes").Recipes;
            SimmerRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<SimmerRecipe>>("simmerrecipes").Recipes;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!(api is ICoreServerAPI coreServerAPI))
            {
                return;
            }
            loadMixingRecipes(coreServerAPI);
            loadDoughRecipes(coreServerAPI);
            loadSimmerRecipes(coreServerAPI);


        }
        void loadMixingRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/mixing");
            int recipeQuantity = 0;

            foreach (var val in files)
            {
                String recCode = null;
                if (val.Value is JObject)
                {
                    CookingRecipe rec = val.Value.ToObject<CookingRecipe>();
                    if (!rec.Enabled) continue;

                    rec.Resolve(coreServerAPI.World, "mixing recipe " + val.Key);
                    RegisterCookingRecipe(rec);
                    recCode = rec.Code;
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        CookingRecipe rec = token.ToObject<CookingRecipe>();
                        if (!rec.Enabled) continue;

                        rec.Resolve(coreServerAPI.World, "mixing recipe " + val.Key);
                        RegisterCookingRecipe(rec);
                        recCode = rec.Code;
                        recipeQuantity++;
                    }
                }
            }
            coreServerAPI.World.Logger.Event("{0} mixing recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The chef and the apprentice..."));
        }
        void loadDoughRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/kneading");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    DoughRecipe rec = val.Value.ToObject<DoughRecipe>();
                    if (!rec.Enabled) continue;

                    LoadKneadingRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        DoughRecipe rec = token.ToObject<DoughRecipe>();
                        if (!rec.Enabled) continue;

                        LoadKneadingRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                    }
                }
            }

            coreServerAPI.World.Logger.Event("{0} kneading recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The butter and the bread..."));
        }
        void loadSimmerRecipes(ICoreServerAPI coreServerAPI)
        {
            Dictionary<AssetLocation, JToken> files = coreServerAPI.Assets.GetMany<JToken>(coreServerAPI.Server.Logger, "recipes/simmering");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    SimmerRecipe rec = val.Value.ToObject<SimmerRecipe>();
                    if (!rec.Enabled) continue;

                    LoadSimmeringRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        SimmerRecipe rec = token.ToObject<SimmerRecipe>();
                        if (!rec.Enabled) continue;

                        LoadSimmeringRecipe(val.Key, rec, coreServerAPI, ref recipeQuantity, ref ignored);
                    }
                }
            }
            coreServerAPI.World.Logger.Event("{0} simmer recipes loaded", recipeQuantity);
            coreServerAPI.World.Logger.StoryEvent(Lang.Get("aculinaryartillery:The syrup and lard..."));
        }
        void LoadSimmeringRecipe(AssetLocation path, SimmerRecipe recipe, ICoreServerAPI coreServerAPI, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "simmer recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(coreServerAPI.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<SimmerRecipe> subRecipes = new List<SimmerRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        SimmerRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (rec.Ingredients.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds;

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Simmering.SmeltedStack.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    coreServerAPI.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (SimmerRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(coreServerAPI.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    RegisterSimmerRecipe(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(coreServerAPI.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                RegisterSimmerRecipe(recipe);
                quantityRegistered++;
            }
        }
        void LoadKneadingRecipe(AssetLocation path, DoughRecipe recipe, ICoreServerAPI coreServerAPI, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "kneading recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(coreServerAPI.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<DoughRecipe> subRecipes = new List<DoughRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        DoughRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (ingreds.Inputs.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds.Inputs[0];

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    coreServerAPI.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (DoughRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(coreServerAPI.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    RegisterDoughRecipe(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(coreServerAPI.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                RegisterDoughRecipe(recipe);
                quantityRegistered++;
            }
        }

        public void RegisterCookingRecipe(CookingRecipe cookingrecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register cooking recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            MixingRecipes.Add(cookingrecipe);
        }

        public void RegisterDoughRecipe(DoughRecipe doughRecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register dough recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            DoughRecipes.Add(doughRecipe);
        }

        public void RegisterSimmerRecipe(SimmerRecipe simmerRecipe)
        {
            if (!canRegister)
            {
                throw new InvalidOperationException("Coding error: Can no long register simmering recipes. Register them during AssetsLoad/AssetsFinalize and with ExecuteOrder < 99999");
            }

            SimmerRecipes.Add(simmerRecipe);
        }
    }
}