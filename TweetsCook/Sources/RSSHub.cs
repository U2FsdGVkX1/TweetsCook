using System;
using System.Net.Http;
using System.Xml.Linq;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace TweetsCook.Sources
{
    partial class RSSHub : Base
    {
        public delegate void RSSEvent(RSS rss);
        public event RSSEvent OnMessage;
        public RSSHub(string SourceUri) : base(SourceUri)
        {

        }
        public override async void Start()
        {
            using var httpClient = new HttpClient();
            List<RSS> items;
            for (var time = DateTime.MinValue; true; time = (items.Count == 0 ? time : items[0].pubDate))
            {
                var response = await httpClient.GetAsync(SourceUri);
                var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
                items = (from item in document.Descendants("item")
                         let rss = new RSS
                         {
                             title = (string)item.Element("title"),
                             description = (string)item.Element("description"),
                             pubDate = (DateTime)item.Element("pubDate"),
                             guid = (string)item.Element("guid"),
                             link = (string)item.Element("link"),
                             author = (string)item.Element("author"),
                         }
                         where rss.pubDate > time
                         select rss).ToList();
                if (time != DateTime.MinValue)
                {
                    foreach (var item in items)
                    {
                        OnMessage(item);
                    }
                }

                await Task.Delay(1000 * 60);
            }
        }
    }
}
