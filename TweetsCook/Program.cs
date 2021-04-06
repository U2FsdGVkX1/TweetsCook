using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using TweetsCook.CQHttp;
using TweetsCook.Sources;

namespace TweetsCook
{
    class Program
    {
        static void Main(string[] args)
        {
            using var browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                Args = new[] { "--no-sandbox", "--window-size=1920,2080" },
                Headless = true,
                DefaultViewport = null,
                ExecutablePath = Config.Chrome
            }).Result;
            var twitterList = new Dictionary<int, string[]>()
            {
            };
            var bilibiliList = new Dictionary<int, string[]>()
            {
            };

            var cq = new CQClient(Config.WebSocket);
            cq.Translate += async (sender, e) =>
            {
                await cq.Send(e.group_id, "在抓了在抓了.jpg");
                
                const string JS = @"(translation, by) => {
const tweet = document.querySelector('article div:nth-child(3) div[lang]').parentElement;
let copyright = document.createElement('div'), div = document.createElement('div');
copyright.style = 'color: #69b9eb';
copyright.innerHTML = '由 ' + by +' 翻译自日语';
div.style = 'margin-top:5px;font-size:23px;font-weight:400;color:#14171a;font-family:system-ui,-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Ubuntu,""Helvetica Neue"",sans-serif;overflow-wrap:break-word;min-width:0;line-height:1.3125;position:relative;';
div.innerText = translation;
tweet.appendChild(copyright);
tweet.appendChild(div);
tweet.parentElement.parentElement.lastChild.remove();
document.querySelector('a[href$=likes]')?.parentElement.parentElement.parentElement.parentElement.remove();
return tweet.parentElement.parentElement.parentElement.parentElement.parentElement.parentElement;
}";
                using var page = await browser.NewPageAsync();
                try
                {
                    await page.GoToAsync(e.url, WaitUntilNavigation.Networkidle0);
                }
                catch
                {
                    await cq.Send(e.group_id, "长时间未加载出 Twitter 页面，如果一会截图有问题请重试");
                }

                var by = twitterList.ContainsKey(e.group_id) ? twitterList[e.group_id][1] : "某某字幕组";
                var article = (ElementHandle)await page.EvaluateFunctionHandleAsync(JS, e.translation, by);
                await article.ScreenshotAsync("Snapshot.png");

                var filePath = Environment.CurrentDirectory.Replace("\\", "/");
                filePath = filePath.Replace("#", "%23");
                filePath = $"file:///{HttpUtility.UrlPathEncode(filePath)}/Snapshot.png";
                await cq.Send(e.group_id, $"[CQ:image,file={filePath}]");
            };
            cq.Start();

            foreach (var twitter in twitterList)
            {
                var _ = new RSSHub(twitter.Value[0]);
                _.NewMessage += async (sender, rss) =>
                {
                    var content = rss.description.Replace("<br>", "\n");
                    content = Regex.Replace(content, @"<img style src=""(.+?)"" .+?>", @"[CQ:image,file=$1]");
                    content = Regex.Replace(content, @"<video .+? poster=""(.+?)""></video>", @"[CQ:image,file=$1]");
                    content = Regex.Replace(content, @"<a href=""(.+?)"" .+?</a>", @"$1");
                    await cq.Send(twitter.Key, $"{content}\n\n原推地址：{rss.link}");

                    using var httpClient = new HttpClient();
                    using var md5 = MD5.Create();
                    var guid = Guid.NewGuid().ToString();
                    var _ = md5.ComputeHash(Encoding.UTF8.GetBytes($"20180609000173981{content}{guid}j5lIKaNHR4YmCH6qdQ5u"));
                    var sign = String.Concat(_.Select(item => item.ToString("x2")));
                    var url = "https://fanyi-api.baidu.com/api/trans/vip/translate";
                    var data = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"q", content},
                        {"from", "jp"},
                        {"to", "zh"},
                        {"appid", "20180609000173981"},
                        {"salt", guid},
                        {"sign", sign},
                    });
                    var response = await httpClient.PostAsync(url, data);
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    JObject deserialize = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var dst = String.Join("\n", from item in deserialize["trans_result"]
                                                select item["dst"].Value<string>());
                    await cq.Send(twitter.Key, $"===========大概翻译===========\n{dst}");
                };
                _.Start();
            }
            foreach (var bilibili in bilibiliList)
            {
                var _ = new RSSHub(bilibili.Value[0]);
                _.NewMessage += async (sender, rss) =>
                {
                    var content = rss.description.Replace("<br>", "\n");
                    content = Regex.Replace(content, @"<img.*? src=""(.+?)"" .+?>", @"[CQ:image,file=$1]");
                    await cq.Send(bilibili.Key, $"===========哔哩哔哩===========\n{content}\n\n原动态地址：{rss.link}");
                };
                _.Start();
            }

            Console.ReadLine();
        }
    }
}
