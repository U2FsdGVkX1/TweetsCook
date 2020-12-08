﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TweetsCook
{
    partial class CQHttp
    {
        private readonly Uri WebSocketUri;
        private readonly int User;

        private ClientWebSocket WebSocket = new ClientWebSocket();
        private Dictionary<string, TaskCompletionSource<string>> Results = new Dictionary<string, TaskCompletionSource<string>>();

        public delegate void MessageEvent(Response.Reply original, string translation);
        public event MessageEvent OnTranslate = null;
        public CQHttp(string webSocketUri, int user)
        {
            WebSocketUri = new Uri(webSocketUri);
            User = user;
        }
        private async Task<R> CQApi<P, R>(string action, P p)
        {
            var echo = Guid.NewGuid().ToString();
            Results[echo] = new TaskCompletionSource<string>();

            var json = JsonConvert.SerializeObject(new Request.Model<P>()
            {
                action = action,
                p = p,
                echo = echo
            });
            byte[] data = Encoding.UTF8.GetBytes(json);
            await WebSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);

            string result = Results[echo].Task.Result;
            Results.Remove(echo);
            return JsonConvert.DeserializeObject<Response.Model<R>>(result).data;
        }
        public async Task<Response.Message> Send(string text)
        {
            return await CQApi<Request.Message, Response.Message>("send_group_msg", new Request.Message
            {
                group_id = User,
                message = text
            });
        }
        public async Task<Response.Reply> GetReply(int message_id)
        {
            return await CQApi<Request.Reply, Response.Reply>("get_msg", new Request.Reply
            {
                message_id = message_id
            });
        }
        public async void Start()
        {
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
                        var message = deserialize.ToObject<Event.Message>();
                        var replyId = Regex.Match(message.message, @"\[CQ:reply,id=(-?\d+)\]");
                        var translation = Regex.Replace(message.message, @"\[CQ:.+?\]", "");
                        if (!replyId.Success) return;

                        var replyMessage = GetReply(int.Parse(replyId.Groups[1].Value)).Result;
                        OnTranslate(replyMessage, translation);
                    });

                } else if (deserialize.ContainsKey("echo"))
                {
                    var echo = deserialize.ToObject<Response.Model<object>>().echo;
                    Results[echo].SetResult(text);
                }
                Console.WriteLine(Encoding.UTF8.GetString(data));
            };
        }
    }
}