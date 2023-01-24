using System;
using FmsgExtensions;

namespace FMsg
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new FMsgHost();
            var processMessagesTask = ProcessMessagesAsync(host);
            var listenConnectionsTask = ListenConnectionsAsync(host);
            

            await Task.WhenAll(processMessagesTask, listenConnectionsTask);
        }

        private static async Task ProcessMessagesAsync(FMsgHost host)
        {
            var msg = new FMsgMessage("@markmnl@localhost", new string[] { "@test@localhost" }, "Genisis");
            msg.SetBodyUTF8("Hello fmsg!");
            host.Send(msg, "127.0.0.1:36900");
        }

        private static async Task ListenConnectionsAsync(FMsgHost host) 
        {
            await host.ListenAsync();
        }

    }

}



