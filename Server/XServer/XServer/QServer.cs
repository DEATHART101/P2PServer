using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XServer
{
    class QServer
    {
        private int m_port;
        private IPAddress m_address;

        private Socket m_listenSocket;

        public QServer(int port, string listen_ip)
        {
            m_port = port;
            m_address = IPAddress.Parse(listen_ip);
        }

        public void Fire()
        {
            m_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var server_endpoint = new IPEndPoint(m_address, m_port);
            m_listenSocket.Bind(server_endpoint);

            Print("开始监听...");
            m_listenSocket.Listen(10);

            while (true)
            {
                var client_socket = m_listenSocket.Accept();

                byte[] send_data = Encoding.ASCII.GetBytes("Hello client!");
                client_socket.Send(send_data);

                byte[] recv_data = new byte[4096];
                int recv_len = client_socket.Receive(recv_data);
                Print(Encoding.ASCII.GetString(recv_data, 0, recv_len));
            }
        }

        public static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }
    }
}
