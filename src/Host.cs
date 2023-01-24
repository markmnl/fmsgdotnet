using FmsgExtensions;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;

namespace FMsg
{
    public class FMsgDecodeException : Exception
    {
        public FMsgDecodeException(string msg) : base(msg)
        {
            
        }
    }

    public enum RejectAcceptCode : byte
    {
        Undisclosed = 1,
        TooBig = 2,
        InsufficentResources = 3,
        ParentNotFound = 4,
        PastTime = 5,
        FutureTime = 6,

        UserUnknown = 100,
        UserFull = 101,

        Accept = 255
    }

    public class FMsgHost
    {
        private readonly Dictionary<byte[], FMsgMessage> outgoing = new Dictionary<byte[], FMsgMessage>();
        private bool listening = false;

        public FMsgHost()
        {
        }   

        public void StopListening()
        {
            if (listening == false)
                throw new InvalidOperationException("Cannot stop becuase not listening");
            listening = false;
            // TODO should we block till active sockets closed?
        }

        public async Task ListenAsync()
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Any, 36900);
            using Socket listener = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ipEndPoint);
            listener.Listen(100);
            listening = true;
            while (listening)
            {
                try
                {
                    var handler = await listener.AcceptAsync();
                    await HandleConnAsync(handler);
                }
                catch(Exception ex)
                {
                    // TODO more specific
                }
            }
            try
            {
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
            }
            catch {}
        }

        public async Task SendAsync(FMsgMessage msg, string endpoint)
        {
            IPEndPoint ipEndPoint = IPEndPoint.Parse(endpoint);
            Socket client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            client.Connect(ipEndPoint);
            var header = msg.EncodeHeader();
            using(SHA256 hasher = SHA256.Create())
            {
                var headerHash = hasher.ComputeHash(header);
                outgoing[headerHash] = msg;
            }
            client.Send(header);

            var buffer = new byte[1600];
            var count = client.Receive(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, count);
            
            Console.WriteLine(response);
            client.Close();
            client.Shutdown(SocketShutdown.Both);
        }

        private async Task HandleConnAsync(Socket sock)
        {
            var buffer = new byte[1600];
            while (true)
            {
                var received = await sock.ReceiveAsync(buffer, SocketFlags.None);
                if (received == 0) // EOF
                    break; 
                
                FMsgMessage msg;
                using(var stream = new MemoryStream(buffer, 0, received))
                using(var reader = new BinaryReader(stream))
                {
                    var version = reader.ReadByte();
                    if(version != 1)
                    { 
                        throw new FMsgDecodeException($"Unsupported message version: {version}");
                    }
                    if(version == 255) // i.e. this is a CHALLENGE
                    {
                        var headerHash = reader.ReadBytes(32);
                        if(!outgoing.ContainsKey(headerHash))
                        {
                            // TODO log unknown challenge dropping conn
                            return;    
                        }
                        break;
                    }
                    msg = FMsgMessage.DecodeHeader(stream);
                }
            }

            // gracefully close connection 
            try
            {
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
            }
            catch(Exception)
            { 
            }
        }

        private async Task<bool> HandleChallengeAsync(Socket sock, BinaryReader reader)
        {

            return true;
        }

        private async Task RejectAccept(Socket sock, FMsgMessage msg, List<RejectAcceptCode> codes)
        {
            var codeArray = new byte[codes.Count];
            for(var i = 0; i < codes.Count; i++)
            {
                codeArray[i] = (byte)codes[i];
            }
            await sock.SendAsync(codeArray);
        }
    }

}
