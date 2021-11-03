using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    class Program
    {
        private static string TARGET_TRADESKILL = "Furnishing";
        private static int TARGET_TRADESKILL_LEVEL = default;
        private static string TARGET_ITEM = default; // Check "Powerful Incense" for double nested quantities

        private static DateTime DATE_FILTER = DateTime.Now.AddDays(-7);
        private static string LOCATION_FILTER = default;

        private const bool INCLUDE_MATERIAL_CONVERTER_RECIPES = true;
        private const bool SHOW_ALL_RECIPES = false;
        private const bool LIST_UNOBTAINABLE_ITEMS = true;

        static async Task Main(string[] args)
        {
            MarketDatabase marketDatabase = new MarketDatabase(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data");
            marketDatabase.LoadDatabaseFromDisk();
            Console.WriteLine($"Market Listings: {marketDatabase.Listings.Count}");

            NwdbInfoApiClient nwdbInfoApiClient = new NwdbInfoApiClient(@"C:\Users\kirch\source\repos\NW_Market_OCR\Data");
            List<ItemsPageData> items = await nwdbInfoApiClient.ListItemsAsync();
            Console.WriteLine($"Items: {items.Count}");

            List<RecipesPageData> recipes = await nwdbInfoApiClient.ListRecipesAsync();
            Console.WriteLine($"Recipes: {recipes.Count}");

            List<RecipeData> recipeDetails = await nwdbInfoApiClient.ListDetailedRecipesAsync();
            Console.WriteLine($"Recipe Details: {recipeDetails.Count}");

            Console.WriteLine("\n== Finding Best Recipes To Craft! ==\n");

            List<RecipeData> recipesToCraft = recipeDetails.Where(_ => (TARGET_ITEM == default || _.Name.Contains(TARGET_ITEM)) && (TARGET_TRADESKILL == default || _.Tradeskill == TARGET_TRADESKILL) && (TARGET_TRADESKILL_LEVEL == default || _.RecipeLevel <= TARGET_TRADESKILL_LEVEL)).ToList();

            if (!SHOW_ALL_RECIPES)
            {
                Console.WriteLine($"Checking {recipesToCraft.Count} recipes against {marketDatabase.Listings.Count} market listings...");
            }

            RecipeCraftSummary mostCostEfficientRecipe = default;
            double highestEfficiency = float.MinValue;

            Dictionary<string, RecipeCraftSummary> recipeSummaryCache = new Dictionary<string, RecipeCraftSummary>();
            foreach (RecipeData recipeToCraft in recipesToCraft)
            {
                RecipeCraftSummary recipeSummary = GetRecipeSummary(recipeToCraft, marketDatabase, recipeDetails, null, recipeSummaryCache);

                if (SHOW_ALL_RECIPES)
                {
                    WriteBuyCraftTreeToConsole(recipeSummary);
                    Console.WriteLine($"");
                }

                foreach (KeyValuePair<string, int> tradeskillExp in CalculateTradeskillExp(recipeSummary))
                {
                    double efficiency = Math.Ceiling(tradeskillExp.Value / recipeSummary.MinimumCost);

                    if (SHOW_ALL_RECIPES)
                    {
                        Console.WriteLine($"{tradeskillExp.Key}: {tradeskillExp.Value} ({efficiency}xp / $)");
                    }

                    if ((TARGET_TRADESKILL == default || tradeskillExp.Key == TARGET_TRADESKILL) && efficiency > highestEfficiency)
                    {
                        mostCostEfficientRecipe = recipeSummary;
                        highestEfficiency = efficiency;
                    }
                }

                if (SHOW_ALL_RECIPES)
                {
                    Console.WriteLine($"{new string('-', 40)}");
                }
            }

            if (LIST_UNOBTAINABLE_ITEMS)
            {
                Console.WriteLine($"\n== Unobtainable Items Found During Search! ==\n");
                List<RecipeDataIngredient> unobtainableIngredients = new List<RecipeDataIngredient>();
                foreach (RecipeData recipeToCraft in recipesToCraft)
                {
                    RecipeCraftSummary recipeSummary = GetRecipeSummary(recipeToCraft, marketDatabase, recipeDetails, null, recipeSummaryCache);
                    unobtainableIngredients.AddRange(recipeSummary.Unobtainable);
                }

                foreach (string ingredient in unobtainableIngredients.Select(_ => _.Name).Distinct())
                {
                    Console.WriteLine($"  {ingredient}");
                }
            }

            Console.WriteLine($"\n== Best Recipe for {TARGET_TRADESKILL}! ==\n");
            WriteBuyCraftTreeToConsole(mostCostEfficientRecipe);
            Console.WriteLine($"");
            foreach (KeyValuePair<string, int> tradeskillExp in CalculateTradeskillExp(mostCostEfficientRecipe))
            {
                double efficiency = Math.Ceiling(tradeskillExp.Value / mostCostEfficientRecipe.MinimumCost);
                Console.WriteLine($"{tradeskillExp.Key}: {tradeskillExp.Value} ({efficiency}xp / $)");
            }
        }

        private static void WriteBuyCraftTreeToConsole(RecipeCraftSummary recipeSummary, Dictionary<string, RecipeBuyItemAction> itemBuys = null, int currentIndent = 0)
        {
            if (itemBuys == null)
            {
                itemBuys = CalculateItemsToBuy(recipeSummary);
            }

            Console.WriteLine($"{new string(' ', currentIndent * 2)}(Craft) {recipeSummary.Recipe.Name} x{recipeSummary.CraftCount} (${Math.Round(recipeSummary.MinimumCost, 2)}ea)");
            currentIndent++;
            foreach (RecipeBuyItemAction buy in recipeSummary.Buys)
            {
                RecipeBuyItemAction itemBuy = itemBuys[buy.Name];
                Console.WriteLine($"{new string(' ', currentIndent * 2)}(Buy)   {buy.Name} x{itemBuy.Quantity} (${itemBuy.Listing.Price}ea @ {itemBuy.Location})");
            }
            foreach (RecipeDataIngredient unobtainable in recipeSummary.Unobtainable)
            {
                Console.WriteLine($"{new string(' ', currentIndent * 2)}(????)  {unobtainable.Name} x{unobtainable.Quantity}");
            }
            foreach (RecipeCraftSummary craft in recipeSummary.Crafts)
            {
                WriteBuyCraftTreeToConsole(craft, itemBuys, currentIndent++);
            }
        }

        private static Dictionary<string, RecipeBuyItemAction> CalculateItemsToBuy(RecipeCraftSummary recipeSummary, Dictionary<string, RecipeBuyItemAction> itemsToBuy = null)
        {
            if (itemsToBuy == null)
            {
                itemsToBuy = new Dictionary<string, RecipeBuyItemAction>();
            }

            foreach(RecipeBuyItemAction buy in recipeSummary.Buys)
            {
                if (itemsToBuy.ContainsKey(buy.Listing.Name))
                {
                    itemsToBuy[buy.Listing.Name].Quantity += buy.Quantity * recipeSummary.CraftCount;
                    itemsToBuy[buy.Listing.Name].TotalCost += buy.TotalCost * recipeSummary.CraftCount;
                }
                else
                {
                    RecipeBuyItemAction buyItemAction = new RecipeBuyItemAction
                    {
                        Listing = buy.Listing,
                        Location = buy.Location,
                        Name = buy.Name,
                        Quantity = buy.Quantity * recipeSummary.CraftCount,
                        TotalCost = buy.TotalCost * recipeSummary.CraftCount,
                    };
                    itemsToBuy.Add(buy.Listing.Name, buyItemAction);
                }
            }

            foreach(RecipeCraftSummary craftRecipeSummary in recipeSummary.Crafts)
            {
                CalculateItemsToBuy(craftRecipeSummary, itemsToBuy);
            }

            return itemsToBuy;
        }

        private static Dictionary<string, int> CalculateTradeskillExp(RecipeCraftSummary recipeSummary, Dictionary<string, int> tradeskillExp = null)
        {
            if (tradeskillExp == null)
            {
                tradeskillExp = new Dictionary<string, int>();
            }

            if (tradeskillExp.ContainsKey(recipeSummary.Recipe.Tradeskill))
            {
                tradeskillExp[recipeSummary.Recipe.Tradeskill] += recipeSummary.Recipe.RecipeExp * recipeSummary.CraftCount;
            }
            else
            {
                tradeskillExp.Add(recipeSummary.Recipe.Tradeskill, recipeSummary.Recipe.RecipeExp * recipeSummary.CraftCount);
            }

            foreach (RecipeCraftSummary craftRecipeSummary in recipeSummary.Crafts)
            {
                CalculateTradeskillExp(craftRecipeSummary, tradeskillExp);
            }

            return tradeskillExp;
        }

        private static RecipeCraftSummary GetRecipeSummary(RecipeData recipe, MarketDatabase marketDatabase, List<RecipeData> recipeDetails, List<string> allIngredients = null, Dictionary<string, RecipeCraftSummary> recipeSummaryCache = null)
        {
            if (allIngredients == null)
            {
                allIngredients = new List<string>();
            }

            if (recipeSummaryCache == null)
            {
                recipeSummaryCache = new Dictionary<string, RecipeCraftSummary>();
            }

            if (recipeSummaryCache.ContainsKey(recipe.Id))
            {
                return recipeSummaryCache[recipe.Id];
            }

            RecipeCraftSummary recipeSummary = new RecipeCraftSummary();
            recipeSummary.Recipe = recipe;
            recipeSummary.CraftCount = 1;
            allIngredients.Add(recipe.Output.Id);

            List<string> usedCategoricalIngredients = new List<string>(); // When a recipe requires a category of ingredients, you cannot double up on one (e.g. Cooking)

            foreach (RecipeDataIngredient recipeIngredient in recipe.Ingredients)
            {
                List<RecipeDataIngredient> ingredientsToCheck = recipeIngredient.Type == "category"
                    ? recipeIngredient.SubIngredients.Where(_ => !usedCategoricalIngredients.Contains(_.Name)).ToList()
                    : new List<RecipeDataIngredient> { recipeIngredient };

                float minimumCost = float.MaxValue;
                CraftBuyOptions minimumCostCraftBuyOptions = default;
                foreach (RecipeDataIngredient recipeDataIngredient in ingredientsToCheck)
                {
                    MarketListing marketListingToBuy = default;
                    float minimumCostToBuy = float.MaxValue;
                    MarketItemSummary marketSummary = marketDatabase.GetItemSummary(recipeDataIngredient.Name, LOCATION_FILTER, DATE_FILTER);
                    if (marketSummary.LocationPrices.Count != 0)
                    {
                        foreach (TimePrices timePrices in marketSummary.LocationPrices.SelectMany(_ => _.TimePrices))
                        {
                            if ((timePrices.Minimum * recipeDataIngredient.Quantity) < minimumCostToBuy)
                            {
                                marketListingToBuy = timePrices.Listings.First(_ => _.Price == timePrices.Minimum);
                                minimumCostToBuy = timePrices.Minimum * recipeDataIngredient.Quantity;
                            }
                        }
                    }

                    RecipeCraftSummary craftedIngredientSummary = default;
                    float minimumCostToCraft = float.MaxValue;

                    List<RecipeData> ingredientRecipes = recipeDetails
                        .Where(_ => _.Name == $"Recipe: {recipeDataIngredient.Name}")
                        .Where(_ => INCLUDE_MATERIAL_CONVERTER_RECIPES || !_.Ingredients.Any(_ => _.Name.Contains("Material Converter")))
                        .Where(_ => !allIngredients.Contains(_.Output.Id)) // Cannot allow crafting ingredients that are already required for the recipe as that causes an infitinite loop
                        .ToList();

                    foreach (RecipeData ingredientRecipe in ingredientRecipes)
                    {
                        RecipeCraftSummary ingredientRecipeSummary = GetRecipeSummary(ingredientRecipe, marketDatabase, recipeDetails, new List<string>(allIngredients), recipeSummaryCache);
                        ingredientRecipeSummary.CraftCount = (int)Math.Ceiling(recipeDataIngredient.Quantity / (ingredientRecipe.Output.Quantity * 1d));
                        float ingredientRecipeCostToCraft = (float)Math.Round(ingredientRecipeSummary.MinimumCost * recipeDataIngredient.Quantity, 2); // ingredientRecipe.CraftingFee seems broken

                        if (ingredientRecipeCostToCraft < minimumCostToCraft)
                        {
                            craftedIngredientSummary = ingredientRecipeSummary;
                            minimumCostToCraft = ingredientRecipeCostToCraft;
                        }
                    }
  

                    float ingredientOptionMinimumCost = Math.Min(minimumCostToBuy, minimumCostToCraft);

                    if (ingredientOptionMinimumCost < minimumCost)
                    {
                        minimumCost = ingredientOptionMinimumCost;
                        minimumCostCraftBuyOptions = new CraftBuyOptions();

                        if (minimumCostToBuy < minimumCostToCraft)
                        {
                            minimumCostCraftBuyOptions.Buy = new RecipeBuyItemAction
                            {
                                Location = marketListingToBuy.Location,
                                Name = marketListingToBuy.Name,
                                Quantity = recipeDataIngredient.Quantity,
                                TotalCost = minimumCostToBuy,
                                Listing = marketListingToBuy,
                            };
                        }
                        else
                        {
                            minimumCostCraftBuyOptions.Craft = craftedIngredientSummary;
                        }
                    }
                }

                recipeSummary.MinimumCost += minimumCost;
                if (minimumCostCraftBuyOptions?.Buy != null)
                {
                    recipeSummary.Buys.Add(minimumCostCraftBuyOptions.Buy);
                    usedCategoricalIngredients.Add(minimumCostCraftBuyOptions.Buy.Name);
                }
                else if (minimumCostCraftBuyOptions?.Craft != null)
                {
                    recipeSummary.Crafts.Add(minimumCostCraftBuyOptions.Craft);
                    usedCategoricalIngredients.Add(minimumCostCraftBuyOptions.Craft.Recipe.Output.Name);
                }
                else
                {
                    recipeSummary.Unobtainable.Add(recipeIngredient);
                }
            }

            recipeSummaryCache[recipe.Id] = recipeSummary;

            return recipeSummary;
        }
    }

    public class CraftBuyOptions
    {
        public RecipeCraftSummary Craft;
        public RecipeBuyItemAction Buy;
    }

    public class RecipeCraftSummary
    {
        public RecipeData Recipe;
        public float MinimumCost;
        public int CraftCount;

        public List<RecipeBuyItemAction> Buys = new List<RecipeBuyItemAction>();
        public List<RecipeCraftSummary> Crafts = new List<RecipeCraftSummary>();
        public List<RecipeDataIngredient> Unobtainable = new List<RecipeDataIngredient>();
    }

    public class RecipeBuyItemAction
    {
        public string Name;
        public int Quantity;
        public string Location;
        public float TotalCost;

        public MarketListing Listing;
    }
}
