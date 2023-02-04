using FmsgExtensions;
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

        private volatile bool listening = false;
        private Socket listener;

        public FMsgHost()
        {
        }

        public void StopListening()
        {
            if (listening == false)
                throw new InvalidOperationException("Cannot stop becuase not listening");
            listening = false;
            try
            {
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
            }
            catch { }
        }

        public async Task ListenAsync()
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Any, Config.Port);
            listener = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(ipEndPoint);
            listener.Listen(100);
            listening = true;
            Console.WriteLine($"Listening on {ipEndPoint}...");
            while (listening)
            {
                try
                {
                    using (var socket = await listener.AcceptAsync())
                    {
                        socket.SendTimeout = Config.SendTimeout;
                        socket.ReceiveTimeout = Config.ReceiveTimeout;
                        await HandleConnAsync(socket);
                    }
                }
                catch (Exception ex)
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
            msg.ValidateHeader();
            var uniqueHosts = msg.To.Select(r => r.Domain).Distinct();
            var header = msg.EncodeHeader();
            using var hasher = SHA256.Create();
            var headerHash = hasher.ComputeHash(header);
            outgoing[headerHash] = msg;
            try
            {
                foreach (var host in uniqueHosts)
                {
                    try
                    {
                        Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        client.SendTimeout = Config.SendTimeout;
                        client.ReceiveTimeout = Config.ReceiveTimeout;
                        await client.ConnectAsync(host, port);
                        await client.SendAsync(header);
                        var hostRecipients = msg.To.Where(addr => addr.Domain == host).ToArray();
                        var buffer = new byte[hostRecipients.Length];
                        var count = await client.ReceiveAsync(buffer);
                        if (count == 0)
                        {
                            Console.WriteLine($"Failed to send to, {host}: EOF");
                        }
                        else if (count == 1)
                        {
                            Console.WriteLine($"Failed to send to, {host}: REJECTED - {(RejectAcceptCode)buffer[0]}");
                        }
                        else
                        {

                            if (count != hostRecipients.Length)
                            {
                                Console.WriteLine($"Recieved unexpected number of RejectAccept codes, expected: {hostRecipients.Length}, got: {count}");
                            }
                            else
                            {
                                for (var i = 0; i < count; i++)
                                {
                                    var code = (RejectAcceptCode)buffer[i];
                                    Console.WriteLine($"\t{hostRecipients[i]}\t{code}");
                                }
                            }
                        }
                        client.Close();
                        client.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception sending to, {host}: {ex}");
                    }
                }
            }
            finally
            {
                FMsgMessage removedMsg;
                outgoing.TryRemove(headerHash, value: out removedMsg);
            }
        }

        private async Task HandleConnAsync(Socket sock)
        {
            using (var stream = new NetworkStream(sock))
            using (var reader = new BinaryReader(stream))
            {
                FMsgMessage msg;
                var remoteEndPoint = (IPEndPoint)sock.RemoteEndPoint;
                var version = reader.ReadByte();
                if (version != 1)
                {
                    throw new FMsgDecodeException($"Unsupported message version: {version}");
                }
                if (version == 255) // i.e. this is a CHALLENGE
                {
                    var headerHash = reader.ReadBytes(32);
                    FMsgMessage outgoingMsg;
                    if (outgoing.TryGetValue(headerHash, out outgoingMsg))
                    {
                        var msgHash = await outgoingMsg.CalcMessageHashAsync();
                        await sock.SendAsync(msgHash);
                    }
                    else
                    {
                        throw new FmsgProtocolException($"Outgoing not found for CHALLENGE from: {remoteEndPoint}");
                    }
                    return;
                }

                msg = FMsgMessage.DecodeHeader(reader);
                msg.RemoteEndPoint = remoteEndPoint;
                var size = reader.ReadUInt32();

                //  TODO check pid if present

                //  TODO check timestamp

                //  check any recipients for us
                var recipients = msg.To.Where(r => String.Equals(r.Domain, Config.Domain, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (recipients.Length == 0)
                    throw new FmsgProtocolException($"recieved message has no recipients for {Config.Domain}");
                
                // check message size
                RejectAcceptCode[] response;
                if (size > Config.MaxMessageSize)
                {
                    response = new RejectAcceptCode[] { RejectAcceptCode.TooBig };
                }
                else
                {
                    // CHALLENGE
                    byte[]? incomingMessageHash = null;
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
                        incomingMessageHash = await ChallengeAsync(msg);
                    }

                    response = await Store.StoreIncomingAsync(msg, recipients, stream, size, incomingMessageHash);
                }

                await RejectAcceptAsync(sock, response);
            }

            // gracefully close connection since we got this far
            try
            {
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task<byte[]> ChallengeAsync(FMsgMessage msg)
        {
            var header = msg.EncodeHeader();
            var ipEndPoint = IPEndPoint.Parse($"{msg.From.Domain}:{msg.RemoteEndPoint.Port}");
            var client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Before connecting, verify domain message is from indeed exists at IP message received from.
            // Otherwise we could challenge an unsuspecting host fraudulent messages purport to be from.
            //
            var addresses = await Dns.GetHostAddressesAsync(msg.From.Domain);
            if (!addresses.Any(a => a == msg.RemoteEndPoint.Address))
            {
                throw new FmsgProtocolException($"From domain: {msg.From.Domain}, does not resolve to an IP address for sender: {msg.RemoteEndPoint.Address}, got: {addresses}");
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

            return msgHash;
        }

        private async Task RejectAcceptAsync(Socket sock, RejectAcceptCode[] codes)
        {
            var codeArray = new byte[codes.Length];
            for (var i = 0; i < codes.Length; i++)
            {
                codeArray[i] = (byte)codes[i];
            }
            await sock.SendAsync(codeArray);
        }
    }

}
