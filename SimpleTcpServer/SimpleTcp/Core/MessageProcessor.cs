using System;
using System.Collections.Generic;

namespace SimpleTcp.Core
{
    internal class MessageProcessor
    {
        private string _processingMessage;
        
        private readonly Queue<string> _processedMessage = new Queue<string>();
        

        public bool IsProcessedMessageExists() => _processedMessage.Count > 0;

        public void ClearProcessingMessgae()
        {
            _processingMessage = string.Empty;
            _processedMessage.Clear();
        }

        public string GetProcessingMessage() => _processingMessage;

        public string[] GetAllProcessedMessage()
        {
            var ret = _processedMessage.ToArray();
            _processedMessage.Clear();
            return ret;
        }

        public void SetMessageOnProcessor(string msg)
        {
            if (msg == string.Empty) 
                return;

            _processingMessage += msg;

            var isRemainFullMessage = _processingMessage.Contains(CoreProtocol.HEADER_MSG_INIT) 
                                      && _processingMessage.Contains(CoreProtocol.HEADER_MSG_END);
            if (!isRemainFullMessage)
                return;
            
            while (true)
            {
                var openMsgHeaderIndex = _processingMessage.IndexOf(CoreProtocol.HEADER_MSG_INIT, StringComparison.Ordinal);
                if (openMsgHeaderIndex < 0)
                    break;
                
                var closeMsgHeaderIndex = _processingMessage.IndexOf(CoreProtocol.HEADER_MSG_END, StringComparison.Ordinal);
                if (closeMsgHeaderIndex < 0)
                    break;

                var offsetEnd = closeMsgHeaderIndex + CoreProtocol.HEADER_MSG_END.Length;
                var extractedMsg = _processingMessage.Substring(openMsgHeaderIndex, offsetEnd);
                _processingMessage = _processingMessage.Remove(openMsgHeaderIndex, offsetEnd);
                _processedMessage.Enqueue(extractedMsg.Replace(CoreProtocol.HEADER_MSG_INIT, string.Empty).Replace(CoreProtocol.HEADER_MSG_END, string.Empty));
            }
        }
    }
}