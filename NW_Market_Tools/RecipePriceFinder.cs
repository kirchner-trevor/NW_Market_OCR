using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using NW_Market_Model;
using NwdbInfoApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NW_Market_Tools
{
    public class RecipePriceFinder : IMarketTool
    {
        private static string[] TARGET_TRADESKILLS = new[] { "Weaponsmithing", "Jewelcrafting", "Arcana", "Furnishing", "Armoring", "Engineering", "Cooking", };
        private static int[] TARGET_TRADESKILL_LEVELS = new[] { 200 };
        private static string TARGET_ITEM = default;

        private static DateTime DATE_FILTER = DateTime.UtcNow.AddDays(-7);
        private static string LOCATION_FILTER = default;
        private static float SIMILAR_COST_PERCENTAGE = 0.25f; // How much more expensive an item can be and still get included in the total available amount
        private static int MIN_AVAILABLE = 1;

        private const bool INCLUDE_MATERIAL_CONVERTER_RECIPES = false;
        private const bool SHOW_ALL_RECIPES = false;
        private const bool LIST_UNOBTAINABLE_ITEMS = false;
        private const bool INCLUDE_EXPIRED = false;
        private const string DATA_DIRECTORY = @"C:\Users\kirch\source\repos\NW_Market_OCR\Data";

        private readonly string AccessKeyId;
        private readonly string SecretAccessKey;
        private readonly ItemDatabase itemDatabase;

        private DateTime lastUpdateDate = DateTime.MinValue;

        public RecipePriceFinder(string accessKeyId, string secretAccessKey, ItemDatabase itemDatabase)
        {
            AccessKeyId = accessKeyId;
            SecretAccessKey = secretAccessKey;
            this.itemDatabase = itemDatabase;
        }

        public async Task Run(string server)
        {
            lastUpdateDate = File.GetLastWriteTimeUtc(Path.Combine(DATA_DIRECTORY, server, "recipeSuggestions.json"));

            itemDatabase.SetServer(server);
            itemDatabase.LoadDatabaseFromDisk();

            if (itemDatabase.Contents != null && itemDatabase.Contents.Updated > lastUpdateDate)
            {
                Trace.WriteLine($"Updating recipes for server {server}");
                Trace.WriteLine($"Items: {itemDatabase.Contents.Items.Count}");

                NwdbInfoApiClient nwdbInfoApiClient = new NwdbInfoApiClient(DATA_DIRECTORY);
                List<ItemsPageData> items = await nwdbInfoApiClient.ListItemsAsync();
                Trace.WriteLine($"Items: {items.Count}");

                List<RecipesPageData> recipes = await nwdbInfoApiClient.ListRecipesAsync();
                Trace.WriteLine($"Recipes: {recipes.Count}");

                List<RecipeData> recipeDetails = await nwdbInfoApiClient.ListDetailedRecipesAsync();
                Trace.WriteLine($"Recipe Details: {recipeDetails.Count}");

                AmazonS3Client s3Client = new AmazonS3Client(AccessKeyId, SecretAccessKey, RegionEndpoint.USEast2);

                Trace.WriteLine("\n== Finding Best Recipes To Craft! ==\n");

                List<CraftItemSuggestion> itemCraftingSuggestions = new List<CraftItemSuggestion>();
                List<RecipeDataIngredient> unobtainableIngredients = new List<RecipeDataIngredient>();
                foreach (string targetTradeskill in TARGET_TRADESKILLS)
                {
                    foreach (int targetTradeskillLevel in TARGET_TRADESKILL_LEVELS)
                    {
                        List<RecipeData> recipesToCraft = recipeDetails.Where(_ => (TARGET_ITEM == default || _.Name.Contains(TARGET_ITEM)) && (targetTradeskill == default || _.Tradeskill == targetTradeskill) && (targetTradeskillLevel == default || _.RecipeLevel <= targetTradeskillLevel)).ToList();

                        //if (!SHOW_ALL_RECIPES)
                        //{
                        //    Trace.WriteLine($"Checking {recipesToCraft.Count} recipes against {marketDatabase.Listings.Count} market listings...");
                        //}

                        RecipeCraftSummary mostCostEfficientRecipe = default;
                        double highestEfficiency = float.MinValue;

                        Dictionary<string, RecipeCraftSummary> recipeSummaryCache = new Dictionary<string, RecipeCraftSummary>();
                        foreach (RecipeData recipeToCraft in recipesToCraft)
                        {
                            RecipeCraftSummary recipeSummary = GetRecipeSummary(recipeToCraft, recipeDetails, null, recipeSummaryCache);

                            if (SHOW_ALL_RECIPES)
                            {
                                WriteBuyCraftTreeToTrace(recipeSummary);
                                Trace.WriteLine($"");
                            }

                            foreach (KeyValuePair<string, int> tradeskillExp in CalculateTradeskillExp(recipeSummary))
                            {
                                double efficiency = Math.Ceiling(tradeskillExp.Value / recipeSummary.MinimumCost);

                                if (SHOW_ALL_RECIPES)
                                {
                                    Trace.WriteLine($"{tradeskillExp.Key}: {tradeskillExp.Value} ({efficiency}xp / $)");
                                }

                                if ((targetTradeskill == default || tradeskillExp.Key == targetTradeskill) && efficiency > highestEfficiency)
                                {
                                    mostCostEfficientRecipe = recipeSummary;
                                    highestEfficiency = efficiency;
                                }
                            }

                            if (SHOW_ALL_RECIPES)
                            {
                                Trace.WriteLine($"{new string('-', 40)}");
                            }


                            itemCraftingSuggestions.Add(ConvertToCraftItemSuggestion(recipeSummary));
                        }

                        if (LIST_UNOBTAINABLE_ITEMS)
                        {
                            foreach (RecipeData recipeToCraft in recipesToCraft)
                            {
                                RecipeCraftSummary recipeSummary = GetRecipeSummary(recipeToCraft, recipeDetails, null, recipeSummaryCache);
                                unobtainableIngredients.AddRange(recipeSummary.Unobtainable);
                            }
                        }

                        Trace.WriteLine($"\n== Best Recipe for {targetTradeskill} @ Lvl {targetTradeskillLevel}! ==\n");
                        WriteBuyCraftTreeToTrace(mostCostEfficientRecipe);
                        Trace.WriteLine($"");
                        foreach (KeyValuePair<string, int> tradeskillExp in CalculateTradeskillExp(mostCostEfficientRecipe))
                        {
                            double efficiency = Math.Ceiling(tradeskillExp.Value / mostCostEfficientRecipe.MinimumCost);
                            Trace.WriteLine($"{tradeskillExp.Key}: {tradeskillExp.Value} ({efficiency}xp / $)");
                        }
                    }
                }

                if (LIST_UNOBTAINABLE_ITEMS)
                {
                    Trace.WriteLine($"\n== Unobtainable Items Found During Search! ==");
                    foreach (string ingredient in unobtainableIngredients.OrderBy(_ => _.Type).Select(_ => _.Name).Distinct().OrderBy(_ => _))
                    {
                        Trace.WriteLine($"  {ingredient}");
                    }
                }

                Trace.WriteLine("Writing recipe suggestions to disk...");
                string recipeSuggestionsPath = Path.Combine(DATA_DIRECTORY, server, "recipeSuggestions.json");
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
                Trace.WriteLine("Recipe suggestions written to disk!");

                Trace.WriteLine("Uploading recipe suggestions...");
                PutObjectResponse putResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = "nwmarketdata",
                    Key = server + "/recipeSuggestions.json",
                    FilePath = recipeSuggestionsPath,
                });
                Trace.WriteLine("Recipe suggestions uploaded!");
            }
            else
            {
                Trace.WriteLine($"Database has not updated since last run at {lastUpdateDate}, skipping...");
            }

        }

        private class RecipeSuggestions
        {
            public List<CraftItemSuggestion> Suggestions { get; set; } = new List<CraftItemSuggestion>();
            public DateTime Updated { get; set; } = DateTime.UtcNow;
        }

        private static CraftItemSuggestion ConvertToCraftItemSuggestion(RecipeCraftSummary recipeSummary)
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
                    CostPerQuantity = _.ItemStats.GetLatest().AveragePrice,
                    Quantity = _.Quantity,
                }).ToList(),
                Crafts = recipeSummary.Crafts.Select(_ => ConvertToCraftItemSuggestion(_)).ToList(),
            };
        }

        private class BuyItemSuggestion
        {
            public string Name { get; set; }
            public int Quantity { get; set; }

            public float CostPerQuantity { get; set; }
            public int Available { get; set; }
            public string Location { get; set; }
        }

        private class CraftItemSuggestion
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

        private static void WriteBuyCraftTreeToTrace(RecipeCraftSummary recipeSummary, Dictionary<string, RecipeBuyItemAction> itemBuys = null, int currentIndent = 0)
        {
            if (itemBuys == null)
            {
                itemBuys = CalculateItemsToBuy(recipeSummary);
            }

            Trace.WriteLine($"{new string(' ', currentIndent * 2)}(Craft) {recipeSummary.Recipe.Name} x{recipeSummary.CraftCount} (${Math.Round(recipeSummary.MinimumCost, 2)}ea)");
            currentIndent++;
            foreach (RecipeBuyItemAction buy in recipeSummary.Buys)
            {
                RecipeBuyItemAction itemBuy = itemBuys[buy.Name];
                Trace.WriteLine($"{new string(' ', currentIndent * 2)}(Buy)   {buy.Name} x{itemBuy.Quantity} (${itemBuy.ItemStats.GetLatest().AveragePrice}ea | {itemBuy.TotalAvailableAtLocation} @ {itemBuy.Location})");
            }
            foreach (RecipeDataIngredient unobtainable in recipeSummary.Unobtainable)
            {
                Trace.WriteLine($"{new string(' ', currentIndent * 2)}(????)  {unobtainable.Name} x{unobtainable.Quantity}");
            }
            foreach (RecipeCraftSummary craft in recipeSummary.Crafts)
            {
                WriteBuyCraftTreeToTrace(craft, itemBuys, currentIndent++);
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
                if (itemsToBuy.ContainsKey(buy.ItemStats.Name))
                {
                    itemsToBuy[buy.ItemStats.Name].Quantity += buy.Quantity * recipeSummary.CraftCount;
                    itemsToBuy[buy.ItemStats.Name].TotalCost += buy.TotalCost * recipeSummary.CraftCount;
                }
                else
                {
                    RecipeBuyItemAction buyItemAction = new RecipeBuyItemAction
                    {
                        ItemStats = buy.ItemStats,
                        Location = buy.Location,
                        Name = buy.Name,
                        Quantity = buy.Quantity * recipeSummary.CraftCount,
                        TotalCost = buy.TotalCost * recipeSummary.CraftCount,
                        TotalAvailableAtLocation = buy.TotalAvailableAtLocation,
                    };
                    itemsToBuy.Add(buy.ItemStats.Name, buyItemAction);
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

        private RecipeCraftSummary GetRecipeSummary(RecipeData recipe, List<RecipeData> recipeDetails, List<string> allIngredients = null, Dictionary<string, RecipeCraftSummary> recipeSummaryCache = null)
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
                    ItemStats itemToBuy = default;
                    int marketListingTotalAvailableAtLocation = 0;
                    float minimumCostToBuy = float.MaxValue;

                    ItemStats item = itemDatabase.Contents.Items.FirstOrDefault(_ => _.Name == recipeDataIngredient.Name);
                    PeriodicItemStats latestItemStats = item?.GetLatest();
                    if (latestItemStats != null)
                    {
                        if ((latestItemStats.AveragePrice * recipeDataIngredient.Quantity) < minimumCostToBuy)
                        {
                            itemToBuy = item;
                            minimumCostToBuy = latestItemStats.AveragePrice * recipeDataIngredient.Quantity;
                            marketListingTotalAvailableAtLocation = latestItemStats.TotalAvailableBelowMarketAverage;
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
                        RecipeCraftSummary ingredientRecipeSummary = GetRecipeSummary(ingredientRecipe, recipeDetails, new List<string>(allIngredients), recipeSummaryCache);
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
                                Location = null,
                                Name = itemToBuy.Name,
                                Quantity = recipeDataIngredient.Quantity,
                                TotalCost = minimumCostToBuy,
                                ItemStats = itemToBuy,
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

        private class CraftBuyOptions
        {
            public RecipeCraftSummary Craft;
            public RecipeBuyItemAction Buy;
        }

        private class RecipeCraftSummary
        {
            public RecipeData Recipe;
            public float MinimumCost;
            public int CraftCount;

            public List<RecipeBuyItemAction> Buys = new List<RecipeBuyItemAction>();
            public List<RecipeCraftSummary> Crafts = new List<RecipeCraftSummary>();
            public List<RecipeDataIngredient> Unobtainable = new List<RecipeDataIngredient>();
        }

        private class RecipeBuyItemAction
        {
            public string Name;
            public int Quantity;
            public string Location;
            public float TotalCost;
            public int TotalAvailableAtLocation;

            public ItemStats ItemStats;
        }
    }
}
