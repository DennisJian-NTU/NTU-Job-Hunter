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
            try
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                    ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                };

                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.google.com/");

                    // 徹底清洗網址，解決 404 的終極一招
                    string cleanUrl = site.Url.Trim().Replace("\r", "").Replace("\n", "");
                    var request = new HttpRequestMessage(HttpMethod.Get, cleanUrl);
                    request.Headers.Add("Cookie", "over18=1");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"   ❌ {site.Name} 失敗: {response.StatusCode}");
                        return;
                    }

                    string html = await response.Content.ReadAsStringAsync();
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var links = doc.DocumentNode.SelectNodes("//a");
                    if (links == null) return;

                    foreach (var node in links)
                    {
                        string title = node.InnerText.Trim();
                        string href = node.Attributes["href"]?.Value ?? "";
                        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href)) continue;

                        bool isPttArticle = site.Url.Contains("ptt") && href.Contains("/M.");
                        bool isNtuArticle = site.Url.Contains("ntu") && href.Contains("spotlight");

                        if (isPttArticle || isNtuArticle)
                        {
                            string fullLink = new Uri(new Uri(cleanUrl), href).AbsoluteUri;
                            string lowerTitle = title.ToLower();
                            if (myKeywords.Any(k => lowerTitle.Contains(k)) && !excludeKeywords.Any(e => lowerTitle.Contains(e)))
                            {
                                Console.WriteLine($"✨ 命中：{title}");
                                await SendLineMessage($"\n🌟 [{site.Name}] 發現好缺！\n\n標題：{title}\n連結：{fullLink}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"❌ {site.Name} 異常: {ex.Message}"); }
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
