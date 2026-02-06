using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Text;

namespace ZabgcScheduleBot
{
    public class Parse
    {
        public static async Task <string[]> GetDates()
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
        public static async Task GetData(string fileName, string jsonName) 
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
    }
}
