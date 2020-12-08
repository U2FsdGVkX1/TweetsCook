using PuppeteerSharp;
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using TweetsCook.Sources;

namespace TweetsCook
{
    class Program
    {
        static void Main(string[] args)
        {
            CQHttp cq = new CQHttp(Config.WebSocket, Config.QQGroup);
            cq.OnTranslate += async (Response.Reply original, string translation) =>
            {
                var twitterUrl = Regex.Match(original.message, "原推地址：(.+)");
                if (!twitterUrl.Success) return;

                await cq.Send("在抓了在抓了.jpg");
                using Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Args = new[] { "--no-sandbox", "--window-size=1920,1080" },
                    Headless = false,
                    DefaultViewport = null,
                    //ExecutablePath = @"/usr/bin/chromium"
                    ExecutablePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
                });
                const string JS = @"(translation, by) => {
const tweet = document.querySelector('article div:nth-child(3) div[lang]').parentElement;
let copyright = document.createElement('div'), div = document.createElement('div');
copyright.style = 'color: #69b9eb';
copyright.innerHTML = '由 <span style=""color: #f7c4ba;font-weight: bold"">' + by +'</span> 翻译自日语';
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
                    await page.GoToAsync(twitterUrl.Groups[1].Value, WaitUntilNavigation.Networkidle0);
                } catch(PuppeteerSharp.NavigationException e)
                {
                    await cq.Send("长时间未加载出 Twitter 页面，如果一会截图有问题请重试");
                }
                    
                var article = (ElementHandle)await page.EvaluateFunctionHandleAsync(JS, translation, Config.Cook);
                await article.ScreenshotAsync("Snapshot.png");

                var filePath = Environment.CurrentDirectory.Replace("\\", "/");
                filePath = filePath.Replace("#", "%23");
                filePath = $"file:///{HttpUtility.UrlPathEncode(filePath)}/Snapshot.png";
                Console.WriteLine($"[CQ:image,file={filePath}]");
                await cq.Send($"[CQ:image,file={filePath}]");
            };
            cq.Start();

            RSSHub twitter = new RSSHub(Config.TwitterUrl);
            twitter.OnMessage += async (RSS rss) =>
            {
                var content = rss.description.Replace("<br>", "\n");
                content = Regex.Replace(content, @"\<img src=""(.*?)"" .+?>", @"[CQ:image,file=$1]");
                await cq.Send($"{content}\n\n原推地址：{rss.link}");
            };
            twitter.Start();

            Console.ReadLine();
        }
    }
}
