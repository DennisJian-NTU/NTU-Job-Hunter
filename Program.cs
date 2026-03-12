using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NTUJobHunter
{
    class Program
    {
        // 你的 Google Sheets CSV 網址
        static string userConfigCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTjAdjergagVBidinxnQhq9c7hf64SEyOgOX85HtE4tNUvtlKVHTZr8wB6TUp5pVaS92fMp8ZhMfiny/pubhtml?gid=1519632381&single=true";
        static List<string> myKeywords = new List<string>();
        static List<string> excludeKeywords = new List<string>();
        static HashSet<string> sentLinks = new HashSet<string>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 [Uni-Ask Cloud Hunter] 啟動中...");
            await LoadCloudConfigs();

            var targets = new List<SiteConfig> {
                new SiteConfig { 
                    Name = "Self-Test", 
                    Url = "https://raw.githubusercontent.com/DennisJian-NTU/NTU-Job-Hunter/main/Program.cs" 
                }
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
            
            // 修正：處理換行符號
            var rows = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 我們要找的是第 2 行 (索引 1)
            if (rows.Length > 1) {
                var columns = rows[1].Split(',');
                
                // 根據你的截圖，「訂閱關鍵字」是在第 3 欄 (索引 2)
                if (columns.Length > 2) {
                    string rawKeywords = columns[2].Trim();
                    myKeywords = rawKeywords.Split(';').Select(k => k.Trim()).ToList();
                    Console.WriteLine($"📊 同步成功！抓取到關鍵字：{string.Join(", ", myKeywords)}");
                }
            }
        }
    } catch (Exception ex) { 
        Console.WriteLine($"⚠️ CSV 同步失敗: {ex.Message}"); 
    }
}
        static async Task ScanSite(SiteConfig site) {
            Console.WriteLine($"🌐 [Uni-Ask] 正在進行自體循環測試...");
            try {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    string content = await client.GetStringAsync(site.Url);
                    var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines) {
                        await CheckAndNotify(site.Name, line.Trim(), site.Url);
                    }
                }
            } catch (Exception ex) { Console.WriteLine($"❌ 掃描異常: {ex.Message}"); }
        }

        static async Task CheckAndNotify(string siteName, string title, string link) {
            if (string.IsNullOrEmpty(title)) return;
            string cleanTitle = title.Trim().ToLower();
            bool hasKey = myKeywords.Any(k => cleanTitle.Contains(k.Trim().ToLower()));

            if (hasKey) {
                Console.WriteLine($"   ✨ [命中成功]：{title}");
                await SendLineMessage($"\n🌟 [Uni-Ask 測試] 獵人抓到代碼了！\n\n內容：{title}");
            }
        }

        static async Task SendLineMessage(string msg) {
            string channelToken = Environment.GetEnvironmentVariable("LINE_TOKEN");
            string userId = Environment.GetEnvironmentVariable("LINE_USER_ID");

            using (var client = new HttpClient()) {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {channelToken}");
                var payload = new { to = userId, messages = new[] { new { type = "text", text = msg } } };
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
            }
        }
    }

    // --- 關鍵修復：定義 SiteConfig 結構 ---
    public class SiteConfig {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}

