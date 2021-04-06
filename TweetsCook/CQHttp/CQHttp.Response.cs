namespace TweetsCook.CQHttp.Response
{
    partial class Message
    {
        public int message_id;
    }
    partial class Reply
    {
        public int message_id;
        public int real_id;
        public int time;
        public string message;
    }
    partial class Model<T>
    {
        public T data;
        public int retcode;
        public string status;
        public string echo;
    }
}
