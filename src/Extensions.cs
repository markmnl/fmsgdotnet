using FMsg;
using System.IO;
using System.Text;

namespace FmsgExtensions
{
    public static class FMsgMessageExtensions
    {
        public static FMsgAddress ReadFmsgAddress(this BinaryReader reader)
        {
            var str = reader.ReadUInt8PrefixedUTF8();
            return FMsgAddress.Parse(str);
        }

        public static void WriteFmsgAddress(this BinaryWriter writer, FMsgAddress addr)
        {
            var str = addr.ToString();
            writer.WriteUInt8PrefixedUTF8(str);
        }
    }

    public static class BinaryExtensions
    {
        public static void WriteASCIIPrefixedUTF8(this BinaryWriter writer, string str)
        {
            writer.Write((byte)str.Length);
            writer.Write(Encoding.ASCII.GetBytes(str));
        }

        public static void WriteUInt8PrefixedUTF8(this BinaryWriter writer, string str)
        {
            writer.Write((byte)str.Length);
            writer.Write(Encoding.UTF8.GetBytes(str));
        }

        public static string ReadUInt8PrefixedASCII(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return String.Empty;
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        public static string ReadUInt8PrefixedUTF8(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return String.Empty;
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }
    }

    public static class DateTimeExtensions
    {
        public static DateTime FromTimestamp(this double unixTime)
        {
            return DateTime.UnixEpoch.AddSeconds(unixTime);
        }

        public static double Timestamp(this DateTime date)
        {
            return (date - DateTime.UnixEpoch).TotalSeconds;
        }
    }

}