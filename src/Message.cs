using FmsgExtensions;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FMsg
{
    [Flags]
    public enum FmsgFlag : byte
    {
        HasPid = 0,
        Important = 1 << 1,
        NoReply = 1 << 2,
        NoChallenge = 1 << 3,

        UnderDuress = 1 << 7

    }

    public class InvalidFmsgException : Exception
    {
        public InvalidFmsgException(string reason) : base(reason)
        {
        }
    }

    public class FMsgMessage
    {
        public const byte Version = 1;
        //public static readonly Regex RecipientRegEx = new Regex("^[\\p{L}\\_\\-]*$"); // TODO underscore or hyphen once consecutively
        
        public FmsgFlag Flags { get; private set; }
        public bool IsImportant { get { return (Flags & FmsgFlag.Important) == FmsgFlag.Important; } }
        public bool IsNoReply { get { return (Flags & FmsgFlag.NoReply) == FmsgFlag.NoReply; } }
        public bool IsNoChallenge { get { return (Flags & FmsgFlag.NoChallenge) == FmsgFlag.NoChallenge; } }
        public bool IsUnderDuress { get { return (Flags & FmsgFlag.UnderDuress) == FmsgFlag.UnderDuress; } }
        public byte[]? Pid { get; set; }
        public FMsgAddress From { get; private set; }
        public FMsgAddress[] To { get; set; } = new FMsgAddress[0];
        public long Timestamp { get; internal set; }
        public string? Topic { get; set; }
        public string Type { get; private set; } = String.Empty;
        public string BodyFilepath { get; private set; }
        public uint? BodySize { get; private set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        // TODO attachments

        private byte[] messageHash;


        public FMsgMessage(string from, string[] to, string? topic = null)
        {
            this.From = FMsgAddress.Parse(from);
            this.To = new FMsgAddress[to.Length];
            for (var i = 0; i < to.Length; i++)
            {
                var addr = FMsgAddress.Parse((string)to[i]);
                this.To[i] = addr;
            }
            this.Topic = topic;
        }

        internal FMsgMessage()
        {
            From = null;
        }

        public void SetBodyUTF8(string body)
        {
            Type = "text/plain; charset=utf-8"; // see https://www.iana.org/assignments/media-types/media-types.xhtml
            var bytes = Encoding.UTF8.GetBytes(body);
            BodyFilepath = Store.StoreOutgoing(this, bytes);
            BodySize = (uint)bytes.Length;
        }

        public void SetImportant() { SetFlag(FmsgFlag.Important); }
        public void SetNoReply() { SetFlag(FmsgFlag.NoReply); }
        public void SetNoChallenge() { SetFlag(FmsgFlag.NoChallenge); }
        public void SetUnderDuress() { SetFlag(FmsgFlag.UnderDuress); }
        public void UnsetImportant() { UnsetFlag(FmsgFlag.Important); }
        public void UnsetNoReply() { UnsetFlag(FmsgFlag.NoReply); }
        public void UnsetNoChallenge() { UnsetFlag(FmsgFlag.NoChallenge); }
        public void UnsetUnderDuress() { UnsetFlag(FmsgFlag.UnderDuress); }

        public void ValidateHeader()
        {
            if (From == null)
                throw new InvalidFmsgException("From is required");
            if (Type == String.Empty)
                throw new InvalidFmsgException("Type is required");
            if (To.Length == 0)
                throw new InvalidFmsgException("At least one recipient in To is required");
            if (Timestamp < 1)
                throw new InvalidFmsgException($"Invalid Timestamp: {Timestamp}");
            if (String.IsNullOrEmpty(BodyFilepath))
                throw new InvalidFmsgException("Body not set");
            if (!File.Exists(BodyFilepath))
                throw new InvalidFmsgException($"Body file not found: {BodyFilepath}");
        }

        public byte[] EncodeHeader()
        {
            // validate this msg first
            ValidateHeader();

            // seralise to the spec: https://github.com/markmnl/fmsg#definition
            using(var stream = new MemoryStream())
            using(var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write((byte)Flags);
                if (Pid != null) 
                {
                    SetFlag(FmsgFlag.HasPid);
                    writer.Write(Pid);
                }
                writer.WriteFmsgAddress(From);
                writer.Write((byte)To.Length);
                foreach(var addr in To)
                {
                    writer.WriteFmsgAddress(addr);
                }
                writer.Write(Timestamp);
                if (Topic == null)
                {
                    writer.Write((byte)0);
                }
                else
                { 
                    writer.WriteUInt8PrefixedUTF8(Topic);
                }
                writer.WriteASCIIPrefixedUTF8(Type);
                return stream.ToArray();
            }
        }

        public static FMsgMessage DecodeHeader(BinaryReader reader)
        {
            var msg = new FMsgMessage();
            msg.Flags = (FmsgFlag)reader.ReadByte();
            if (msg.Flags.HasFlag(FmsgFlag.HasPid))
            {
                msg.Pid = reader.ReadBytes(32);
            }
            msg.From = reader.ReadFmsgAddress();
            msg.Timestamp = reader.ReadInt64();
            var toCount = reader.ReadByte();
            msg.To = new FMsgAddress[toCount];
            for(var i = 0; i < toCount; i++)
            {
                msg.To[i] = reader.ReadFmsgAddress();
            }
            msg.Topic = reader.ReadUInt8PrefixedASCII();
            return msg;
        }

        public async Task<byte[]> CalcMessageHashAsync() 
        {
            if (String.IsNullOrEmpty(BodyFilepath))
                throw new InvalidOperationException("body filepath not net");

            if (messageHash == null)
            {
                using (var hasher = SHA256.Create())
                using (var fileStream = File.OpenRead(BodyFilepath))
                {
                    messageHash = await hasher.ComputeHashAsync(fileStream);
                }
            }

            return messageHash;
        }

        private void SetFlag(FmsgFlag flag)
        {
            Flags |= flag;
        }

        private void UnsetFlag(FmsgFlag flag)
        {
            Flags &= ~flag;
        }

    }
}