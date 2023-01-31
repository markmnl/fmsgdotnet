using System;
using FmsgExtensions;

namespace FMsg
{
    class Program
    {
        private static readonly FMsgHost host = new FMsgHost();
        private static volatile bool processOutgoing = false;

        static async Task Main(string[] args)
        {
            var processMessagesTask = ProcessOutgoingAsync(host);
            var listenConnectionsTask = ProcessIncomingAsync(host);

            Console.CancelKeyPress += OnCancelKeyPress;
            processOutgoing = true;
            
            await Task.WhenAll(processMessagesTask, listenConnectionsTask);
        }

        private static void Stop()
        {
            Console.WriteLine("Stopping...");
            host.StopListening();
            processOutgoing = false;
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            Stop();
        }

        private static async Task ProcessIncomingAsync(FMsgHost host)
        {
            await host.ListenAsync();
        }

        private static async Task ProcessOutgoingAsync(FMsgHost host)
        {
            while (processOutgoing) 
            {
                try 
                {
                    // TODO watch out dir
                    var msg = new FMsgMessage("@markmnl@localhost", new string[] { "@test@localhost" }, "Genisis");
                    msg.SetBodyUTF8("Hello fmsg!");
                    await host.SendAsync(msg, "127.0.0.1:36900");
                    await Task.Delay(5000);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }


    }

}



