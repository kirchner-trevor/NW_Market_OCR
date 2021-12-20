using System;
using System.IO;
using System.Text.Json;

namespace NW_Market_Model
{
    public abstract class Repository<T> where T : RepositoryContents, new()
    {
        private string DataDirectory;

        public Repository() : this(null) { }

        public Repository(string dataDirectory)
        {
            DataDirectory = dataDirectory;
        }

        public T Contents { get; set; }

        public void SaveDatabaseToDisk()
        {
            Contents.Updated = DateTime.UtcNow;
            string json = JsonSerializer.Serialize(Contents);
            string filePath = GetDataBasePathOnDisk();
            string directoryPath = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(filePath, json);
        }

        public void LoadDatabaseFromDisk()
        {
            Contents = null;
            if (File.Exists(GetDataBasePathOnDisk()))
            {
                string json = File.ReadAllText(GetDataBasePathOnDisk());
                Contents = JsonSerializer.Deserialize<T>(json);
            }
            else
            {
                Contents = new T();
            }
        }

        public string GetDataBasePathOnDisk()
        {
            return Path.Combine(DataDirectory, GetFileName());
        }

        protected abstract string GetFileName();
    }

    public abstract class RepositoryContents
    {
        public DateTime Updated { get; set; }
    }
}
