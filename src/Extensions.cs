using System.IO;
using System.Text;

namespace FmsgExtensions
{
    public static class BinaryWriterExtensions
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
    }

    public static class DateTimeExtensions
    {
        public static DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime FromTimestamp(this long unixTime)
        {
            return Epoch.AddSeconds(unixTime);
        }

        public static long Timestamp(this DateTime date)
        {
            return Convert.ToInt64((date - Epoch).TotalSeconds);
        }
    }

}