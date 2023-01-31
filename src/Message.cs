using FmsgExtensions;
using System.Net;
using System.Security.Cryptography;

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
        public long Timestamp { get; set; }
        public string? Topic { get; set; }
        public string Type { get; private set; } = String.Empty;
        public string BodyFilepath { get; private set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        // TODO attachments


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

        // public void SetBody(string mimeType, byte[] body)
        // {
        //     var count = Encoding.ASCII.GetByteCount(mimeType);
        //     if (count > byte.MaxValue)
        //         throw new ArgumentException($"Too long mime-type: {mimeType}");
        //     Type = mimeType;
        //     Body = body;
        // }

        public void SetBodyUTF8(string filepath)
        {
            if(!File.Exists(filepath))
            {
                throw new FileNotFoundException($"{filepath} not found");
            }
            Type = "text/plain;charset=utf-8"; // see https://www.iana.org/assignments/media-types/media-types.xhtml
            BodyFilepath = filepath;
        }

        public void SetImportant() { SetFlag(FmsgFlag.Important); }
        public void SetNoReply() { SetFlag(FmsgFlag.NoReply); }
        public void SetNoChallenge() { SetFlag(FmsgFlag.NoChallenge); }
        public void SetUnderDuress() { SetFlag(FmsgFlag.UnderDuress); }
        public void UnsetImportant() { UnsetFlag(FmsgFlag.Important); }
        public void UnsetNoReply() { UnsetFlag(FmsgFlag.NoReply); }
        public void UnsetNoChallenge() { UnsetFlag(FmsgFlag.NoChallenge); }
        public void UnsetUnderDuress() { UnsetFlag(FmsgFlag.UnderDuress); }

        public byte[] EncodeHeader()
        {
            // validate this msg first
            if (From == null)
                throw new InvalidFmsgException("From is required");
            if (Type == String.Empty)
                throw new InvalidFmsgException("Type is required");
            if (To.Length == 0)
                throw new InvalidFmsgException("At least one recipient in To is required");

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

        public static FMsgMessage DecodeHeader(Stream stream)
        {
            var msg = new FMsgMessage();
            using(var reader = new BinaryReader(stream))
            {
                msg.Flags = (FmsgFlag)reader.ReadByte();
                if (msg.Flags.HasFlag(FmsgFlag.HasPid))
                {
                    msg.Pid = reader.ReadBytes(32);
                    // TODO check pid known
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
            }
            return msg;
        }

        public Task<byte[]> CalcMessageHashAsync() 
        {
            if (String.IsNullOrEmpty(BodyFilepath))
                throw new InvalidOperationException("body filepath not net");

            using(var hasher = SHA256.Create())
            using (var fileStream = File.OpenRead(BodyFilepath))
            {
                return await hasher.ComputeHashAsync(fileStream); 
            }
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