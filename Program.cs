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
    
    // 這裡就是你要修改的地方！
var targets = new List<SiteConfig> {
    new SiteConfig { 
        Name = "Self-Test", 
        // 這裡換成你自己的 GitHub 帳號和專案名
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
    Console.WriteLine($"🌐 [Uni-Ask] 正在進行自體循環測試...");
    try {
        using (var client = new HttpClient()) {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            string content = await client.GetStringAsync(site.Url);
            
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine($"🔎 成功讀取到 {lines.Length} 行代碼，開始全面掃描...");

            foreach (var line in lines) {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine)) continue;

                // 直接呼叫比對邏輯，不預設任何 Markdown 格式
                await CheckAndNotify(site.Name, cleanLine, site.Url);
            }
        }
    } catch (Exception ex) { 
        Console.WriteLine($"❌ 掃描異常: {ex.Message}"); 
    }
}

        // 這是你剛才漏掉的「心臟零件」
static async Task CheckAndNotify(string siteName, string title, string link) {
    if (string.IsNullOrEmpty(title)) return;

    // 1. 強力清洗：轉小寫、去前後空白、去掉內部的換行
    string cleanTitle = title.Replace("\r", "").Replace("\n", "").Trim().ToLower();
    
    // 2. 偵錯日誌：讓我們看看獵人到底在比對什麼
    Console.WriteLine($"   🧐 正在比對標題: [{cleanTitle}]");
    Console.WriteLine($"   🔑 目前關鍵字: {string.Join(", ", myKeywords)}");

    // 3. 執行比對
    bool hasKey = myKeywords.Any(k => cleanTitle.Contains(k.Trim().ToLower()));
    bool hasExclude = excludeKeywords.Any(e => cleanTitle.Contains(e.Trim().ToLower()));

    if (hasKey && !hasExclude) {
        //if (!sentLinks.Contains(link)) {
            Console.WriteLine($"   ✨ [命中成功]：{title}");
            await SendLineMessage($"\n🌟 [{siteName}] 發現好缺！\n\n標題：{title}\n連結：{link}");
        //sentLinks.Add(link);
     //   } else {
            Console.WriteLine("   ⏭️ 此連結已發送過，跳過。");
    //    }
 //   } else {
      //  Console.WriteLine("   ❌ 關鍵字不匹配。");
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
















