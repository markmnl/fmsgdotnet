using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FMsg
{
    internal static class Config
    {
        public static int Port = FMsgHost.DefaultPort;
        public static string Domain { get; set; } = Environment.GetEnvironmentVariable("FMSG_DOMAIN") ?? "localhost";
        public static readonly uint MaxMessageSize = 1024 * 10;
        public static bool AllowSkipChallenge = false;
        public static string DataDir { get; set; } = Environment.GetEnvironmentVariable("FMSG_DATA_DIR") ?? "./data";
        public static string IncomingDir { get; set; } = Path.Join(DataDir, "tmp");
    }
}
