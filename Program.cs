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
        // ---------------- [ 雲端配置區 ] ----------------
        // 1. 從 GitHub Secrets 讀取 Token
        static string lineToken = Environment.GetEnvironmentVariable("LINE_TOKEN");

        // 2. 你的 Google 試算表 CSV 網址 (請填入你發布的 CSV 連結)
        static string userConfigCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTjAdjergagVBidinxnQhq9c7hf64SEyOgOX85HtE4tNUvtlKVHTZr8wB6TUp5pVaS92fMp8ZhMfiny/pub?gid=1519632381&single=true&output=csv";

        // 3. 預填表單範本 (將 {0} 替換為 ID)
        static string magicLinkTemplate = "https://docs.google.com/forms/d/e/1FAIpQLScwgSg1tB2QdQKV4FXljTq1ujjfNxm5XVY7x456nYQugGFARA/viewform?usp=pp_url&entry.1698759269={0}";

        // ---------------- [ 變數區 ] ----------------
        static List<string> myKeywords = new List<string>();
        static List<string> excludeKeywords = new List<string>();
        static HashSet<string> sentLinks = new HashSet<string>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 [Uni-Ask Cloud Hunter] 啟動中...");

            if (string.IsNullOrEmpty(lineToken))
            {
                Console.WriteLine("❌ 錯誤：找不到 LINE_TOKEN 環境變數。請檢查 GitHub Secrets 設定。");
                return;
            }

            await LoadCloudConfigs();

            var targets = new List<SiteConfig> {
                new SiteConfig { Name = "PTT-Job", Url = "https://www.ptt.cc/bbs/NTU-Job/index.html" },
                new SiteConfig { Name = "NTU-Spotlight", Url = "https://www.ntu.edu.tw/spotlight/index.html" }
            };

            foreach (var site in targets)
            {
                await ScanSite(site);
            }

            Console.WriteLine("✅ 任務完成。");
        }

        static async Task LoadCloudConfigs()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    string csvContent = await client.GetStringAsync(userConfigCsvUrl);
                    var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1);
                    var lastEntry = lines.LastOrDefault();

                    if (lastEntry != null)
                    {
                        var parts = lastEntry.Split(',');
                        if (parts.Length >= 3)
                        {
                            myKeywords = parts[2].Split('，', ',').Select(k => k.Trim().ToLower()).Where(k => !string.IsNullOrEmpty(k)).ToList();
                            if (parts.Length >= 4)
                                excludeKeywords = parts[3].Split('，', ',').Select(e => e.Trim().ToLower()).Where(e => !string.IsNullOrEmpty(e)).ToList();
                            Console.WriteLine($"📊 同步完成。關鍵字：{string.Join("/", myKeywords)}");
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"⚠️ 配置同步失敗: {ex.Message}"); }
        }

        static async Task ScanSite(SiteConfig site)
{
    // 1. 強制換成 XML 入口，這在 GitHub 跑最穩
    string targetUrl = site.Name.Contains("PTT") 
        ? "https://www.ptt.cc/atom/NTU-Job.xml" 
        : "https://www.ntu.edu.tw/spotlight/index.html";

    try {
        // 2. 簡化 Handler，XML 通常不需要複雜偽裝
        using (var client = new HttpClient()) {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var response = await client.GetAsync(targetUrl);
            
            if (!response.IsSuccessStatusCode) {
                Console.WriteLine($"   ❌ {site.Name} 連線失敗: {response.StatusCode}");
                return;
            }

            string content = await response.Content.ReadAsStringAsync();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(content);

            // 3. XML 標籤解析邏輯
            var nodes = site.Name.Contains("PTT") 
                ? doc.DocumentNode.SelectNodes("//entry") 
                : doc.DocumentNode.SelectNodes("//a");

            if (nodes == null) return;

            foreach (var node in nodes) {
                string title = site.Name.Contains("PTT") ? node.SelectSingleNode(".//title")?.InnerText : node.InnerText;
                string href = site.Name.Contains("PTT") ? node.SelectSingleNode(".//link")?.Attributes["href"]?.Value : node.Attributes["href"]?.Value;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href)) continue;

                // 進行關鍵字比對... (後續邏輯維持原樣)
                await CheckAndNotify(site.Name, title, href);
            }
        }
    } catch (Exception ex) { Console.WriteLine($"   ❌ {site.Name} 異常: {ex.Message}"); }
}

        static async Task SendLineMessage(string msg)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + lineToken);
                    var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("message", msg) });
                    await client.PostAsync("https://notify-api.line.me/api/notify", content);
                }
            }
            catch { }
        }
    }
    class SiteConfig { public string Name; public string Url; }

}

