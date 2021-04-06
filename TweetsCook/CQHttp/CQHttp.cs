using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TweetsCook.CQHttp.Model;

namespace TweetsCook.CQHttp
{
    partial class CQClient
    {
        private readonly Uri WebSocketUri;
        private readonly ClientWebSocket WebSocket = new();
        private readonly Dictionary<string, TaskCompletionSource<JObject>> Results = new();

        public event EventHandler<Translate> Translate;
        public CQClient(string webSocketUri)
        {
            WebSocketUri = new Uri(webSocketUri);
        }
        private async Task<R> CQApi<R>(string action, object p)
        {
            var echo = Guid.NewGuid().ToString();
            Results[echo] = new TaskCompletionSource<JObject>();

            var json = JsonConvert.SerializeObject(new Request.Model<object>()
            {
                action = action,
                p = p,
                echo = echo
            });
            byte[] data = Encoding.UTF8.GetBytes(json);
            await WebSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);

            var result = Results[echo].Task.Result;
            Results.Remove(echo);
            return (result.ToObject<Response.Model<R>>()).data;
        }
        public async Task<Response.Message> Send(int group_id, string text)
        {
            return await CQApi<Response.Message>("send_group_msg", new Request.Message
            {
                group_id = group_id,
                message = text
            });
        }
        public async Task<Response.Reply> GetReply(int message_id)
        {
            return await CQApi<Response.Reply>("get_msg", new Request.Reply
            {
                message_id = message_id
            });
        }
        public async void Start()
        {
            var regex = new Regex(@"原推地址：(.+)", RegexOptions.Compiled);
            await WebSocket.ConnectAsync(WebSocketUri, CancellationToken.None);
            while(true)
            {
                var data = new byte[1024 * 1024];
                var response = await WebSocket.ReceiveAsync(data, CancellationToken.None);
                if (response.MessageType == WebSocketMessageType.Close) throw new WebSocketException();
                var text = Encoding.UTF8.GetString(data);
                
                JObject deserialize = JObject.Parse(text);
                if (deserialize.ContainsKey("message_type"))
                {
                    var _ = Task.Run(() =>
                    {
                        var translation = "";
                        var message = deserialize.ToObject<Event.Message>();
                        var replyId = Regex.Match(message.message, @"\[CQ:reply,id=(-?\d+)\]");
                        if (replyId.Success)
                        {
                            translation = Regex.Replace(message.message, @"\[CQ:.+?\]", "");
                            try
                            {
                                var replyMessage = GetReply(int.Parse(replyId.Groups[1].Value)).Result;
                                message.message = replyMessage.message;
                            }
                            catch
                            {
                                return;
                            }
                        }
                        else
                        {
                            translation = regex.Replace(message.message, "").Trim();
                        }
                        
                        var twitterUrl = regex.Match(message.message);
                        if (!twitterUrl.Success) return;
                        Translate.Invoke(this, new()
                        {
                            group_id = message.group_id,
                            url = twitterUrl.Groups[1].Value,
                            translation = translation
                        });
                    });

                }
                else if (deserialize.ContainsKey("echo"))
                {
                    var echo = deserialize["echo"].Value<string>();
                    Results[echo].SetResult(deserialize);
                }
            };
        }
    }
}
