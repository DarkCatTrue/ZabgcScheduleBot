using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace ZabgcScheduleBot.Parsing
{
    public class Parse
    {
        private readonly FileSystem _fileSystem;
        private readonly string _basePath;
        public Parse(FileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _basePath = AppContext.BaseDirectory;
        }

        private string GetFullPath(string relativePath) => Path.Combine(_basePath, relativePath);

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
            await _fileSystem.CopyOldSchedule();
        }

        public async Task SaveDataBeforeNotification()
        {
            await _fileSystem.CopyOldSchedule();
            await SaveAllPages();
        }

        public async Task RecordUpdateDates()
        {
            string[] dates = await GetDates();
            string currentDate = dates[0];
            string updateDate = dates[1];
            await _fileSystem.RecordDates(currentDate, updateDate);
        }

        public async Task SaveJsons()
        {
            await SaveData("cg.htm", GetFullPath("Jsons/Groups.json"));
            await SaveData("cp.htm", GetFullPath("Jsons/Teachers.json"));
            await SaveData("ca.htm", GetFullPath("Jsons/Audiences.json"));
        }

        private async Task SaveData(string fileName, string jsonPath)
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

            string directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(jsonPath, JsonConvert.SerializeObject(dict));
        }

        public async Task SaveAllPages()
        {
            await SaveSchedulePages(GetFullPath("Jsons/Groups.json"), GetFullPath("CurrentSchedule/Groups"));
            await SaveSchedulePages(GetFullPath("Jsons/Audiences.json"), GetFullPath("CurrentSchedule/Audiences"));
            await SaveSchedulePages(GetFullPath("Jsons/Teachers.json"), GetFullPath("CurrentSchedule/Teachers"));
        }

        public async Task SaveSchedulePages(string jsonPath, string destinationFolder)
        {
            string urlSchedule = DotNetEnv.Env.GetString("Url_Schedule");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var web = new HtmlWeb();
            web.OverrideEncoding = Encoding.GetEncoding(1251);

            string json = await File.ReadAllTextAsync(jsonPath);
            var jObject = JObject.Parse(json);

            foreach (var property in jObject.Properties())
            {
                JToken value = property.Value;
                var doc = await web.LoadFromWebAsync($"{urlSchedule}/{value.ToString()}");
                string filePath = Path.Combine(destinationFolder, value.ToString());
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                doc.Save(filePath);
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
            return webSchedule != fileSchedule;
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