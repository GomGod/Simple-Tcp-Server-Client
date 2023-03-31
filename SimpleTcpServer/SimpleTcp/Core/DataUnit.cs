using System;
using System.Net.Sockets;
using System.Text;

namespace SimpleTcp.Core
{
    internal class DataUnit
    {
        public byte[] buffer;
        public Socket targetSocket;
        public readonly int bufferSize;

        public DataUnit(int size)
        {
            bufferSize = size;
            buffer = new byte[bufferSize];
        }

        public void ClearBuffer()
        {
            Array.Clear(buffer, 0, bufferSize);
        }

        public string GetDecodedString(EncodingFormat format)
        {
            if (!SocketUtility.DecodeMethods.TryGetValue(format, out var decodeMethod))
            {
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            if (decodeMethod == null)
            {
                throw new NullReferenceException();
            }

            return decodeMethod(buffer).Trim('\0');
        }
    }
}