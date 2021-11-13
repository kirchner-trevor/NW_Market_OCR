using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using MW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    class MarketTools
    {
        private static string[] TARGET_TRADESKILLS = new[] { "Weaponsmithing", "Jewelcrafting", "Arcana", "Furnishing", "Armoring", "Engineering", "Cooking", };
        private static int[] TARGET_TRADESKILL_LEVELS = new[] { 200 };
        private static string TARGET_ITEM = default;

        private static DateTime DATE_FILTER = default;
        private static string LOCATION_FILTER = default;
        private static float SIMILAR_COST_PERCENTAGE = 0.25f; // How much more expensive an item can be and still get included in the total available amount
        private static int MIN_AVAILABLE = 1;

        private const bool INCLUDE_MATERIAL_CONVERTER_RECIPES = false;
        private const bool SHOW_ALL_RECIPES = false;
        private const bool LIST_UNOBTAINABLE_ITEMS = true;
        private const bool INCLUDE_EXPIRED = false;
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";

        static async Task Main(string[] args)
        {
            string credentials = args[0];
            string[] credentialParts = credentials.Split(":");
            string accessKeyId = credentialParts[0];
            string secretAccessKey = credentialParts[1];

            DateTime lastUpdateDate = DateTime.MinValue;

            while (true)
            {
                MarketDatabase marketDatabase = new MarketDatabase(DATA_DIRECTORY);
                marketDatabase.LoadDatabaseFromDisk();
                Console.WriteLine($"Market Listings: {marketDatabase.Listings.Count}");

                if (marketDatabase.Updated > lastUpdateDate)
                {
                    lastUpdateDate = marketDatabase.Updated;

                    NwdbInfoApiClient nwdbInfoApiClient = new NwdbInfoApiClient(DATA_DIRECTORY);
                    List<ItemsPageData> items = await nwdbInfoApiClient.ListItemsAsync();
                    Console.WriteLine($"Items: {items.Count}");

                    List<RecipesPageData> recipes = await nwdbInfoApiClient.ListRecipesAsync();
                    Console.WriteLine($"Recipes: {recipes.Count}");

                    List<RecipeData> recipeDetails = await nwdbInfoApiClient.ListDetailedRecipesAsync();
                    Console.WriteLine($"Recipe Details: {recipeDetails.Count}");

                    AmazonS3Client s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, RegionEndpoint.USEast2);

                    Console.WriteLine("\n== Finding Best Recipes To Craft! ==\n");

                    List<CraftItemSuggestion> itemCraftingSuggestions = new List<CraftItemSuggestion>();
                    List<RecipeDataIngredient> unobtainableIngredients = new List<RecipeDataIngredient>();
                    foreach (string targetTradeskill in TARGET_TRADESKILLS)
                    {
                        foreach (int targetTradeskillLevel in TARGET_TRADESKILL_LEVELS)
                        {
                            List<RecipeData> recipesToCraft = recipeDetails.Where(_ => (TARGET_ITEM == default || _.Name.Contains(TARGET_ITEM)) && (targetTradeskill == default || _.Tradeskill == targetTradeskill) && (targetTradeskillLevel == default || _.RecipeLevel <= targetTradeskillLevel)).ToList();

                            //if (!SHOW_ALL_RECIPES)
                            //{
                            //    Console.WriteLine($"Checking {recipesToCraft.Count} recipes against {marketDatabase.Listings.Count} market listings...");
                            //}

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

                                    if ((targetTradeskill == default || tradeskillExp.Key == targetTradeskill) && efficiency > highestEfficiency)
                                    {
                                        mostCostEfficientRecipe = recipeSummary;
                                        highestEfficiency = efficiency;
                                    }
                                }

                                if (SHOW_ALL_RECIPES)
                                {
                                    Console.WriteLine($"{new string('-', 40)}");
                                }


                                itemCraftingSuggestions.Add(ConvertToCraftItemSuggestion(recipeSummary));
                            }

                            if (LIST_UNOBTAINABLE_ITEMS)
                            {
                                foreach (RecipeData recipeToCraft in recipesToCraft)
                                {
                                    RecipeCraftSummary recipeSummary = GetRecipeSummary(recipeToCraft, marketDatabase, recipeDetails, null, recipeSummaryCache);
                                    unobtainableIngredients.AddRange(recipeSummary.Unobtainable);
                                }
                            }

                            Console.WriteLine($"\n== Best Recipe for {targetTradeskill} @ Lvl {targetTradeskillLevel}! ==\n");
                            WriteBuyCraftTreeToConsole(mostCostEfficientRecipe);
                            Console.WriteLine($"");
                            foreach (KeyValuePair<string, int> tradeskillExp in CalculateTradeskillExp(mostCostEfficientRecipe))
                            {
                                double efficiency = Math.Ceiling(tradeskillExp.Value / mostCostEfficientRecipe.MinimumCost);
                                Console.WriteLine($"{tradeskillExp.Key}: {tradeskillExp.Value} ({efficiency}xp / $)");
                            }
                        }
                    }

                    if (LIST_UNOBTAINABLE_ITEMS)
                    {
                        Console.WriteLine($"\n== Unobtainable Items Found During Search! ==");
                        foreach (string ingredient in unobtainableIngredients.OrderBy(_ => _.Type).Select(_ => _.Name).Distinct().OrderBy(_ => _))
                        {
                            Console.WriteLine($"  {ingredient}");
                        }
                    }

                    Console.WriteLine("Writing recipe suggestions to disk...");
                    string recipeSuggestionsPath = Path.Combine(DATA_DIRECTORY, "recipeSuggestions.json");
                    string json = JsonSerializer.Serialize(new RecipeSuggestions
                    {
                        Suggestions = itemCraftingSuggestions
                            // Filter out any recipes that didn't calculate properly
                            .Where(_ => float.IsNormal(_.CostPerQuantity) && _.CostPerQuantity < float.MaxValue)
                            // Get only 1 element per recipe id
                            .GroupBy(_ => _.RecipeId)
                            .Select(_ => _.First())
                            .ToList(),
                    });
                    File.WriteAllText(recipeSuggestionsPath, json);
                    Console.WriteLine("Recipe suggestions written to disk!");

                    Console.WriteLine("Uploading recipe suggestions...");
                    PutObjectResponse putResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = "nwmarketdata",
                        Key = "recipeSuggestions.json",
                        FilePath = recipeSuggestionsPath,
                    });
                    Console.WriteLine("Recipe suggestions uploaded!");
                }
                else
                {
                    Console.WriteLine($"Database has not updated since last run at {lastUpdateDate}, skipping...");
                }

                Console.WriteLine("Sleeping for 30 minutes!");
                Thread.Sleep(180_000);
            }
        }

        public class RecipeSuggestions
        {
            public List<CraftItemSuggestion> Suggestions { get; set; } = new List<CraftItemSuggestion>();
            public DateTime Updated { get; set; } = DateTime.UtcNow;
        }

        public static CraftItemSuggestion ConvertToCraftItemSuggestion(RecipeCraftSummary recipeSummary)
        {
            Dictionary<string, int> totalExperience = CalculateTradeskillExp(recipeSummary);
            return new CraftItemSuggestion
            {
                RecipeId = recipeSummary.Recipe.Id,
                Name = recipeSummary.Recipe.Name,
                LevelRequirement = recipeSummary.Recipe.RecipeLevel,
                Tradeskill = recipeSummary.Recipe.Tradeskill,
                TotalExperience = totalExperience,
                CostPerQuantity = recipeSummary.MinimumCost,
                Quantity = recipeSummary.CraftCount,
                ExperienceEfficienyForPrimaryTradekill = totalExperience[recipeSummary.Recipe.Tradeskill] / recipeSummary.MinimumCost,
                Buys = recipeSummary.Buys.Select(_ => new BuyItemSuggestion
                {
                    Available = _.TotalAvailableAtLocation,
                    Location = _.Location,
                    Name = _.Name,
                    CostPerQuantity = _.Listing.Price,
                    Quantity = _.Quantity,
                }).ToList(),
                Crafts = recipeSummary.Crafts.Select(_ => ConvertToCraftItemSuggestion(_)).ToList(),
            };
        }

        public class BuyItemSuggestion
        {
            public string Name { get; set; }
            public int Quantity { get; set; }

            public float CostPerQuantity { get; set; }
            public int Available { get; set; }
            public string Location { get; set; }
        }

        public class CraftItemSuggestion
        {
            public string RecipeId { get; set; }

            public string Name { get; set; }
            public int Quantity { get; set; }

            public string Tradeskill { get; set; }
            public int LevelRequirement { get; set; }
            public float CostPerQuantity { get; set; }
            public float ExperienceEfficienyForPrimaryTradekill { get; set; }

            public Dictionary<string, int> TotalExperience { get; set; } = new Dictionary<string, int>();

            public List<CraftItemSuggestion> Crafts { get; set; } = new List<CraftItemSuggestion>();
            public List<BuyItemSuggestion> Buys { get; set; } = new List<BuyItemSuggestion>();
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
                Console.WriteLine($"{new string(' ', currentIndent * 2)}(Buy)   {buy.Name} x{itemBuy.Quantity} (${itemBuy.Listing.Price}ea | {itemBuy.TotalAvailableAtLocation} @ {itemBuy.Location})");
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

            foreach (RecipeBuyItemAction buy in recipeSummary.Buys)
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
                        TotalAvailableAtLocation = buy.TotalAvailableAtLocation,
                    };
                    itemsToBuy.Add(buy.Listing.Name, buyItemAction);
                }
            }

            foreach (RecipeCraftSummary craftRecipeSummary in recipeSummary.Crafts)
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
                RecipeCraftSummary existingSummary = recipeSummaryCache[recipe.Id];
                return new RecipeCraftSummary
                {
                    Buys = existingSummary.Buys,
                    CraftCount = 1,
                    Crafts = existingSummary.Crafts,
                    MinimumCost = existingSummary.MinimumCost,
                    Recipe = existingSummary.Recipe,
                    Unobtainable = existingSummary.Unobtainable,
                };
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
                    int marketListingTotalAvailableAtLocation = 0;
                    float minimumCostToBuy = float.MaxValue;
                    MarketItemSummary marketSummary = marketDatabase.GetItemSummary(recipeDataIngredient.Name, LOCATION_FILTER, DATE_FILTER, INCLUDE_EXPIRED);
                    if (marketSummary.LocationPrices.Count != 0)
                    {
                        foreach (TimePrices timePrices in marketSummary.LocationPrices.SelectMany(_ => _.TimePrices))
                        {
                            IEnumerable<float> pricesOfQuantity = timePrices.Listings.Where(_ => _.Latest.Available >= MIN_AVAILABLE).Select(_ => _.Price);
                            float minPriceOfQuantity = pricesOfQuantity.Any() ? pricesOfQuantity.Min() : float.MaxValue;
                            if ((minPriceOfQuantity * recipeDataIngredient.Quantity) < minimumCostToBuy)
                            {
                                marketListingToBuy = timePrices.Listings.First(_ => _.Price == minPriceOfQuantity);
                                minimumCostToBuy = timePrices.Minimum * recipeDataIngredient.Quantity;
                                marketListingTotalAvailableAtLocation = timePrices.Listings
                                    .Where(_ => (Math.Abs(_.Price - marketListingToBuy.Price) <= marketListingToBuy.Price * SIMILAR_COST_PERCENTAGE) && _.Location == marketListingToBuy.Location)
                                    .Sum(_ => _.Latest.Available);
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
                                TotalAvailableAtLocation = marketListingTotalAvailableAtLocation,
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
        public int TotalAvailableAtLocation;

        public MarketListing Listing;
    }
}
