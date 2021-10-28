using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NW_Market_OCR
{
    class NwdbInfoApi
    {
        private const string ITEM_CACHE_FILE_NAME = "itemCache.json";
        private static JsonSerializerOptions NWDB_JSON_SERIALIZER_OPTIONS = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private bool IsCachePopulated()
        {
            return File.Exists(ITEM_CACHE_FILE_NAME);
        }

        private List<ItemsPageData> GetCachedListItems()
        {
            return LoadFromJson<List<ItemsPageData>>(ITEM_CACHE_FILE_NAME);
        }

        public async Task<List<ItemsPageData>> ListItemsAsync()
        {
            if (IsCachePopulated())
            {
                return GetCachedListItems();
            }

            List<ItemsPageData> itemsPages = new List<ItemsPageData>();
            using (HttpClient httpClient = new HttpClient())
            {
                int pageCount = 1;

                for (int page = 1; page <= pageCount; page++)
                {
                    Console.WriteLine($"Fetching page {page} of items...");
                    HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"https://nwdb.info/db/items/page/{page}.json"));

                    if (response.IsSuccessStatusCode)
                    {
                        string contentString = await response.Content.ReadAsStringAsync();
                        ItemsPage content = JsonSerializer.Deserialize<ItemsPage>(contentString, NWDB_JSON_SERIALIZER_OPTIONS);
                        Console.WriteLine($"Successfully deserialized response...");

                        if (content.Success)
                        {
                            itemsPages.AddRange(content.Data);
                            pageCount = content.PageCount;
                            Console.WriteLine($"Added items to collection...");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Error fetching page {page} of items... (${response.StatusCode}: {response.ReasonPhrase})");
                    }
                }
            }

            SaveAsJson(ITEM_CACHE_FILE_NAME, itemsPages);

            return itemsPages;
        }

        private void SaveAsJson(string fileName, object objectToSave)
        {
            Console.WriteLine($"Saving {fileName} to disk...");
            string json = JsonSerializer.Serialize(objectToSave);
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            File.WriteAllText(filePath, json);
        }

        private T LoadFromJson<T>(string fileName) where T : new()
        {
            Console.WriteLine($"Loading {fileName} from disk...");
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
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

        public class ItemsPage
        {
            public bool Success { get; set; }
            public int PageCount { get; set; }
            public List<ItemsPageData> Data { get; set; }
        }

        public class ItemsPageData
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string ItemType { get; set; }
            public bool NamedItem { get; set; }
            public string Name { get; set; }
        }
    }
}
