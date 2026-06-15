using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class Finder
    {
        private readonly string _basePath;
        private string GetFullPath(string relativePath) => Path.Combine(_basePath, relativePath);

        public Finder()
        {
            _basePath = AppContext.BaseDirectory;
        }

        public async Task<bool> Find(string input)
        {
            if (await ExistsInJsonFile("Jsons/Groups.json", input)) return true;
            if (await ExistsInJsonFile("Jsons/Teachers.json", input)) return true;
            if (await ExistsInJsonFile("Jsons/Audiences.json", input)) return true;
            return false;
        }

        private async Task<bool> ExistsInJsonFile(string relativePath, string key)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                if (!File.Exists(fullPath))
                    return false;
                string json = await File.ReadAllTextAsync(fullPath);
                JObject obj = JObject.Parse(json);
                return obj.ContainsKey(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении или парсинге {relativePath}: {ex.Message}");
                return false;
            }
        }
    }
}