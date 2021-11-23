using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace NwdbInfoApi
{
    /// <summary>
    /// Allows accessing information from nwdbinfo APIs only after getting allow-listed access by talking with owner.
    /// The User-Agent string used is allow-listed access and is therefore considered some-what of a secret.
    /// </summary>
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
        private string UserAgent;

        public NwdbInfoApiClient(string dataDirectory, string userAgent = null)
        {
            DataDirectory = dataDirectory;
            UserAgent = userAgent;
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

            List<T> pages = new List<T>();
            using (HttpClient httpClient = new HttpClient())
            {
                int pageCount = 1;

                for (int page = 1; page <= pageCount; page++)
                {
                    Console.WriteLine($"Fetching page {page}...");
                    ApiResponse<List<T>> content = await GetResource<List<T>>(httpClient, $"https://nwdb.info/db/{resource}/page/{page}.json");

                    if (content.Success)
                    {
                        pages.AddRange(content.Data);
                        pageCount = content.PageCount.Value;
                        Console.WriteLine($"Added page to collection...");
                    }
                }
            }

            SaveAsJson(cacheFile, pages);

            return pages;
        }

        private async Task<ApiResponse<T>> GetResource<T>(HttpClient httpClient, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(UserAgent))
            {
                request.Headers.Add("User-Agent", UserAgent);
            }

            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string contentString = await response.Content.ReadAsStringAsync();
                ApiResponse<T> content = JsonSerializer.Deserialize<ApiResponse<T>>(contentString, NWDB_JSON_SERIALIZER_OPTIONS);
                return content;
            }
            else
            {
                Console.WriteLine($"Error fetching resource '{url}'... (${response.StatusCode}: {response.ReasonPhrase})");
                return default;
            }
        }

        // TODO : List Detailed Items (need info on containersWithItem to get salvaging data, need "IngredientCategories" to bucket generic items like "Refining Materials Tier 3" for buying categories of items)

        public async Task<List<RecipeData>> ListDetailedRecipesAsync()
        {
            if (IsCached(RECIPE_DETAILS_CACHE_FILE_NAME))
            {
                return LoadFromJson<List<RecipeData>>(RECIPE_DETAILS_CACHE_FILE_NAME);
            }

            List<RecipesPageData> recipes = await ListRecipesAsync();

            List<RecipeData> pages = new List<RecipeData>();
            using (HttpClient httpClient = new HttpClient())
            {
                int count = 0;
                foreach (RecipesPageData recipe in recipes)
                {
                    ApiResponse<RecipeData> content = await GetResource<RecipeData>(httpClient, $"https://nwdb.info/db/recipe/{recipe.Id}.json");

                    if (content.Success)
                    {
                        pages.Add(content.Data);

                        count++;
                        Console.Write($"\rAdded Item {count}/{recipes.Count}                    ");
                    }
                }
            }

            SaveAsJson(RECIPE_DETAILS_CACHE_FILE_NAME, pages);

            return pages;
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
