using Cairo;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ACulinaryArtillery.Util
{
    public static class HandbookInfoExtensions
    {
        public static void addCreatedByMixingInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            DoughRecipe[] doughRecipes = [.. capi.GetKneadingRecipes().Where(rec => rec.Output.ResolvedItemstack.Satisfies(inSlot.Itemstack))];

            if (doughRecipes.Length > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new(capi, 7f);
                components.Add(verticalSpaceSmall);

                components.Add(new RichTextComponent(capi, Lang.Get("Created in: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock("aculinaryartillery:mixingbowlmini")), 80.0, 10, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { VerticalAlign = EnumVerticalAlign.Top, PaddingRight = 8.0, UnscaledMarginTop = 8.0 });
                components.Add(new SlideshowItemstackTextComponent(capi, [.. capi.World.SearchBlocks("aculinaryartillery:mixingbowl-*").Select(block => new ItemStack(block))], 80.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));

                foreach (DoughRecipe recipe in doughRecipes)
                {
                    if (recipe.Ingredients == null) continue;

                    components.Add(verticalSpaceSmall);
                    components.Add(new RichTextComponent(capi, Lang.Get("Inputs: "), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                    foreach (DoughIngredient ing in recipe.Ingredients)
                    {
                        ItemStack[] inputs = [.. ing.Inputs.Where(input => input.IsWildCard)
                                                           .SelectMany(input => capi.World.SearchItems(input.Code).Select(item => new ItemStack(item, input.Quantity))
                                                                                                                  .Where(stack => stack != null && ing.GetMatch(stack, false) != null)),
                                              .. ing.Inputs.Select(input => new ItemStack(input.ResolvedItemstack.Id, input.ResolvedItemstack.Class, input.Quantity, (TreeAttribute)input.ResolvedItemstack.Attributes, capi.World))
                                                           .Where(stack => stack != null)
                                             ];

                        if (inputs.Length > 0) components.Add(new SlideshowItemstackTextComponent(capi, inputs, 40.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { ShowStackSize = true, PaddingRight = 5, VerticalAlign = EnumVerticalAlign.FixedOffset, UnscaledMarginTop = 10 });
                    }
                }
                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }

        public static void addMixingIngredientForInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            ItemStack maxstack = inSlot.Itemstack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10;

            List<ItemStack> recipeOutputs = [.. new HashSet<ItemStack>(capi.GetKneadingRecipes().Where(rec => rec.Ingredients.Any(ing => ing.GetMatch(maxstack) != null)).Select(rec => rec.Output.ResolvedItemstack).Where(stack => stack != null))];

            if (recipeOutputs.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 7f));
                components.Add(new RichTextComponent(capi, Lang.Get("Kneading Ingredient for: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ClearFloatTextComponent(capi, 2f));

                while (recipeOutputs.Count > 0)
                {
                    ItemStack dstack = recipeOutputs[0];
                    recipeOutputs.RemoveAt(0);
                    if (dstack == null) continue;

                    components.Add(new SlideshowItemstackTextComponent(capi, dstack, recipeOutputs, 40.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }

        public static void addSimmerIngredientForInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            ItemStack maxstack = inSlot.Itemstack.Clone();
            maxstack.StackSize = maxstack.Collectible.MaxStackSize * 10;

            List<ItemStack> recipeOutputs = [.. new HashSet<ItemStack>(capi.GetSimmerRecipes().Where(rec => rec.Ingredients.Any(ing => ing.SatisfiesAsIngredient(maxstack))).Select(rec => rec.Simmering.SmeltedStack.ResolvedItemstack).Where(stack => stack != null))];

            if (recipeOutputs.Count > 0)
            {
                components.Add(new ClearFloatTextComponent(capi, 7f));
                components.Add(new RichTextComponent(capi, Lang.Get("Simmering Ingredient for: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ClearFloatTextComponent(capi, 2f));

                while (recipeOutputs.Count > 0)
                {
                    ItemStack dstack = recipeOutputs[0];
                    recipeOutputs.RemoveAt(0);
                    if (dstack == null) continue;

                    components.Add(new SlideshowItemstackTextComponent(capi, dstack, recipeOutputs, 40.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                }

                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }

        public static void addCreatedBySimmeringInfo(this List<RichTextComponentBase> components, ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor)
        {
            List<SimmerRecipe> simmerRecipes = [.. capi.GetSimmerRecipes().Where(rec => rec.Simmering.SmeltedStack.ResolvedItemstack.Satisfies(inSlot.Itemstack))];
            
            if (simmerRecipes.Count > 0)
            {
                ClearFloatTextComponent verticalSpaceSmall = new ClearFloatTextComponent(capi, 7f);
                components.Add(verticalSpaceSmall);

                components.Add(new RichTextComponent(capi, Lang.Get("Created in: ") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
                components.Add(new ItemstackTextComponent(capi, new ItemStack(capi.World.GetBlock(new AssetLocation("aculinaryartillery:saucepan-burned"))), 80.0, 10, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { VerticalAlign = EnumVerticalAlign.Top, PaddingRight = 8.0, UnscaledMarginTop = 8.0 });
                components.Add(new SlideshowItemstackTextComponent(capi, [.. capi.World.SearchBlocks(new AssetLocation("aculinaryartillery:cauldronmini-*")).Select(block => new ItemStack(block))], 80.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));
                components.Add(new SlideshowItemstackTextComponent(capi, [.. capi.World.SearchBlocks(new AssetLocation("aculinaryartillery:cauldron-*")).Select(block => new ItemStack(block))], 80.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))));

                foreach (SimmerRecipe recipe in simmerRecipes)
                {
                    if (recipe.Ingredients == null) continue;

                    components.Add(verticalSpaceSmall);
                    components.Add(new RichTextComponent(capi, Lang.Get("Inputs: "), CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));

                    foreach (CraftingRecipeIngredient ing in recipe.Ingredients.Where(ing => ing.IsWildCard || ing.ResolvedItemstack != null))
                    {
                        ItemStack[] inputs = [.. ing.IsWildCard ? capi.World.SearchItems(ing.Code).Select(item => new ItemStack(item, ing.Quantity))
                                                                                                  .Where(stack => stack != null && ing.SatisfiesAsIngredient(stack, false)) : [],
                                              new ItemStack(ing.ResolvedItemstack.Id, ing.ResolvedItemstack.Class, ing.Quantity, (TreeAttribute)ing.ResolvedItemstack.Attributes, capi.World)
                                             ];

                        if (inputs.Length > 0) components.Add(new SlideshowItemstackTextComponent(capi, inputs, 40.0, EnumFloat.Inline, (ItemStack cs) => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs))) { ShowStackSize = true, PaddingRight = 5, VerticalAlign = EnumVerticalAlign.FixedOffset, UnscaledMarginTop = 10 });
                    }
                }

                components.Add(new ClearFloatTextComponent(capi, 3f));
            }
        }
    }
}