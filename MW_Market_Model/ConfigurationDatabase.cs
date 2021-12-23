using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NW_Market_Model
{
    public class ConfigurationDatabase
    {
        private const string DATABASE_FILE_NAME = "configurationData.json";
        private string DataDirectory;

        public ConfigurationDatabase() : this(Directory.GetCurrentDirectory())
        {
        }

        public ConfigurationDatabase(string dataDirectory)
        {
            DataDirectory = dataDirectory;

            LoadDatabaseFromDisk();
        }

        public ConfigurationContent Content { get; set; }

        private void LoadDatabaseFromDisk()
        {
            Trace.WriteLine($"Loading {DATABASE_FILE_NAME} from disk...");
            if (File.Exists(GetDataBasePathOnDisk()))
            {
                string json = File.ReadAllText(GetDataBasePathOnDisk());
                Content = JsonSerializer.Deserialize<ConfigurationContent>(json);
            }
        }

        public void SaveDatabaseToDisk()
        {
            Content.Updated = DateTime.UtcNow;
            Trace.WriteLine($"Saving {DATABASE_FILE_NAME} to disk...");
            string json = JsonSerializer.Serialize(Content);
            File.WriteAllText(GetDataBasePathOnDisk(), json);
        }

        public string GetDataBasePathOnDisk()
        {
            return Path.Combine(DataDirectory, DATABASE_FILE_NAME);
        }

        public string GetDataBasePathOnServer()
        {
            return DATABASE_FILE_NAME;
        }

        public Dictionary<string, string> GetServerMergeMappings()
        {
            return new List<(string[], string)>
            {
                (new[] { "Cantahar"}, "Themiscrya"),
                (new[] { "Aztlan","Topan","Rosetau","Carabas"}, "Yaxche"),
                (new[] { "Xibalba","Locuta"}, "Morrow"),
                (new[] { "Cedar Forest","Blefuscu"}, "Krocylea"),
                (new[] { "Adlivun","Minda","Loloi","Kay Pacha","Euphrates","Brobdingnag"}, "Frislandia"),
                (new[] { "Mictlan"}, "Calnogor"),
                (new[] { "Ensipe"}, "Dominora"),
                (new[] { "Maldonada","Tumtum"}, "Pyrallis"),
                (new[] { "Ute-Yomigo","Chicomoztoc","Tamoanchan","Glubbdubdrib","Tulgey Wood"}, "Maramma"),
                (new[] { "Sitara","Hanan Pacha","Babilary","Argadnel","Barataria","Utensia"}, "Castle of Steel"),
                (new[] { "Bilskirnir","Caerleon","Dinas Emrys","Himinbjorg"}, "Silha"),
                (new[] { "Charybdis","Ker-Is","Lemonsyne","Macaria","Vlaanderen"}, "City of Brass"),
                (new[] { "Savoya","Maharloka","Onogoro","Dis","Asmaida","Vyraj"}, "Elaea"),
                (new[] { "Duguang","Falias","Moriai","Kaluwalhatian","Maca"}, "Scheria"),
                (new[] { "Elphame","Chinvat"}, "Ys"),
                (new[] { "Ruach","Tritonis","Norumbega"}, "Heliopolis"),
                (new[] { "Atvatabar","Andlang","Vidblain"}, "Zuvendis"),
                (new[] { "Royllo","Tlalocan"}, "Ogygia"),
                (new[] { "Vingolf","Pahurli","Nunne Chaha","Opona"}, "Pleroma"),
                (new[] { "Ydalir"}, "Valgrind"),
                (new[] { "Balnibari","Briedablik","Oponskoye"}, "Belovodye"),
                (new[] { "Takamagahara","Aarnivalkea"}, "Orun"),
                (new[] { "Vaitarani","Aeaea","Cantref Gwaelod","Emain Albach","Iriy","Metnal"}, "Orofena"),
                (new[] { "Flkvangr","Himavanta","Irminsul","Lokomorye","Karpathenburg"}, "Amano Iwato"),
                (new[] { "Neritum","Amsvartnir","Asphodel","Hufaidh","Nolandia","Onkeion"}, "Lin Lin"),
                (new[] { "Hy-Brasil","Nepenthe","Mu"}, "Omeyocan"),
                (new[] { "Brazir","Bentusle"}, "Ohonoo"),
                (new[] { "Antullia","Jezirat al Tennyn","Podesta","Maleas","Lesnik","Bembina"}, "Cibola"),
                (new[] { "Yinfu","Santu","Jinyuan","Youming","Elelin","Empi"}, "Zugen"),
                (new[] { "Boosaule","Britannula","Caeno","Cenculiana","Ceryneia","Deipnias"}, "Bouneima"),
                (new[] { "Dindymon","Diyu"}, "Difu"),
                (new[] { "Eupana","Galunlati","Harpagion","Hippocrene","Houssa","Houkang"}, "Trapalanda"),
                (new[] { "Idadalmunon","Jiuyou"}, "Jiuquan"),
                (new[] { "Linnunrata","Mulitefao","Ptolemais","Tarshish","Tayopa"}, "Kshira Sagara"),
                (new[] { "Uku Pacha","Phlegethon","Killaraus"}, "Neno Kuni"), // ,"Ophir"
                (new[] { "Vourukasha","Iardanes","Ashok Vatika","Satanazes"}, "Nidavellir"),
                (new[] { "Pohjola"}, "Diranda"),
                (new[] { "Mimisbrunnr","Ortygia","Parima"}, "Lintukoto"),
                (new[] { "Aukumea","Plancta"}, "Mag Mell"),
                (new[] { "Riallaro","Kronomo","Quanlu"}, "Rivadeneyra"),
                (new[] { "Sarragalla","Theleme","Tlillan-Tlapallan","Taenarum"}, "Ferri"),
                (new[] { "Rarohenga","Kokytos","Tipra Chonnlai","Saknussemm","Rupes Nigra"}, "Lilliput"),
                (new[] { "Samavasarana","Magnhalour","Tuonela","Sannikov Land"}, "Midian"),
                (new[] { "Karalku","Duzakh"}, "Adiri"),
                (new[] { "Lemuria","Khmun","Luggnagg"}, "Hawaiki"),
                (new[] { "Agartha","Eridu","Yama","Yomi","Lindalino"}, "Ryugu-Jo"),
                (new[] { "Zara", "Hsuan", "Kibu"}, "Barzakh"),
                (new[] { "Geatland", "Erythia"}, "Alakapuri"),
                (new[] { "Buzhou","Fusang","Laputa","Acherusia"}, "Yulbrada"),
                (new[] { "Kyphanta","Cynthus","Crommyon","Kressa"}, "Cretea"), // ,"Cretea"
                (new[] { "Myrkalfar","Batala","Puyok","Ibabawnon","Heorot"}, "Kabatakan"),
                (new[] { "Duat"}, "Asgard"),
                (new[] { "Eurytheia","Finias","Takshasila,"}, "Diana’s Grove"),
                (new[] { "Ekera","Gaunes"}, "Hellheim"),
                (new[] { "Argyre","Ismarus Eta","Harpinna"}, "Abaton"),
                (new[] { "Gorias","Montes Serrorum","Hippotae","Ismarus Theta"}, "Harmonia"), // "Eridanus",
                (new[] { "Slavna","Opar"}, "Alastor"),
                (new[] { "Tupia","Pointland","Wonderland"}, "Fae"),
                (new[] { "Silpium"}, "Albraca"),
                (new[] { "Ganzir","Aiolio","Vagon"}, "Learad"),
                (new[] { "Annwyn","Zerzura","Damalise","Nowhere"}, "Sanor"),
                (new[] { "Vimur","Thrudheim","Jansenia"}, "Antillia"),
                (new[] { "Ravenal"}, "Styx"),
                (new[] { "Saena","Urdarbrunn","Penglai","Carcosa"}, "Glyn Cagny"),
                (new[] { "Alfheim","Vainola","Bermeja"}, "Saba"), // ,"Ophir"
                (new[] { "Gladsheim","Vaikuntha"}, "Aaru"),
                (new[] { "Panchaia","Nastrond"}, "Pryandia"),
                (new[] { "Noatun","Suddene"}, "Avalon"),
                (new[] { "Nysos","Nexdorea"}, "Ramaja"),
                (new[] { "Tabor Island"}, "Nysa"),
                (new[] { "Melinde"}, "Ife"),
                (new[] { "Bakhu","Estotiland"}, "Lyonesse"),
                (new[] { "Groclant","Erewhon","Balita","Inferni","Thule"}, "Tir Na Nog"),
                (new[] { "Idavoll","Runeberg"}, "Barri"),
                (new[] { "Naxos","Kantia","Jacquet"}, "Murias"),
                (new[] { "Icaria"}, "Bifrost"),
                (new[] { "Tanje","Pellucidar","Kianida"}, "Nav"),
                (new[] { "Pulotu"}, "Kor"),
                (new[] { "Kvenland","Frisland","Larissa"}, "Bengodi"),
                (new[] { "Iroko","Ketumati","Una-bara","Petermannland","Pepys"}, "Bensalem"),
                (new[] { "Mayda"}, "Karkar"),
                (new[] { "Wachusette","Zanara"}, "Bran"),
                (new[] { "Jotunheim"}, "Aepyornis"),
                (new[] { "Mardi"}, "Brittia"),
                (new[] { "Ishtakar","Metsola","Glitnir","Kalevala","Zu-Vendis","Phaeacia"}, "Caer Sidi"),
                (new[] { "Jumala","Kaloon","Lethe"}, "Charadra"),
                (new[] { "Caprona","Malva"}, "Ship-Trap"),
                (new[] { "Corbenic","Goldenthal","Kioram"}, "Tartarus"),
                (new[] { "Lesath","Marsic"}, "Delphnius"),
                (new[] { "Chryse","Dvaraka"}, "Mandara"),
                (new[] { "Banoic","Cassipa","Canis"}, "Arcturus"),
                (new[] { "Caspak","Altruria","Barsoom"}, "Evonium"),
                (new[] { "Fomax"}, "Nericus"), // "Eridanus",
                (new[] { "Pavlopetri","Kerguelen","Heracleion","Ravenspurn","Muziris","Otuken"}, "Balanjar"),
                (new[] { "Emathia","Hellopia","Quivira","Vicina","Apis","Swevenham"}, "Perseus"),
                (new[] { "Hydrus","Lepus","Menkar","Nembus","Pollux","Serpens"}, "Vega"),
                (new[] { "Subra","Vineta","Yourang","Youda","Yinjian"}, "Ursa"),
                (new[] { "Vyrij","Wood Perilous","Xarayes","Alioth","Antares"}, "Aquila"),
                (new[] { "Canopis","Cygnus","Grus","Izar","Karaka"}, "Dry Tree"),
                (new[] { "Acrus"}, "Acamar"),
            }
            .SelectMany(_ => _.Item1.Select(s => (s, _.Item2)))
            .ToDictionary(_ => CreateServerIdFromName(_.Item1), _ => CreateServerIdFromName(_.Item2));
        }

        private string CreateServerIdFromName(string serverName)
        {
            return serverName?.Trim()?.ToLowerInvariant()?.Replace(" ", "-").PadLeft(3, '-');
        }
    }

    public class ConfigurationContent
    {
        public List<ServerListInfo> ServerList { get; set; }
        public DateTime Updated { get; set; }
    }

    public class ServerListInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WorldSet { get; set; }
        public string Region { get; set; }
        public int? Listings { get; set; }
    }
}
