using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FmsgExtensions;
using System.Security.Cryptography;

namespace FMsg
{
    [Flags]
    public enum FmsgFlag : byte
    {
        HasPid = 0,
        Important = 1 << 1,
        NoReply = 1 << 2,
        NoVerify = 1 << 3,
        UnderDuress = 1 << 7

    }

    public class InvalidFmsgAddressException : Exception
    {
        public InvalidFmsgAddressException(string reason) : base(reason)
        {
        }
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
        public bool IsNoVerify { get { return (Flags & FmsgFlag.NoVerify) == FmsgFlag.NoVerify; } }
        public bool IsUnderDuress { get { return (Flags & FmsgFlag.UnderDuress) == FmsgFlag.UnderDuress; } }
        public byte[]? Pid { get; set; }
        public string From { get; private set; }
        public string[] To { get; set; } = new string[0];
        public long Timestamp { get; set; }
        public string? Topic { get; set; }
        public string Type { get; private set; } = String.Empty;
        public string BodyFilepath { get; private set; }
        // TODO attachments


        public FMsgMessage(string from, string[] to, string? topic = null)
        {
            ValidateAddress(from);
            this.From = from;
            foreach(var addr in to)
            {
                ValidateAddress(addr);
            }
            this.To = to;
            this.Topic = topic;
        }

        internal FMsgMessage()
        {
            From = "";
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

        public static void ValidateAddress(string address)
        {
            if (String.IsNullOrWhiteSpace(address))
                throw new InvalidFmsgAddressException("Null or empty");
            if (!address.StartsWith('@'))
                throw new InvalidFmsgAddressException("Missing leading '@' character");
            var i = address.LastIndexOf("@");
            if (i < 1)
                throw new InvalidFmsgAddressException("Missing a second '@' seperating recipient and domain");
            var recipient = address.Substring(1, i);
            // if (!RecipientRegEx.IsMatch(recipient))
            //     throw new InvalidFmsgAddressException($"Invalid recipient part: {recipient}");
            var domain = address.Substring(i+1);
            if (System.Uri.CheckHostName(domain) == System.UriHostNameType.Unknown)
                throw new InvalidFmsgAddressException($"Invalid domain part: {domain}");
        }

        public void SetImportant() { SetFlag(FmsgFlag.Important); }
        public void SetNoReply() { SetFlag(FmsgFlag.NoReply); }
        public void SetNoVerify() { SetFlag(FmsgFlag.NoVerify); }
        public void SetUnderDuress() { SetFlag(FmsgFlag.UnderDuress); }
        public void UnsetImportant() { UnsetFlag(FmsgFlag.Important); }
        public void UnsetNoReply() { UnsetFlag(FmsgFlag.NoReply); }
        public void UnsetNoVerify() { UnsetFlag(FmsgFlag.NoVerify); }
        public void UnsetUnderDuress() { UnsetFlag(FmsgFlag.UnderDuress); }

        public byte[] EncodeHeader()
        {
            // validate this msg first
            if (From == String.Empty)
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
                writer.WriteUInt8PrefixedUTF8(From);
                writer.Write((byte)To.Length);
                foreach(var addr in To)
                {
                    writer.WriteUInt8PrefixedUTF8(addr);
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
                msg.From = reader.ReadUInt8PrefixedUTF8();
                msg.Timestamp = reader.ReadInt64();
                var toCount = reader.ReadByte();
                msg.To = new string[toCount];
                for(var i = 0; i < toCount; i++)
                {
                    msg.To[i] = reader.ReadUInt8PrefixedUTF8();
                }
                msg.Topic = reader.ReadUInt8PrefixedASCII();
                
                    
            }
            return msg;
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