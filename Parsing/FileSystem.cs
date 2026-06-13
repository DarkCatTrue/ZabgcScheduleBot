using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class FileSystem
    {
        private static readonly string CurrentScheduleDir = "CurrentSchedule";
        private static readonly string PreviousScheduleDir = "PreviousSchedule";
        private static readonly string JsonDir = "Jsons";
        private static string JsonPath => Path.Combine(Directory.GetCurrentDirectory(), JsonDir);
        private static string UpdateJson => Path.Combine(JsonPath, "Update.json");
        private static string GroupsDirectory => Path.Combine(CurrentScheduleDir, "Groups");
        private static string TeachersDirectory => Path.Combine(CurrentScheduleDir, "Teachers");
        private static string AudiencesDirectory => Path.Combine(CurrentScheduleDir, "Audiences");

        public void InitialCatalogs()
        {
            Directory.CreateDirectory(JsonPath);
            Directory.CreateDirectory(CurrentScheduleDir);
            Directory.CreateDirectory(GroupsDirectory);
            Directory.CreateDirectory(TeachersDirectory);
            Directory.CreateDirectory(AudiencesDirectory);

            Directory.CreateDirectory(PreviousScheduleDir);
            Directory.CreateDirectory(Path.Combine(PreviousScheduleDir, "Groups"));
            Directory.CreateDirectory(Path.Combine(PreviousScheduleDir, "Teachers"));
            Directory.CreateDirectory(Path.Combine(PreviousScheduleDir, "Audiences"));
        }

        public async Task RecordDates(string currentDate, string updateDate)
        {
            JObject jObject = new JObject
            {
                ["currentDate"] = currentDate,
                ["updateDate"] = updateDate
            };
            string json = jObject.ToString();
            await File.WriteAllTextAsync(UpdateJson, json);
        }

        public async Task<string?> GetDate(string dateName)
        {
            string json = await File.ReadAllTextAsync(UpdateJson);
            JObject obj = JObject.Parse(json);
            string? date = obj.Value<string>(dateName);
            return date;
        }

        public async Task CopyOldSchedule()
        {
            await CopyFilesAsync(GroupsDirectory, Path.Combine(PreviousScheduleDir, "Groups"));
            await CopyFilesAsync(TeachersDirectory, Path.Combine(PreviousScheduleDir, "Teachers"));
            await CopyFilesAsync(AudiencesDirectory, Path.Combine(PreviousScheduleDir, "Audiences"));
        }

        private static Task CopyFilesAsync(string sourceDir, string destDir)
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

        public async Task<string> GetFileNameFromDescriptionName(string key)
        {
            string rootFolder = CurrentScheduleDir;
            var files = new (string FilePath, string FolderPrefix)[]
            {
                (Path.Combine(JsonDir, "Groups.json"), Path.Combine(rootFolder, "Groups")),
                (Path.Combine(JsonDir, "Teachers.json"), Path.Combine(rootFolder, "Teachers")),
                (Path.Combine(JsonDir, "Audiences.json"), Path.Combine(rootFolder, "Audiences"))
            };

            foreach (var (filePath, folderPrefix) in files)
            {
                if (!File.Exists(filePath)) continue;

                string json = await File.ReadAllTextAsync(filePath);
                var obj = JObject.Parse(json);
                var token = obj[key];
                if (token != null)
                {
                    string value = token.ToString();
                    return value;
                }
            }
            return string.Empty;
        }

        public async Task<(string Path, ScheduleType Type)> GetFullPathFromDescriptionName(string key, bool isCurrent)
        {
            string rootFolder = isCurrent ? CurrentScheduleDir : PreviousScheduleDir;
            var files = new (string FilePath, string FolderPrefix, ScheduleType Type)[]
            {
                (Path.Combine(JsonDir, "Groups.json"), Path.Combine(rootFolder, "Groups"), ScheduleType.Group),
                (Path.Combine(JsonDir, "Teachers.json"), Path.Combine(rootFolder, "Teachers"), ScheduleType.TeacherOrAudience),
                (Path.Combine(JsonDir, "Audiences.json"), Path.Combine(rootFolder, "Audiences"), ScheduleType.TeacherOrAudience)
            };

            foreach (var (filePath, folderPrefix, scheduleType) in files)
            {
                if (!File.Exists(filePath)) continue;

                string json = await File.ReadAllTextAsync(filePath);
                var obj = JObject.Parse(json);
                var token = obj[key];
                if (token != null)
                {
                    string fileName = token.ToString();
                    string fullPath = Path.Combine(folderPrefix, fileName);
                    return (fullPath, scheduleType);
                }
            }
            return (string.Empty, ScheduleType.None);
        }
    }
}