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
    await LoadCloudConfigs();
    
    // 強制手寫測試對象
    await ScanSite(new SiteConfig { Name = "PTT-TEST", Url = "https://www.ptt.cc/atom/NTU-Job.xml" });
    
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

        static async Task ScanSite(SiteConfig site)
{
    Console.WriteLine($"🌐 開始掃描網址: {site.Url}"); // 如果這行沒印出來，就是 Main 呼叫失敗
    try {
        using (var client = new HttpClient()) {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var response = await client.GetAsync(site.Url);
            Console.WriteLine($"📡 連線狀態: {response.StatusCode}");
            
            string content = await response.Content.ReadAsStringAsync();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(content);

            var nodes = doc.DocumentNode.SelectNodes("//entry"); 
            if (nodes == null) {
                Console.WriteLine("⚠️ 找不到任何 <entry> 標籤！");
                return;
            }
            Console.WriteLine($"📝 找到 {nodes.Count} 篇文章，開始比對...");

            foreach (var node in nodes) {
                string title = node.SelectSingleNode(".//title")?.InnerText ?? "無標題";
                string href = node.SelectSingleNode(".//link")?.Attributes["href"]?.Value ?? "";
                await CheckAndNotify(site.Name, title, href);
            }
        }
    } catch (Exception ex) { Console.WriteLine($"❌ 異常: {ex.Message}"); }
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



