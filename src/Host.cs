using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FMsg
{
    public class FMsgHost
    {
        public FMsgHost()
        {

        }   

        public void Send(FMsgMessage msg, string endpoint)
        {
            IPEndPoint ipEndPoint = IPEndPoint.Parse(endpoint);
            Socket client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            client.Connect(ipEndPoint);
            client.Send(msg.Encode());

            var buffer = new byte[1024];
            var count = client.Receive(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, count);
            
            Console.WriteLine(response);
            client.Shutdown(SocketShutdown.Both);
        }
    }

}
