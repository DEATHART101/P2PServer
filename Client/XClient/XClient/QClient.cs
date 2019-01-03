using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XClient
{
    class QClient
    {
        Socket m_serverScoekt;

        public QClient()
        {

        }

        public void ConnectServer(
            string server_address,
            int server_port)
        {
            IPAddress server_ip = IPAddress.Parse(server_address);
            IPEndPoint server_ep = new IPEndPoint(server_ip, server_port);

            m_serverScoekt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_serverScoekt.Connect(server_ep);

            // Success
            byte[] recv_data = new byte[4096];
            int recv_len = m_serverScoekt.Receive(recv_data);
            Print(Encoding.ASCII.GetString(recv_data, 0, recv_len));

            byte[] send_data = Encoding.ASCII.GetBytes("Hello to you too!");
            m_serverScoekt.Send(send_data);

            m_serverScoekt.Close();
            m_serverScoekt = null;
        }

        public static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }
    }
}
