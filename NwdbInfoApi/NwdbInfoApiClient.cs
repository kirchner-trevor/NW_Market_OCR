using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NwdbInfoApi
{
    public class NwdbInfoApiClient
    {
        private const string ITEM_CACHE_FILE_NAME = "itemCache.json";
        private const string RECIPE_CACHE_FILE_NAME = "recipeCache.json";
        private const string RECIPE_DETAILS_CACHE_FILE_NAME = "recipeDetailsCache.json";

        private static JsonSerializerOptions NWDB_JSON_SERIALIZER_OPTIONS = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        private string DataDirectory;

        public NwdbInfoApiClient(string dataDirectory)
        {
            DataDirectory = dataDirectory;
        }

        public async Task<List<ItemsPageData>> ListItemsAsync()
        {
            return await ListPagesAsync<ItemsPageData>(ITEM_CACHE_FILE_NAME, "items");
        }

        public async Task<List<RecipesPageData>> ListRecipesAsync()
        {
            return await ListPagesAsync<RecipesPageData>(RECIPE_CACHE_FILE_NAME, "recipes");
        }

        private async Task<List<T>> ListPagesAsync<T>(string cacheFile, string resource)
        {
            if (IsCached(cacheFile))
            {
                return LoadFromJson<List<T>>(cacheFile);
            }

            return new List<T>();
        }

        // TODO : List Detailed Items (need info on containersWithItem to get salvaging data, need "IngredientCategories" to bucket generic items like "Refining Materials Tier 3" for buying categories of items)

        public async Task<List<RecipeData>> ListDetailedRecipesAsync()
        {
            if (IsCached(RECIPE_DETAILS_CACHE_FILE_NAME))
            {
                return LoadFromJson<List<RecipeData>>(RECIPE_DETAILS_CACHE_FILE_NAME);
            }

            return new List<RecipeData>();
        }

        private bool IsCached(string fileName)
        {
            return File.Exists(Path.Combine(DataDirectory, fileName));
        }

        private void SaveAsJson(string fileName, object objectToSave)
        {
            Console.WriteLine($"Saving {fileName} to disk...");
            string json = JsonSerializer.Serialize(objectToSave);
            string filePath = Path.Combine(DataDirectory, fileName);
            File.WriteAllText(filePath, json);
        }

        private T LoadFromJson<T>(string fileName) where T : new()
        {
            Console.WriteLine($"Loading {fileName} from disk...");
            string filePath = Path.Combine(DataDirectory, fileName);
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<T>(json);
            }
            else
            {
                return new T();
            }
        }
    }

    public class ApiResponse<TData>
    {
        public bool Success { get; set; }
        public int? PageCount { get; set; }
        public TData Data { get; set; }
    }

    public class RecipesPageData
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Type { get; set; }
        public string ItemType { get; set; }
        public string Tradeskill { get; set; }
        public int RecipeLevel { get; set; }
        public int RecipeExp { get; set; }
    }

    public class RecipeData : RecipesPageData
    {
        public int CraftingFee { get; set; }
        public List<RecipeDataIngredient> Ingredients { get; set; }
        public RecipeDataOutput Output { get; set; }
    }

    public class RecipeDataIngredient
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Type { get; set; } // item, category
        public int Quantity { get; set; }

        public List<RecipeDataIngredient> SubIngredients { get; set; }
    }

    public class RecipeDataOutput
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public int Quantity { get; set; }
    }

    public class ItemsPageData
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Type { get; set; }
        public string ItemType { get; set; }
        public bool NamedItem { get; set; }
    }
}
