using System;
using FmsgExtensions;

namespace FMsg
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new FMsgHost();
            var msg = new FMsgMessage("@markmnl@fmsg.io", new string[] {"@test@example.com"}, "Genisis");

            msg.SetBodyUTF8("Hello fmsg!");
            host.Send(msg, "127.0.0.1:36900");
        }

    }

}



