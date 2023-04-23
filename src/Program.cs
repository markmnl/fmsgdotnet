using System;
using System.Net;
using FmsgExtensions;

namespace FMsg
{
    class Program
    {
        private static readonly FMsgHost host = new FMsgHost();
        private static volatile bool processOutgoing = true;

        static async Task Main(string[] args)
        {
            // TODO checks, e.g. data dir exists


            var outTask = ProcessOutgoingAsync(host);
            var inTask = ProcessIncomingAsync(host);

            Console.CancelKeyPress += OnCancelKeyPress;

            await Task.WhenAll(outTask, inTask);

            Console.WriteLine("Bye!");
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
            Console.WriteLine($"Processing outgoing...");
            while (processOutgoing) 
            {
                try 
                {
                    // TODO watch out dir
                    var msg = new FMsgMessage("@markmnl2@localhost", new string[] { "@markmnl@localhost" }, "Genisis");
                    Console.WriteLine($"sending msg {msg.From} --> { String.Join(", ", (object[])msg.To)} {msg.Timestamp}");
                    msg.SetBodyUTF8("Hello fmsg!");
                    await host.SendAsync(msg, Config.RemotePort);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.ToString());
                }
                await Task.Delay(5000);
            }
        }


    }

}



