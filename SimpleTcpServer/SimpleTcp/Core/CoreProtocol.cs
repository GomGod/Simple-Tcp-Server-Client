namespace SimpleTcp.Core
{
    internal static class CoreProtocol
    {
        public const string PING_SEND = "#ps";
        public const string PING_RECV = "#pr";
        public const string MSG_CLOSE_CONNECTION = "#cl";
        public const string MSG_CLOSE_REQUEST_RECEIVED_ACK = "#cla";
        public const string MSG_CLOSE_SERVER_NOTICE = "#csv";
        
        public const string HEADER_MSG_INIT = "<#mi>";
        public const string HEADER_MSG_END = "<#me>";
    }
}