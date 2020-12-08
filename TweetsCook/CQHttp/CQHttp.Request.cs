using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TweetsCook.Request
{
    partial class Message
    {
        public int group_id;
        public string message;
    }
    partial class Reply
    {
        public int message_id;
    }
    partial class Model<T>
    {
        public string action;
        [JsonProperty("params")]
        public T p;
        public string echo;
    }
}
