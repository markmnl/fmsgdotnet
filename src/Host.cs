using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace FMsg
{
    public class FMsgDecodeException : Exception
    {
        public FMsgDecodeException(string msg) : base(msg)
        {
            
        }
    }

    public class FmsgProtocolException : Exception
    {
        public FmsgProtocolException(string msg) : base(msg)
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
        public const int DefaultPort = 36900;

        /// <summary>
        /// Maps messages being sent keyed on header hash
        /// </summary>
        private readonly ConcurrentDictionary<byte[], FMsgMessage> outgoing = new();

        /// <summary>
        /// Maps hash of messages being received keyed on instance of message
        /// </summary>
        private readonly ConcurrentDictionary<FMsgMessage, byte[]> incoming = new();

        private volatile bool listening = false;

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
            var ipEndPoint = new IPEndPoint(IPAddress.Any, Config.Port);
            using Socket listener = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ipEndPoint);
            listener.Listen(100);
            listening = true;
            while (listening)
            {
                try
                {
                    using (var handler = await listener.AcceptAsync())
                    {
                        await HandleConnAsync(handler);
                    }
                }
                catch(Exception ex)
                {
                    // TODO more specific
                    Console.WriteLine(ex.ToString());
                }
            }
            try
            {
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task SendAsync(FMsgMessage msg, int port = DefaultPort)
        {
            var uniqueHosts = msg.To.Select(r => r.Domain).Distinct();
            var header = msg.EncodeHeader();
            using (SHA256 hasher = SHA256.Create())
            {
                var headerHash = hasher.ComputeHash(header);
                outgoing[headerHash] = msg;
            }

            foreach (var host in uniqueHosts)
            {
                try
                {
                    var ipEndPoint = IPEndPoint.Parse($"{host}:{port}");
                    Socket client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    await client.ConnectAsync(ipEndPoint);

                    await client.SendAsync(header);

                    var buffer = new byte[uniqueHosts.Count()];
                    var count = await client.ReceiveAsync(buffer);

                    if (count == 0)
                    {
                        Console.WriteLine($"Failed to send to, {host}: EOF");
                    }
                    else if (count == 1)
                    {
                        Console.WriteLine($"Failed to send to, {host}: REJECTED - {buffer[0]}"); // TODO lookup reason
                    }
                    else
                    {
                        Console.WriteLine($"Failed to send to, {host}: REJECTED - {buffer[0]}")
                    }

                    client.Close();
                    client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send to, {host}: {ex}");
                }
            }
            outgoing.Remove(headerHash);
        }

        private async Task HandleConnAsync(Socket sock)
        {
            using (var stream = new NetworkStream(sock))
            using (var reader = new BinaryReader(stream))
            {
                FMsgMessage msg;
                var remoteEndPoint = (IPEndPoint)sock.RemoteEndPoint;
                var version = reader.ReadByte();
                if(version != 1)
                { 
                    throw new FMsgDecodeException($"Unsupported message version: {version}");
                }
                if(version == 255) // i.e. this is a CHALLENGE
                {
                    var headerHash = reader.ReadBytes(32);
                    if(outgoing.ContainsKey(headerHash))
                    {
                        await HandleChallengeAsync(reader);
                        return;
                    }
                    else 
                    {
                        Console.WriteLine($"Outgoing not found for CHALLENGE from: {remoteEndPoint}");
                        return;
                    }
                }

                msg = FMsgMessage.DecodeHeader(stream);
                msg.RemoteEndPoint = remoteEndPoint;

                // TODO validate header e.g. timestamp

                // CHALLENGE
                if (msg.IsNoChallenge) 
                {
                    if (!Config.AllowSkipChallenge)
                    {
                        // TODO, reject or terminate?
                        return;
                    }
                }
                else 
                {
                    var success = await TryChallengeAsync(msg);
                    if (!success)
                    {
                        return;
                    }
                } 

                var results = await Store.TryStoreIncomingAsync(msg, reader);
                await RejectAcceptAsync(sock, results);
            }

            // gracefully close connection 
            try
            {
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task<bool> TryChallengeAsync(FMsgMessage msg)
        {
            var header = msg.EncodeHeader();
            var ipEndPoint = IPEndPoint.Parse($"{msg.From.Domain}:{msg.RemoteEndPoint.Port}");
            var client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Before connecting, verify domain message is from indeed exists at IP message received from.
            // Otherwise we could challenge an unsuspecting host fraudulent messages purport to be from.
            var addresses = await Dns.GetHostAddressesAsync(msg.From.Domain);
            if (!addresses.Any(a => a == msg.RemoteEndPoint.Address))
            {
                Console.WriteLine($"From domain: {msg.From.Domain}, does not resolve to IP address of sender: {msg.RemoteEndPoint.Address}, got: {addresses}");
                return false;
            }

            await client.ConnectAsync(ipEndPoint);

            // send header hash
            using (var hasher = SHA256.Create())
            {
                var headerHash = hasher.ComputeHash(header);
                await client.SendAsync(headerHash);
            }

            // recieve message hash
            byte[] msgHash = new byte[SHA256.HashSizeInBytes];
            await client.ReceiveAsync(msgHash);

            incoming[msg] = msgHash;

            return true;
        }

        private async Task<bool> HandleChallengeAsync(BinaryReader reader)
        {

            return true;
        }

        private async Task RejectAcceptAsync(Socket sock, RejectAcceptCode[] codes)
        {
            var codeArray = new byte[codes.Length];
            for(var i = 0; i < codes.Length; i++)
            {
                codeArray[i] = (byte)codes[i];
            }
            await sock.SendAsync(codeArray);
        }
    }

}
