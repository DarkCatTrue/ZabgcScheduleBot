using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class Finder
    {
        public async Task<bool> Find(string input)
        {
            if (await ExistsInJsonFile("Jsons\\Groups.json", input)) return true;
            if (await ExistsInJsonFile("Jsons\\Teachers.json", input)) return true;
            if (await ExistsInJsonFile("Jsons\\Audiences.json", input)) return true;

            return false;
        }

        private async Task<bool> ExistsInJsonFile(string filePath, string key)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                JObject obj = JObject.Parse(json);
                return obj.ContainsKey(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении или парсинге {filePath}: {ex.Message}");
                return false;
            }
        }
    }
}
