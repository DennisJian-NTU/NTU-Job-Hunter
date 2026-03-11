using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace NTUJobHunter
{
    class Program
    {
        // ---------------- [ 雲端配置 ] ----------------
        static string lineToken = Environment.GetEnvironmentVariable("LINE_TOKEN");
        // 請務必確認這裡貼的是 pub?output=csv 的連結
        static string userConfigCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTjAdjergagVBidinxnQhq9c7hf64SEyOgOX85HtE4tNUvtlKVHTZr8wB6TUp5pVaS92fMp8ZhMfiny/pub?gid=1519632381&single=true&output=csv";

        static List<string> myKeywords = new List<string>();
        static List<string> excludeKeywords = new List<string>();
        static HashSet<string> sentLinks = new HashSet<string>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 [Uni-Ask Cloud Hunter] 啟動中...");
            if (string.IsNullOrEmpty(lineToken)) {
                Console.WriteLine("❌ 找不到 LINE_TOKEN");
                return;
            }

            await LoadCloudConfigs();
            
            var targets = new List<SiteConfig> {
                new SiteConfig { Name = "PTT-Job", Url = "https://www.ptt.cc/atom/NTU-Job.xml" },
                new SiteConfig { Name = "NTU-Spotlight", Url = "https://www.ntu.edu.tw/spotlight/index.html" }
            };

            foreach (var site in targets) {
                await ScanSite(site);
            }
            Console.WriteLine("✅ 任務完成。");
        }

        static async Task LoadCloudConfigs() {
            try {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    string csvContent = await client.GetStringAsync(userConfigCsvUrl);
                    var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1);
                    var lastEntry = lines.LastOrDefault();
                    if (lastEntry != null) {
                        var parts = lastEntry.Split(',');
                        if (parts.Length >= 3) {
                            myKeywords = parts[2].Split('，', ',').Select(k => k.Trim().ToLower()).Where(k => !string.IsNullOrEmpty(k)).ToList();
                            if (parts.Length >= 4)
                                excludeKeywords = parts[3].Split('，', ',').Select(e => e.Trim().ToLower()).Where(e => !string.IsNullOrEmpty(e)).ToList();
                            Console.WriteLine($"📊 同步完成。關鍵字：{string.Join("/", myKeywords)}");
                        }
                    }
                }
            } catch (Exception ex) { Console.WriteLine($"⚠️ 同步失敗: {ex.Message}"); }
        }

        static async Task ScanSite(SiteConfig site) {
            try {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    var response = await client.GetAsync(site.Url);
                    if (!response.IsSuccessStatusCode) return;
                    
                    string content = await response.Content.ReadAsStringAsync();
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(content);

                    // PTT 用 entry 解析，台大用 a 解析
                    var nodes = site.Name.Contains("PTT") ? doc.DocumentNode.SelectNodes("//entry") : doc.DocumentNode.SelectNodes("//a");
                    if (nodes == null) return;

                    foreach (var node in nodes) {
                        string title = site.Name.Contains("PTT") ? node.SelectSingleNode(".//title")?.InnerText : node.InnerText;
                        string href = site.Name.Contains("PTT") ? node.SelectSingleNode(".//link")?.Attributes["href"]?.Value : node.Attributes["href"]?.Value;

                        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href)) continue;

                        string fullLink = href.StartsWith("http") ? href : new Uri(new Uri(site.Url), href).AbsoluteUri;
                        await CheckAndNotify(site.Name, title.Trim(), fullLink);
                    }
                }
            } catch { }
        }

        // 這是你剛才漏掉的「心臟零件」
        static async Task CheckAndNotify(string siteName, string title, string link) {
            string lowerTitle = title.ToLower();
            bool hasKey = myKeywords.Any(k => lowerTitle.Contains(k.Trim().ToLower()));
            bool hasExclude = excludeKeywords.Any(e => lowerTitle.Contains(e));

            if (hasKey && !hasExclude && !sentLinks.Contains(link)) {
                Console.WriteLine($"✨ 命中：{title}");
                await SendLineMessage($"\n🌟 [{siteName}] 發現好缺！\n\n標題：{title}\n連結：{link}");
                sentLinks.Add(link);
            }
        }

        static async Task SendLineMessage(string msg) {
            try {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + lineToken);
                    var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("message", msg) });
                    await client.PostAsync("https://notify-api.line.me/api/notify", content);
                }
            } catch { }
        }
    }
    class SiteConfig { public string Name; public string Url; }
}


