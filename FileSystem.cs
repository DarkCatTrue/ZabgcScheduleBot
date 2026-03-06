using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace ZabgcScheduleBot
{
    public class FileSystem
    {
        private static string currentSchedule = "CurrentSchedule";
        private static string jsonPath = "Jsons";
        private static string updateJson = $"{jsonPath}\\Update.json";
        public void InitialCatalogs()
        {
            Directory.CreateDirectory(jsonPath);
            Directory.CreateDirectory(currentSchedule);
            Directory.CreateDirectory($"{currentSchedule}\\GroupsSchedule");
            Directory.CreateDirectory($"{currentSchedule}\\TeachersSchedule");
            Directory.CreateDirectory($"{currentSchedule}\\AudiencesSchedule");
            Directory.CreateDirectory("PastSchedule");
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
    }
}
