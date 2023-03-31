using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimpleTcp.Core
{
    public class SimpleTcpClient
    {
        private Socket _mainSocket;
        private readonly EncodingFormat _encodingFormat;
        private readonly int _connectionPort;
        private readonly int _bufferSize;
        private readonly float _timeOutLimit;

        private readonly MessageProcessor _messageProcessor;

        public event Action OnConnectionFailed;
        public event Action OnConnectedToEditorServer;
        public event Action<DisconnectionType> OnDisconnectedFromEditorServer;
        public event Action<string> OnMessageReceived;

        private bool _isConnected;
        private bool _shutdownPing;
        
        public SimpleTcpClient(int portNumber, int sizeOfBuffer, float timeOutLimit, EncodingFormat format)
        {
            _connectionPort = portNumber;
            _bufferSize = sizeOfBuffer;
            _timeOutLimit = timeOutLimit;
            _encodingFormat = format;
            _messageProcessor = new MessageProcessor();
        }

        public void TryConnect(string ipAddress)
        {
            _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _mainSocket.NoDelay = true;
            _mainSocket.SendBufferSize = 0;
            var serverAddr = new IPEndPoint(IPAddress.Parse(ipAddress), _connectionPort);
            _mainSocket.BeginConnect(serverAddr, OnConnect, _mainSocket);
        }

        public void RequestCloseClient()
        {
            if (_mainSocket == null) return;
            _shutdownPing = true;
            SendMessageToServer(CoreProtocol.MSG_CLOSE_CONNECTION);
        }

        private void CloseClient()
        {
            if (_mainSocket == null) return;

            _mainSocket.Close();
            _mainSocket.Dispose();
            _mainSocket = null;
            _isConnected = false;
        }

        private async void CheckServerConnection()
        {
            while (_isConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(Constants.PING_INTERVAL));
                if (!_isConnected || _shutdownPing)
                    break;
                
                _isConnected = false;
                SendMessageToServer(CoreProtocol.PING_SEND);
                await Task.Delay(TimeSpan.FromSeconds(_timeOutLimit));
                
                if (!_shutdownPing) continue;
                OnDisconnectedFromEditorServer?.Invoke(DisconnectionType.TimeOut);
                if (_isConnected)
                {
                    CloseClient();
                }
                return;
            }
            CloseClient();
        }

        public bool SendMessageToServer(string msg)
        {
            return SocketUtility.SendMessage(msg, _mainSocket, _encodingFormat);
        }

        private void OnConnect(IAsyncResult asyncResult)
        {
            try
            {
                var clSocket = asyncResult.AsyncState as Socket;
                clSocket.EndConnect(asyncResult);

                var dataUnit = new DataUnit(_bufferSize)
                {
                    targetSocket = clSocket
                };
                _isConnected = true;
                _mainSocket.BeginReceive(dataUnit.buffer, 0, dataUnit.bufferSize, 0, OnDataReceived, dataUnit);
                CheckServerConnection();
                OnConnectedToEditorServer?.Invoke();
            }
            catch (Exception)
            {
                OnConnectionFailed?.Invoke();
            }
        }

        private void OnDataReceived(IAsyncResult asyncResult)
        {
            if (!(asyncResult.AsyncState is DataUnit receivedData))
                throw new NullReferenceException();
            _isConnected = true;
            receivedData.targetSocket.EndReceive(asyncResult);
            var decoded = receivedData.GetDecodedString(_encodingFormat);
            PutReceivedMessage(decoded);
            receivedData.ClearBuffer();
            try
            {
                receivedData.targetSocket.BeginReceive(receivedData.buffer, 0, _bufferSize, 0, OnDataReceived, receivedData);
            }
            catch (Exception)
            {
                //더 이상 통신할 수 없는 상태로 판단하여 요청없이 강제 종료
                if (!_isConnected) return;
                
                OnDisconnectedFromEditorServer?.Invoke(DisconnectionType.Forced);
                CloseClient();
            }
        }

        private void PutReceivedMessage(string received)
        {
            _messageProcessor.SetMessageOnProcessor(received);
            if (!_messageProcessor.IsProcessedMessageExists())
                return;

            var processedMsg = _messageProcessor.GetAllProcessedMessage();
            foreach (var msg in processedMsg)
            {
                if(InternalMessageProcessing(msg))
                    continue;
                OnMessageReceived?.Invoke(msg);
            }
        }

        private bool InternalMessageProcessing(string msg)
        {
            if(msg.Equals(CoreProtocol.PING_SEND))
            {
                SendMessageToServer(CoreProtocol.PING_RECV);
                return true;
            }

            if (msg.Equals(CoreProtocol.PING_RECV))
            {
                return true;
            }
            
            if (msg.Equals(CoreProtocol.MSG_CLOSE_REQUEST_RECEIVED_ACK))
            {
                OnDisconnectedFromEditorServer?.Invoke(DisconnectionType.Correctly);
                CloseClient();
                return true;
            }

            if(msg.Equals(CoreProtocol.MSG_CLOSE_SERVER_NOTICE))
            {
                SendMessageToServer(CoreProtocol.MSG_CLOSE_CONNECTION);
                return true;
            }

            return false;
        }
    }
}