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
        #region Defines

        private delegate string CommandProccesserHandler(string command, string[] args);

        #endregion

        private int m_port;
        private IPAddress m_address;

        private Socket m_listenSocket;

        private List<Socket> m_socketTourists;
        private Dictionary<Socket, QMember> m_socketMembers;
        private List<QChatRoom> m_chatRooms;

        private Dictionary<string, CommandProccesserHandler> m_commandProcessers;

        public QServer(int port, string listen_ip)
        {
            m_port = port;
            m_address = IPAddress.Parse(listen_ip);
            m_socketTourists = new List<Socket>();
            m_socketMembers = new Dictionary<Socket, QMember>();
            m_chatRooms = new List<QChatRoom>();

            Print(string.Format("服务器监听端口被设置为:{0}:{1}", listen_ip, port));

            // Init commands
            m_commandProcessers = new Dictionary<string, CommandProccesserHandler>();
            m_commandProcessers.Add("CONN", Command_CONN);
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
                List<Socket> check_reads = new List<Socket>(m_socketTourists);
                check_reads.Add(m_listenSocket);

                Socket.Select(check_reads, null, null, 1000);

                foreach (Socket socket in check_reads)
                {
                    if (socket.GetHashCode() == m_listenSocket.GetHashCode())
                    {
                        Socket new_socket = m_listenSocket.Accept();
                        m_socketTourists.Add(new_socket);
                    }
                    else
                    {
                        byte[] recv_data = new byte[1024];
                        int recv_len = socket.Receive(recv_data);
                        string recv_string = Encoding.ASCII.GetString(recv_data, 0, recv_len);

                        if (recv_len == 0)
                        {
                            // On client disconnected
                            m_socketTourists.Remove(socket);
                        }
                        else
                        {
                            // Recieved something
                            var socket_ep = socket.LocalEndPoint;
                            Print(string.Format("从{0}接受了{1}字节，内容为\"{2}\"", ((IPEndPoint)socket_ep).Address.ToString(), recv_len, recv_string));

                            Proccess(socket, recv_string);
                        }
                    }
                }
            }
        }

        private void Proccess(Socket socket, string socket_input)
        {
            try
            {
                string[] splits = socket_input.Split(' ');
                string command = splits[0];

                if (m_commandProcessers.ContainsKey(command))
                {
                    var proccesser = m_commandProcessers[command];

                    try
                    {
                        string ret_message = proccesser.Invoke(command, SubArray<string>(splits, 1));
                        byte[] send_data = Encoding.ASCII.GetBytes(ret_message);
                        socket.Send(send_data);
                    }
                    catch (Exception e)
                    {

                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        private string Command_CONN(string command, string[] args)
        {
            return "Hello Client!";
        }

        public static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }

        public static T[] SubArray<T>(T[] ts, int start)
        {
            int length = ts.Length;
            T[] result = new T[length - start];
            for (int i = start; i < length; i++)
            {
                result[i - start] = ts[i];
            }

            return result;
        }
    }
}
