using Newtonsoft.Json.Linq;

namespace ZabgcScheduleBot.Parsing
{
    public class FileSystem
    {
        private static readonly string CurrentScheduleDir = "CurrentSchedule";
        private static readonly string PreviousScheduleDir = "PreviousSchedule";
        private static readonly string JsonDir = "Jsons";

        private static string BasePath => AppContext.BaseDirectory;

        private static string JsonPath => Path.Combine(BasePath, JsonDir);
        private static string UpdateJson => Path.Combine(JsonPath, "Update.json");

        private static string CurrentSchedulePath => Path.Combine(BasePath, CurrentScheduleDir);
        private static string GroupsDirectory => Path.Combine(CurrentSchedulePath, "Groups");
        private static string TeachersDirectory => Path.Combine(CurrentSchedulePath, "Teachers");
        private static string AudiencesDirectory => Path.Combine(CurrentSchedulePath, "Audiences");

        private static string PreviousSchedulePath => Path.Combine(BasePath, PreviousScheduleDir);

        public void InitialCatalogs()
        {
            Directory.CreateDirectory(JsonPath);
            Directory.CreateDirectory(CurrentSchedulePath);
            Directory.CreateDirectory(PreviousSchedulePath);

            Directory.CreateDirectory(GroupsDirectory);
            Directory.CreateDirectory(TeachersDirectory);
            Directory.CreateDirectory(AudiencesDirectory);

            Directory.CreateDirectory(Path.Combine(PreviousSchedulePath, "Groups"));
            Directory.CreateDirectory(Path.Combine(PreviousSchedulePath, "Teachers"));
            Directory.CreateDirectory(Path.Combine(PreviousSchedulePath, "Audiences"));
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
            await CopyFilesAsync(GroupsDirectory, Path.Combine(PreviousSchedulePath, "Groups"));
            await CopyFilesAsync(TeachersDirectory, Path.Combine(PreviousSchedulePath, "Teachers"));
            await CopyFilesAsync(AudiencesDirectory, Path.Combine(PreviousSchedulePath, "Audiences"));
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
            var files = new (string FilePath, string FolderPrefix)[]
            {
                (Path.Combine(BasePath, JsonDir, "Groups.json"), Path.Combine(BasePath, CurrentScheduleDir, "Groups")),
                (Path.Combine(BasePath, JsonDir, "Teachers.json"), Path.Combine(BasePath, CurrentScheduleDir, "Teachers")),
                (Path.Combine(BasePath, JsonDir, "Audiences.json"), Path.Combine(BasePath, CurrentScheduleDir, "Audiences"))
            };

            foreach (var (filePath, folderPrefix) in files)
            {
                if (!File.Exists(filePath)) continue;

                string json = await File.ReadAllTextAsync(filePath);
                var obj = JObject.Parse(json);
                var token = obj[key];
                if (token != null)
                {
                    return token.ToString();
                }
            }
            return string.Empty;
        }

        public async Task<(string Path, ScheduleType Type)> GetFullPathFromDescriptionName(string key, bool isCurrent)
        {
            string rootFolder = isCurrent ? CurrentScheduleDir : PreviousScheduleDir;
            var files = new (string FilePath, string FolderPrefix, ScheduleType Type)[]
            {
                (Path.Combine(BasePath, JsonDir, "Groups.json"), Path.Combine(BasePath, rootFolder, "Groups"), ScheduleType.Group),
                (Path.Combine(BasePath, JsonDir, "Teachers.json"), Path.Combine(BasePath, rootFolder, "Teachers"), ScheduleType.TeacherOrAudience),
                (Path.Combine(BasePath, JsonDir, "Audiences.json"), Path.Combine(BasePath, rootFolder, "Audiences"), ScheduleType.TeacherOrAudience)
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