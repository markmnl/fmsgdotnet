using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMsg
{
    internal static class Config
    {
        public static int Port = 36901;
        public static int RemotePort = 36900;
        public static string Domain { get; set; } = Environment.GetEnvironmentVariable("FMSG_DOMAIN") ?? "mark";
        public static readonly uint MaxMessageSize = 1024 * 10;
        public static bool AllowSkipChallenge = false;
        public static string DataDir { get; set; } = Environment.GetEnvironmentVariable("FMSG_DATA_DIR") ?? "/tmp/fmsgdata4";
        public static string IncomingDir { get; set; } = Path.Join(DataDir, "tmp");
        public static int SendTimeout { get; set; } = int.Parse(Environment.GetEnvironmentVariable("FMSG_SEND_TIMEOUT") ?? "2") * 1000;
        public static int ReceiveTimeout { get; set; } = int.Parse(Environment.GetEnvironmentVariable("FMSG_RECEIVE_TIMEOUT") ?? "2") * 1000;
    }
}
