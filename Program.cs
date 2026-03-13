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
        // 1. 請務必換成你 Google 試算表「發布到網路」的 CSV 連結
        static string userConfigCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vTjAdjergagVBidinxnQhq9c7hf64SEyOgOX85HtE4tNUvtlKVHTZr8wB6TUp5pVaS92fMp8ZhMfiny/pub?gid=1519632381&single=true&output=csv";
        
        static List<string> myKeywords = new List<string>();
        static HashSet<string> sentLinks = new HashSet<string>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 [Uni-Ask Cloud Hunter] 啟動中...");
            
            // 同步雲端設定 (關鍵字)
            await LoadCloudConfigs();

            // 測試目標：抓取你自己的程式碼
var targets = new List<SiteConfig> {
    new SiteConfig { 
        Name = "GitHub-Internship", 
        Url = "https://raw.githubusercontent.com/tw-intern/awesome-taiwan-internships/main/README.md" 
    }
};

            foreach (var site in targets) {
                await ScanSite(site);
            }

            Console.WriteLine("✅ 任務全部完成。");
        }

        static async Task LoadCloudConfigs() {
            try {
                using (var client = new HttpClient()) {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    string csvContent = await client.GetStringAsync(userConfigCsvUrl);
                    
                    var rows = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    // 從第 2 行 (索引 1) 開始抓資料
                    if (rows.Length > 1) {
                        var columns = rows[1].Split(',');
                        
                        // 根據你的表單：A(0)時間, B(1)ID, C(2)訂閱關鍵字
                        if (columns.Length > 2) {
                            string rawKeywords = columns[2].Trim();
                            myKeywords = rawKeywords.Split(';').Select(k => k.Trim()).ToList();
                            Console.WriteLine($"📊 同步完成！目前關鍵字：{string.Join(", ", myKeywords)}");
                        }
                    }
                }
            } catch (Exception ex) { 
                Console.WriteLine($"⚠️ CSV 同步失敗: {ex.Message}"); 
            }
        }

        static async Task ScanSite(SiteConfig site) {
            Console.WriteLine($"🌐 [Uni-Ask] 正在進行獵捕: {site.Name}");
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
    if (string.IsNullOrEmpty(title) || myKeywords.Count == 0) return;

    string cleanTitle = title.Trim().ToLower();
    bool hasKey = myKeywords.Any(k => !string.IsNullOrEmpty(k) && cleanTitle.Contains(k.ToLower()));

    // 關鍵修復：檢查這個標題是否已經發送過
    if (hasKey && !sentLinks.Contains(title)) { 
        Console.WriteLine($"   ✨ [命中成功]：{title}");
        await SendLineMessage($"\n🌟 [{siteName}] 發現好缺！\n\n內容：{title}\n連結：{link}");
        
        // 記下這個標題，下次掃描到一模一樣的就不會再發送
        sentLinks.Add(title); 
    }
}

        static async Task SendLineMessage(string msg) {
            // 從 GitHub Secrets 抓取這兩把鑰匙
            string channelToken = Environment.GetEnvironmentVariable("LINE_TOKEN");
            string userId = Environment.GetEnvironmentVariable("LINE_USER_ID");

            if (string.IsNullOrEmpty(channelToken) || string.IsNullOrEmpty(userId)) {
                Console.WriteLine("❌ 錯誤：Secrets 設定不完整 (LINE_TOKEN 或 LINE_USER_ID)");
                return;
            }

            using (var client = new HttpClient()) {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {channelToken}");
                
                var payload = new { 
                    to = userId, 
                    messages = new[] { new { type = "text", text = msg } } 
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload), 
                    System.Text.Encoding.UTF8, 
                    "application/json"
                );

                var response = await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
                if (response.IsSuccessStatusCode) {
                    Console.WriteLine("✅ [Messaging API] 訊息發送成功！");
                } else {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ LINE 發送失敗：{response.StatusCode}, 詳情: {error}");
                }
            }
        }
    }

    public class SiteConfig {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}


