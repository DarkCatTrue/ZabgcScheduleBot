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
            Directory.CreateDirectory("pastSchedule");
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
        public async Task<string?> GetCurrentDate()
        {
            string json = await File.ReadAllTextAsync(updateJson);

            JObject obj = JObject.Parse(json);

            string? currentDate = obj.Value<string>("currentDate");

            return currentDate;
        }
        public async Task<string?> GetUpdateDate()
        {
            string json = await File.ReadAllTextAsync(updateJson);

            JObject obj = JObject.Parse(json);

            string? updateDate = obj.Value<string>("updateDate");

            return updateDate;
        }
    }
}
