using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class FileSystem
    {
        private static string currentSchedule = "CurrentSchedule";
        private static string previousSchedule = "PreviousSchedule";
        private static string jsonPath = "Jsons";
        private static string updateJson = $"{jsonPath}\\Update.json";
        private static string groupsDirectory = $"{currentSchedule}\\GroupsSchedule";
        private static string teachersDirectory = $"{currentSchedule}\\TeachersSchedule";
        private static string audiencesDirectory = $"{currentSchedule}\\AudiencesSchedule";

        public void InitialCatalogs()
        {
            Directory.CreateDirectory(jsonPath);
            Directory.CreateDirectory(currentSchedule);
            Directory.CreateDirectory(groupsDirectory);
            Directory.CreateDirectory(teachersDirectory);
            Directory.CreateDirectory(audiencesDirectory);
            Directory.CreateDirectory(previousSchedule);
        }
        public async Task RecordUpdateDates(string currentDate, string updateDate)
        {
            JObject jObject = new JObject
            {
                ["currentDate"] = currentDate,
                ["updateDate"] = updateDate
            };
            string json = jObject.ToString();
            await File.WriteAllTextAsync(updateJson, json);
        }

        public async Task<string?> GetDate(string dateName)
        {
            string json = await File.ReadAllTextAsync(updateJson);

            JObject obj = JObject.Parse(json);

            string? date = obj.Value<string>(dateName);

            return date;
        }
        public async Task<string> GetFileNameFromDescriptionName(string key, bool isCurrent)
        {
            string rootFolder = isCurrent ? "CurrentSchedule" : "PreviousSchedule";
            var files = new (string Path, string Prefix, string Type)[]
            {
                ("Jsons/Groups.json", $"{rootFolder}\\GroupsSchedule\\", "Groups"),
                ("Jsons/Teachers.json", $"{rootFolder}\\TeachersSchedule\\", "Teachers"),
                ("Jsons/Audiences.json", $"{rootFolder}\\AudiencesSchedule\\", "Audiences")
            };

            foreach (var (filePath, prefix, type) in files)
            {
                if (!File.Exists(filePath)) continue;

                string json = await File.ReadAllTextAsync(filePath);
                var obj = JObject.Parse(json);
                var token = obj[key];
                if (token != null)
                {
                    string value = token.ToString();
                    return prefix + value;
                }
            }
            return string.Empty;
        }
    }
}
