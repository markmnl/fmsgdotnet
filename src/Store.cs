using FMsg;
using MimeTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMsg
{
    internal static class Store
    {

        public static async Task<RejectAcceptCode[]> TryStoreIncomingAsync(FMsgMessage msg, BinaryReader reader) 
        {
            // write message to temp file
            var tmppath = Path.GetTempFileName();
            try
            {
                var recipients = msg.To.Where(r => r.Domain.ToLowerInvariant() == Config.Domain.ToLowerInvariant()).ToArray();
                if (recipients.Count() == 0)
                    throw new FmsgProtocolException($"recieved message has no recipients for {Config.Domain}");
                var size = reader.ReadUInt32();
                if (size > Config.MaxMessageSize)
                    return new RejectAcceptCode[] { RejectAcceptCode.TooBig };

                // download
                using (var fileStream = new FileStream(tmppath, FileMode.CreateNew))
                {
                    // TODO use size
                    await reader.BaseStream.CopyToAsync(fileStream);
                }
                // TODO attachments

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
                return results; 
            }
            finally 
            {
                File.Delete(tmppath);
            }
        }  
    }
}
