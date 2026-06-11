using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace ZabgcScheduleBot.Parsing
{
    public class Parse
    {
        FileSystem fileSystem = new FileSystem();
        public async Task<string[]> GetDates()
        {
            string urlSchedule = DotNetEnv.Env.GetString("Url_Schedule");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var web = new HtmlWeb();
            web.OverrideEncoding = Encoding.GetEncoding(1251);
            var doc = await web.LoadFromWebAsync($"{urlSchedule}/hg.htm");

            var CurrentDate = doc.DocumentNode.SelectSingleNode("//li[@class='zgr']");
            var UpdateDate = doc.DocumentNode.SelectSingleNode("//div[@class='ref']");

            string dateText = CurrentDate.InnerText.Trim();
            string updateText = UpdateDate.InnerText.Trim();

            return [dateText, updateText];
        }
        public async Task SaveAllData()
        {
            await SaveJsons();
            await SaveAllPages();
            await RecordUpdateDates();
            await fileSystem.CopyOldSchedule();
        }
        public async Task SaveDataBeforeNotification()
        {
            await fileSystem.CopyOldSchedule();
            await SaveAllPages();
        }
        public async Task RecordUpdateDates()
        {
            string[] dates = new string[1];
            dates = await GetDates();
            string currentDate = dates[0];
            string updateDate = dates[1];
            await fileSystem.RecordDates(currentDate, updateDate);
        }

        public async Task SaveJsons()
        {
            await SaveData("cg.htm", "Jsons\\Groups.json");
            await SaveData("cp.htm", "Jsons\\Teachers.json");
            await SaveData("ca.htm", "Jsons\\Audiences.json");
        }

        // Сохранение списка групп, аудиторий, преподавателей в json
        private async Task SaveData(string fileName, string jsonName)
        {
            string urlSchedule = DotNetEnv.Env.GetString("Url_Schedule");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var web = new HtmlWeb();
            web.OverrideEncoding = Encoding.GetEncoding(1251);
            var doc = await web.LoadFromWebAsync($"{urlSchedule}/{fileName}");

            var dict = new Dictionary<string, string>();

            foreach (var node in doc.DocumentNode.SelectNodes("//a[@class='z0']"))
            {
                dict[node.InnerText.Trim()] = node.GetAttributeValue("href", "");
            }

            await File.WriteAllTextAsync($"{jsonName}", JsonConvert.SerializeObject(dict));
        }

        public async Task SaveAllPages()
        {
            await SaveSchedulePages("Jsons\\Groups.json", "CurrentSchedule\\Groups");
            await SaveSchedulePages("Jsons\\Audiences.json", "CurrentSchedule\\Audiences");
            await SaveSchedulePages("Jsons\\Teachers.json", "CurrentSchedule\\Teachers");
        }

        public async Task SaveSchedulePages(string jsonName, string destinaton)
        {
            string urlSchedule = DotNetEnv.Env.GetString("Url_Schedule");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var web = new HtmlWeb();
            web.OverrideEncoding = Encoding.GetEncoding(1251);

            string json = await File.ReadAllTextAsync(jsonName);
            var jObject = JObject.Parse(json);

            foreach (var property in jObject.Properties())
            {
                JToken value = property.Value;
                var doc = await web.LoadFromWebAsync($"{urlSchedule}/{value.ToString()}");
                string path = $"{destinaton}\\{value.ToString()}";
                doc.Save(path);
            }
        }

        public async Task<string> GetScheduleFromWeb(string fileName)
        {
            string url = $"{DotNetEnv.Env.GetString("Url_Schedule")}/{fileName}";
            var doc = await LoadHtmlFromWebAsync(url);
            return ParseSchedule(doc);
        }

        public async Task<string> GetScheduleFromFile(string filepath)
        {
            var doc = LoadHtmlFromFile(filepath);
            return ParseSchedule(doc);
        }

        public async Task<bool> ScheduleIsDifferent(string fileName, string filePath)
        {
            string webSchedule = await GetScheduleFromWeb(fileName);
            string fileSchedule = await GetScheduleFromFile(filePath);
            if (webSchedule != fileSchedule)
            {
                return true;
            }
            return false;
        }

        private async Task<HtmlDocument> LoadHtmlFromWebAsync(string url)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var web = new HtmlWeb { OverrideEncoding = Encoding.GetEncoding(1251) };
            return await web.LoadFromWebAsync(url);
        }

        private HtmlDocument LoadHtmlFromFile(string fileName)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var doc = new HtmlDocument();
            doc.Load(fileName, Encoding.GetEncoding(1251));
            return doc;

        }

        public string ParseSchedule(HtmlDocument doc)
        {
            string descriptionName = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?[0..]?.Trim() ?? "";

            var table = doc.DocumentNode.SelectSingleNode("//table[@class='inf']");
            if (table == null) return string.Empty;

            foreach (var br in table.SelectNodes(".//br") ?? Enumerable.Empty<HtmlNode>())
                br.ParentNode.ReplaceChild(doc.CreateTextNode(" "), br);
            foreach (var nul in table.SelectNodes(".//*[@class='nul']") ?? Enumerable.Empty<HtmlNode>())
                nul.InnerHtml = "Нет пары";

            var allRows = table.SelectNodes(".//tr")?.ToList();
            if (allRows == null || allRows.Count == 0) return string.Empty;

            int startIndex = -1;
            for (int i = 0; i < allRows.Count; i++)
            {
                if (allRows[i].SelectSingleNode(".//td[@rowspan='6']") != null)
                {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex == -1) return string.Empty;

            var rows = allRows.Skip(startIndex).Take(6);

            string date = allRows[startIndex].SelectSingleNode(".//td[@rowspan='6']")?.InnerHtml?.Replace("<br>", " ")?.Trim() ?? "";

            var cells = new List<string[]>();
            foreach (var row in rows)
            {
                var rowCells = row.SelectNodes(".//td[not(@class='hd' and @rowspan='6')]")
                    ?.Select(td => td.InnerText.Trim())
                    .Where(text => !string.IsNullOrEmpty(text))
                    .ToArray();
                if (rowCells?.Length > 0)
                    cells.Add(rowCells);
            }

            var message = new StringBuilder();
            message.AppendLine(descriptionName);
            message.AppendLine($"Дата: {date}");
            message.AppendLine();

            foreach (var row in cells)
            {
                message.AppendLine(string.Join(" | ", row));
            }

            return message.ToString();
        }
        public enum ScheduleType
        {
            Group,
            TeacherOrAudience,
        }
    }

}