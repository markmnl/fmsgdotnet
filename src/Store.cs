using FmsgExtensions;
using MimeTypes;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace FMsg
{
    internal static class Store
    {
        public static string StoreOutgoing(FMsgMessage msg, byte[] data)
        {
            var ext = MimeTypeMap.GetExtension(msg.Type, throwErrorIfNotFound: false);
            if (msg.Timestamp == default)
                msg.Timestamp = DateTime.UtcNow.Timestamp();
            var filepath = Path.Join(Config.DataDir, msg.Timestamp.ToString(), ext);

            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
            File.WriteAllBytes(filepath, data);

            return filepath;
        }


        public static async Task<RejectAcceptCode[]> StoreIncomingAsync(FMsgMessage msg, 
            FMsgAddress[] recipients, 
            NetworkStream stream, 
            uint size, 
            byte[]? messageHash) 
        {
            var tmppath = Path.GetTempFileName();
            try
            {
                // download to temp file
                using (var fileStream = new FileStream(tmppath, FileMode.CreateNew))
                {
                    long recieved = 0;
                    var buffer = new byte[8192];
                    while(recieved < size) 
                    {
                        var bytesRead = await stream.ReadAsync(buffer);
                        fileStream.Write(buffer, 0, bytesRead); // TODO worth async ?
                        recieved += bytesRead;
                    }
                }
                // TODO attachments
                
                // verify matches hash
                if (messageHash != null) 
                {
                    using (var hasher = SHA256.Create())
                    using (var fileStream = File.OpenRead(tmppath))
                    {
                        var downloadedHash = await hasher.ComputeHashAsync(fileStream);
                        if (!downloadedHash.SequenceEqual(messageHash))
                        {
                            throw new FmsgProtocolException("Actual message hash mismatch with challenge response!");
                        }
                    }
                }

                // copy message to recipients dirs
                var ext = MimeTypeMap.GetExtension(msg.Type, false);
                var filename = msg.Timestamp.ToString() + ext;
                var results = new RejectAcceptCode[recipients.Length];
                for (var i = 0; i < recipients.Length;  i++)
                {
                    var addr = recipients[i];
                    var filepath = Path.Combine(Config.DataDir, addr.User, filename);
                    File.Copy(tmppath, filepath, false);
                    results[i] = RejectAcceptCode.Accept;
                }

                // TODO save detail to database

                return results; 
            }
            finally 
            {
                File.Delete(tmppath);
            }
        }  
    }
}
