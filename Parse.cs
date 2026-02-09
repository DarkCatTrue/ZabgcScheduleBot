using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
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


            Console.WriteLine($"{dateText}, {updateText}");

            return [dateText, updateText];
        }

        public async Task SaveData(string fileName, string jsonName)
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

            File.WriteAllText($"{jsonName}", JsonConvert.SerializeObject(dict));
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

        public  async Task <(string group, string date, List<string[]> cells)> GetScheduleFromWeb(string fileName)
        {
            string urlSchedule = DotNetEnv.Env.GetString("Url_Schedule");

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var web = new HtmlWeb();
            web.OverrideEncoding = Encoding.GetEncoding(1251);
            var doc = await web.LoadFromWebAsync($"{urlSchedule}/{fileName}");
            string group = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Substring(8)?.Trim() ?? "";

            string date = doc.DocumentNode.SelectSingleNode("//*[@class='hd' and @rowspan='6']")?.InnerHtml?.Replace("<br>", " ").Trim() ?? "";

            var table = doc.DocumentNode.SelectSingleNode("//table[@class='inf']");

            table.SelectNodes(".//br")?.ToList().ForEach(br =>
                br.ParentNode.ReplaceChild(doc.CreateTextNode(" "), br));

            table.SelectNodes(".//*[@class='nul']")?.ToList().ForEach(nul =>
                nul.InnerHtml = "Нет пары");

            var cells = new List<string[]>();

            table.SelectNodes(".//tr")?.Skip(3).Take(6).ToList().ForEach(row =>
            {
                var rowCells = row.SelectNodes(".//td[not(@class='hd' and @rowspan='6')]")?
                    .Select(td => td.InnerText.Trim())
                    .ToArray();

                if (rowCells != null && rowCells.Length > 0)
                {
                    cells.Add(rowCells);
                }
            });
            return (group, date, cells);
        }

        public async Task <(string group, string date, List<string[]> cells)> GetScheduleFromFile(string fileName)
        {
            var doc = new HtmlDocument();
            
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            doc.Load(fileName, Encoding.GetEncoding(1251));

            string group = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Substring(8)?.Trim() ?? "";
            
            string date = doc.DocumentNode.SelectSingleNode("//*[@class='hd' and @rowspan='6']")?.InnerHtml?.Replace("<br>", " ").Trim() ?? "";

            var table = doc.DocumentNode.SelectSingleNode("//table[@class='inf']");

            table.SelectNodes(".//br")?.ToList().ForEach(br =>
                br.ParentNode.ReplaceChild(doc.CreateTextNode(" "), br));

            table.SelectNodes(".//*[@class='nul']")?.ToList().ForEach(nul =>
                nul.InnerHtml = "Нет пары");

            var cells = new List<string[]>();

            table.SelectNodes(".//tr")?.Skip(3).Take(6).ToList().ForEach(row =>
            {
                var rowCells = row.SelectNodes(".//td[not(@class='hd' and @rowspan='6')]")?
                    .Select(td => td.InnerText.Trim())
                    .ToArray();

                if (rowCells != null && rowCells.Length > 0)
                {
                    cells.Add(rowCells);
                }
            });
            return (group, date, cells);
        }
    }
}
