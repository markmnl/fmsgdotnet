namespace fmsgdotnet
{
    class BuffersComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? a, byte[]? b)
        {
            return a.SequenceEqual(b);
        }

        public int GetHashCode(byte[] b)
        {
            return BitConverter.ToInt32(b, 0);
        }
    }

    static class BufferExtensions
    {
        public static string ToHexString(this byte[] buffer) 
        {
            var str = BitConverter.ToString(buffer);
            return str.Replace("-", "").ToLowerInvariant();
        }

        public static string ToShortHexString(this byte[] buffer, int length = 5)
        {
            var str = BitConverter.ToString(buffer);
            return str.Replace("-", "").ToLowerInvariant().Substring(0, length);
        }
    }
}
