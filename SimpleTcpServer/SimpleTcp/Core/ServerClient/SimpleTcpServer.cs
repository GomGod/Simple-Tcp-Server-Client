using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimpleTcp.Core
{
    public class SimpleTcpServer
    {
        private Socket _mainSocket;
        private readonly EncodingFormat _serverEncodeFormat;
        private ServerState _currentServerState;
        private readonly int _serverPort;
        private readonly int _bufferSize;
        private readonly float _timeOutLimit;

        public event Action<Socket> OnConnectedSocketEvent;
        public event Action<string, Socket> OnMessageReceivedEvent;
        public event Action<DisconnectionType, Socket> OnDisconnectedSocketEvent;

        private readonly Dictionary<Socket, bool> _connectedSocket = new Dictionary<Socket, bool>();
        private readonly Dictionary<Socket, MessageProcessor> _processingMessage = new Dictionary<Socket, MessageProcessor>();
        
        public SimpleTcpServer(int portNumber, int sizeOfBuffer, float timeOutLimit, EncodingFormat encodeFormat)
        {
            _serverPort = portNumber;
            _bufferSize = sizeOfBuffer;
            _timeOutLimit = timeOutLimit;
            _serverEncodeFormat = encodeFormat;
            _currentServerState = ServerState.Closed;
        }
        
        public bool OpenServer()
        {
            if (_currentServerState != ServerState.Closed)
                return false;
            
            try
            {
                var serverEndPoint = new IPEndPoint(IPAddress.Any, _serverPort);
                _mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _mainSocket.NoDelay = true;
                _mainSocket.SendBufferSize = 0;
                
                _mainSocket.Bind(serverEndPoint);
                _mainSocket.Listen(15);
                _mainSocket.BeginAccept(OnAccept, null);
                _currentServerState = ServerState.Open;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task CloseServer()
        {
            if (_currentServerState != ServerState.Open)
                return;
            
            if (_mainSocket == null)
                return;
            
            _currentServerState = ServerState.Closing;
            
            var sokToClose = _connectedSocket.Select(kv => kv.Key).ToList();
            foreach (var socket in sokToClose)
            {
                SendMessage(CoreProtocol.MSG_CLOSE_SERVER_NOTICE, socket);
            }

            await Task.WhenAny(IsSocketClearedCheckTask() ,Task.Delay(TimeSpan.FromSeconds(_timeOutLimit)));
            sokToClose.Clear();
            sokToClose = _connectedSocket.Select(kv => kv.Key).ToList();
            foreach (var socket in sokToClose)
            {
                CloseSocket(socket); //강제 종료
            }
            
            _mainSocket.Close();
            _mainSocket.Dispose();
            _currentServerState = ServerState.Closed;
        }

        private async Task IsSocketClearedCheckTask()
        {
            while (_connectedSocket.Count > 0 && _currentServerState == ServerState.Closing)
            {
                await Task.Delay(TimeSpan.FromSeconds(0.1f));
            }
        }

        private async void PingTargetSocket(Socket socket)
        {
            while (_currentServerState == ServerState.Open)
            {
                await Task.Delay(TimeSpan.FromSeconds(Constants.PING_INTERVAL));
                if (!_connectedSocket.ContainsKey(socket))
                    break;
                SendMessage(CoreProtocol.PING_SEND, socket);
                _connectedSocket[socket] = false;
                
                await Task.Delay(TimeSpan.FromSeconds(_timeOutLimit));
                
                if (!_connectedSocket.ContainsKey(socket) || !_connectedSocket[socket])
                    break;
            }
            
            if (_currentServerState == ServerState.Open) return;
            if (!_connectedSocket.ContainsKey(socket)) return;
            OnDisconnectedSocketEvent?.Invoke(DisconnectionType.TimeOut ,socket);
            CloseSocket(socket);
        }

        private void CloseSocket(Socket socket)
        {
            if (!_connectedSocket.ContainsKey(socket))
                return;
            SendMessage(CoreProtocol.MSG_CLOSE_REQUEST_RECEIVED_ACK, socket);
            
            _connectedSocket.Remove(socket);
            _processingMessage.Remove(socket);

            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
            }
            catch (Exception)
            {
                //....
            }

        }

        public bool SendMessage(string msg, Socket socket)
        {
            if (_currentServerState == ServerState.Closed || !_connectedSocket.ContainsKey(socket))
                return false;
            
            return SocketUtility.SendMessage(msg, socket, _serverEncodeFormat);
        }

        private void OnAccept(IAsyncResult asyncResult)
        {
            try
            {
                var clientSocket = _mainSocket.EndAccept(asyncResult);
                var dataUnit = new DataUnit(_bufferSize)
                {
                    targetSocket = clientSocket
                };
                clientSocket.BeginReceive(dataUnit.buffer, 0, _bufferSize, SocketFlags.None, OnDataReceived, dataUnit);
                _connectedSocket.Add(clientSocket, true);
                _mainSocket.BeginAccept(OnAccept, null);
                PingTargetSocket(clientSocket);
                OnConnectedSocketEvent?.Invoke(clientSocket);
            }
            catch (SocketException)
            {
                Console.WriteLine("Connection Failed!");
                // ignored
            }
            catch (ObjectDisposedException)
            {
                //....
            }
        }

        private void OnDataReceived(IAsyncResult asyncResult)
        {
            if (!(asyncResult.AsyncState is DataUnit receivedDataUnit))
                return;
            if(!receivedDataUnit.targetSocket.Connected)
                return;
            if (!_connectedSocket.TryGetValue(receivedDataUnit.targetSocket, out var connected))
                return;

            _connectedSocket[receivedDataUnit.targetSocket] = true; //뭐가됐든 수신이 됐다면 available 상태로 전환
            receivedDataUnit.targetSocket.EndReceive(asyncResult);
            var decoded = receivedDataUnit.GetDecodedString(_serverEncodeFormat);
            PutReceivedDataToMessageQueue( receivedDataUnit.targetSocket, decoded);
            receivedDataUnit.ClearBuffer();
            
            try
            {
                if (_currentServerState != ServerState.Open)
                {
                    return;
                }
                receivedDataUnit.targetSocket.BeginReceive(receivedDataUnit.buffer, 0, _bufferSize, SocketFlags.None, OnDataReceived, receivedDataUnit);
            }
            catch (SocketException)
            {
                OnDisconnectedSocketEvent?.Invoke(DisconnectionType.Forced, receivedDataUnit.targetSocket);
                CloseSocket(receivedDataUnit.targetSocket);
            }
            catch (ObjectDisposedException)
            {
                //....
            }
        }
        
        private void PutReceivedDataToMessageQueue(Socket socket, string received)
        {
            if (!_connectedSocket.ContainsKey(socket))
            {
                _processingMessage.Remove(socket);
                return;
            }

            if (!_processingMessage.ContainsKey(socket))
            {
                _processingMessage.Add(socket, new MessageProcessor());
            }

            var msgProcessor = _processingMessage[socket]; 
            
            msgProcessor.SetMessageOnProcessor(received);
            if (!msgProcessor.IsProcessedMessageExists())
                return;

            var processedMsg = msgProcessor.GetAllProcessedMessage();
            foreach (var msg in processedMsg)
            {
                if(InternalMessageProcessing(msg, socket)) 
                    continue;
                OnMessageReceivedEvent?.Invoke(msg, socket);
            }
        }

        private bool InternalMessageProcessing(string msg, Socket socket)
        {
            if (msg.Equals(CoreProtocol.PING_SEND))
            {
                SendMessage(CoreProtocol.PING_RECV, socket);
                return true;
            }

            if (msg.Equals(CoreProtocol.PING_RECV))
            {
                return true;
            }
            
            if (msg.Equals(CoreProtocol.MSG_CLOSE_CONNECTION))
            {
                CloseSocket(socket);
                OnDisconnectedSocketEvent?.Invoke(DisconnectionType.Correctly, socket);
                return true;
            }

            return false;
        }

        private enum ServerState
        {
            Closed,
            Open,
            Closing
        }
    }
}