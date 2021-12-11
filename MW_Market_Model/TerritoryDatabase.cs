using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NW_Market_Model
{
    public class TerritoryDatabase
    {
        private readonly string filePath = Path.Combine("Resources", "territories.json");

        private List<Territory> territories;
        private Dictionary<string, Territory> territoryNameLookup;

        public TerritoryDatabase()
        {
            territories = JsonSerializer.Deserialize<List<Territory>>(File.ReadAllText(filePath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            territoryNameLookup = territories
                .SelectMany(_ => _.Names.Distinct().Select(name => new { Territory = _, Name = name }))
                .ToDictionary(_ => _.Name.ToLowerInvariant(), _ => _.Territory);
        }

        public Territory Lookup(string territoryName)
        {
            if (territoryName == null)
            {
                return null;
            }

            return territoryNameLookup.GetValueOrDefault(territoryName.ToLowerInvariant(), null);
        }

        public List<Territory> List()
        {
            return territories;
        }
    }

    public class Territory
    {
        public string TerritoryId { get; set; }
        public string[] Names { get; set; }
    }
}
