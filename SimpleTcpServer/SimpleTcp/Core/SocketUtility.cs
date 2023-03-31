using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace SimpleTcp.Core
{
    internal static class SocketUtility
    {
        public delegate byte[] DelegateMessageEncodeMethod(string str);
        public delegate string DelegateMessageDecodeMethod(byte[] bytes);
        
        public static readonly Dictionary<EncodingFormat, DelegateMessageEncodeMethod> EncodingMethods = new Dictionary<EncodingFormat, DelegateMessageEncodeMethod>()
        {
            {EncodingFormat.Unicode, Encoding.Unicode.GetBytes},
            {EncodingFormat.BigEndianUnicode, Encoding.BigEndianUnicode.GetBytes},
            {EncodingFormat.UTF7, Encoding.UTF7.GetBytes},
            {EncodingFormat.UTF8, Encoding.UTF8.GetBytes},
            {EncodingFormat.UTF32, Encoding.UTF32.GetBytes},
            {EncodingFormat.ASCII, Encoding.ASCII.GetBytes}
        };

        public static readonly Dictionary<EncodingFormat, DelegateMessageDecodeMethod> DecodeMethods = new Dictionary<EncodingFormat, DelegateMessageDecodeMethod>()
        {
            {EncodingFormat.Unicode, Encoding.Unicode.GetString},
            {EncodingFormat.BigEndianUnicode, Encoding.BigEndianUnicode.GetString},
            {EncodingFormat.UTF7, Encoding.UTF7.GetString},
            {EncodingFormat.UTF8, Encoding.UTF8.GetString},
            {EncodingFormat.UTF32, Encoding.UTF32.GetString},
            {EncodingFormat.ASCII, Encoding.ASCII.GetString}
        };
        
        internal static bool SendMessage(string msg, Socket socket, EncodingFormat format)
        {
            msg = $"{CoreProtocol.HEADER_MSG_INIT}{msg}{CoreProtocol.HEADER_MSG_END}";
            if (!EncodingMethods.TryGetValue(format, out var encodeMethod)) return false;
            var encodedMsg = encodeMethod(msg);
            try
            {
                socket.Send(encodedMsg);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}