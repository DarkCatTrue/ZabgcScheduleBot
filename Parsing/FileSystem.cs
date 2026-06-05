using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class FileSystem
    {
        private static string currentSchedule = "CurrentSchedule";
        private static string previousSchedule = "PreviousSchedule";
        private static string jsonPath = "Jsons";
        private static string updateJson = $"{jsonPath}\\Update.json";
        private static string groupsDirectory = $"{currentSchedule}\\Groups";
        private static string teachersDirectory = $"{currentSchedule}\\Teachers";
        private static string audiencesDirectory = $"{currentSchedule}\\Audiences";

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

        public async Task CopyOldSchedule()
        {
            await CopyFilesAsync($"{currentSchedule}\\Groups", $"{previousSchedule}\\Groups");
            await CopyFilesAsync($"{currentSchedule}\\Teachers", $"{previousSchedule}\\Teachers");
            await CopyFilesAsync($"{currentSchedule}\\Audiences", $"{previousSchedule}\\Audiences");
        }

        public static Task CopyFilesAsync(string sourceDir, string destDir)
        {
            return Task.Run(() =>
            {
                foreach (var filePath in Directory.GetFiles(sourceDir))
                {
                    var fileName = Path.GetFileName(filePath);
                    var destFile = Path.Combine(destDir, fileName);
                    File.Copy(filePath, destFile, overwrite: true);
                }
            });
        }

        public enum ScheduleType
        {
            None,
            Group,
            TeacherOrAudience
        }
        public async Task<(string Path, ScheduleType Type)> GetFileNameFromDescriptionName(string key, bool isCurrent)
        {
            string rootFolder = isCurrent ? "CurrentSchedule" : "PreviousSchedule";
            var files = new (string FilePath, string Prefix, ScheduleType Type)[]
            {
                ("Jsons/Groups.json", $"{rootFolder}\\GroupsSchedule\\", ScheduleType.Group),
                ("Jsons/Teachers.json", $"{rootFolder}\\TeachersSchedule\\", ScheduleType.TeacherOrAudience),
                ("Jsons/Audiences.json", $"{rootFolder}\\AudiencesSchedule\\", ScheduleType.TeacherOrAudience)
            };

            foreach (var (filePath, prefix, scheduleType) in files)
            {
                if (!File.Exists(filePath)) continue;

                string json = await File.ReadAllTextAsync(filePath);
                var obj = JObject.Parse(json);
                var token = obj[key];
                if (token != null)
                {
                    string value = token.ToString();
                    return (prefix + value, scheduleType);
                }
            }
            return (string.Empty, ScheduleType.None);
        }
    }
}
