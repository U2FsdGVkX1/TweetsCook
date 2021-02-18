using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using TweetsCook.Sources;

namespace TweetsCook
{
    class Program
    {
        static void Main(string[] args)
        {
            var twitterList = new Dictionary<int, string[]>()
            {
            };
            var bilibiliList = new Dictionary<int, string[]>()
            {
            };

            CQHttp cq = new CQHttp(Config.WebSocket);
            cq.OnTranslate += async (int group_id, string url, string translation) =>
            {
                await cq.Send(group_id, "在抓了在抓了.jpg");
                using Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Args = new[] { "--no-sandbox", "--window-size=1920,2080" },
                    Headless = true,
                    DefaultViewport = null,
                    ExecutablePath = Config.Chrome
                });
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
                    await page.GoToAsync(url, WaitUntilNavigation.Networkidle0);
                } catch(PuppeteerSharp.NavigationException e)
                {
                    await cq.Send(group_id, "长时间未加载出 Twitter 页面，如果一会截图有问题请重试");
                }

                var by = twitterList.ContainsKey(group_id) ? twitterList[group_id][1] : "某某字幕组";
                var article = (ElementHandle)await page.EvaluateFunctionHandleAsync(JS, translation, by);
                await article.ScreenshotAsync("Snapshot.png");

                var filePath = Environment.CurrentDirectory.Replace("\\", "/");
                filePath = filePath.Replace("#", "%23");
                filePath = $"file:///{HttpUtility.UrlPathEncode(filePath)}/Snapshot.png";
                await cq.Send(group_id, $"[CQ:image,file={filePath}]");
            };
            cq.Start();

            foreach (var twitter in twitterList)
            {
                RSSHub _ = new RSSHub(twitter.Value[0]);
                _.OnMessage += async (RSS rss) =>
                {
                    var content = rss.description.Replace("<br>", "\n");
                    content = Regex.Replace(content, @"<img style src=""(.+?)"" .+?>", @"[CQ:image,file=$1]");
                    content = Regex.Replace(content, @"<video .+? poster=""(.+?)""></video>", @"[CQ:image,file=$1]");
                    content = Regex.Replace(content, @"<a href=""(.+?)"" .+?</a>", @"$1");
                    await cq.Send(twitter.Key, $"{content}\n\n原推地址：{rss.link}");
                };
                _.Start();
            }
            foreach (var bilibili in bilibiliList)
            {
                RSSHub _ = new RSSHub(bilibili.Value[0]);
                _.OnMessage += async (RSS rss) =>
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
