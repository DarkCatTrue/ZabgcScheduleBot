using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace ZabgcScheduleBot
{
    public class Parse
    {
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
            await SaveData("cg.htm", "Jsons\\Groups.json");
            await SaveData("cp.htm", "Jsons\\Teachers.json");
            await SaveData("ca.htm", "Jsons\\Audiences.json");
        }

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

        public async Task<string> GetScheduleFromFile(string fileName)
        {
            var doc = LoadHtmlFromFile(fileName);
            return ParseSchedule(doc);
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

        private string ParseSchedule(HtmlDocument doc)
        {
            string group = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?[8..]?.Trim() ?? "";
            string date = doc.DocumentNode.SelectSingleNode("//*[@class='hd' and @rowspan='6']")?.InnerHtml?.Replace("<br>", " ")?.Trim() ?? "";

            var table = doc.DocumentNode.SelectSingleNode("//table[@class='inf']");
            if (table == null) return string.Empty;

            foreach (var br in table.SelectNodes(".//br") ?? Enumerable.Empty<HtmlNode>())
                br.ParentNode.ReplaceChild(doc.CreateTextNode(" "), br);

            foreach (var nul in table.SelectNodes(".//*[@class='nul']") ?? Enumerable.Empty<HtmlNode>())
                nul.InnerHtml = "Нет пары";

            var cells = new List<string[]>();
            var rows = table.SelectNodes(".//tr")?.Skip(3).Take(6);

            var message = new StringBuilder();
            message.AppendLine($"Группа: {group}");
            message.AppendLine($"Дата: {date}");
            message.AppendLine();

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var rowCells = row.SelectNodes(".//td[not(@class='hd' and @rowspan='6')]")
                        ?.Select(td => td.InnerText.Trim())
                        .Where(text => !string.IsNullOrEmpty(text))
                        .ToArray();

                    if (rowCells?.Length > 0)
                        cells.Add(rowCells);
                }
            }

            foreach (var row in cells)
            {
                message.AppendLine(string.Join(" | ", row));
            }

            return message.ToString();
        }
    }
}