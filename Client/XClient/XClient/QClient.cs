using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

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
            Print("已经连接至服务器");
            ThreadStart thread_start = new ThreadStart(RecieveEverything);
            Thread recieve_thread = new Thread(thread_start);

            recieve_thread.Start();

            Encoding gb2312 = Encoding.GetEncoding("GB2312");
            Encoding utf8 = Encoding.UTF8;
            while (true)
            {
                string send_string = Console.ReadLine();
                if (send_string == "quit")
                {
                    break;
                }

                byte[] send_data = gb2312.GetBytes(send_string);
                send_data = Encoding.Convert(gb2312, utf8, send_data);
                m_serverScoekt.Send(send_data);
            }

            m_serverScoekt.Close();
            m_serverScoekt = null;
        }

        private void RecieveEverything()
        {
            while (true)
            {
                byte[] recv_data = new byte[4096];
                int recv_len = m_serverScoekt.Receive(recv_data);
                Print(Encoding.UTF8.GetString(recv_data, 0, recv_len));
            }
        }

        public static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }
    }
}
