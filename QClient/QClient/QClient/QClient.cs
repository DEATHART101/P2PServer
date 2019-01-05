using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace QClient
{
    class QClient
    {
        #region Defines

        private delegate void CommandHandler();

        #endregion

        private Encoding m_transferEncoding;

        private string m_account;
        private string m_password;

        private IPAddress m_serverIp;
        private int m_serverPort;

        private string[] m_args;
        private Dictionary<string, CommandHandler> m_commandProccessers;

        public QClient(string account, string password, string server_ip, int server_port)
        {
            m_serverIp = IPAddress.Parse(server_ip);
            m_serverPort = server_port;

            m_transferEncoding = Encoding.ASCII;

            m_commandProccessers = new Dictionary<string, CommandHandler>();
        }

        public void Run()
        {
            while (true)
            {
                string console_input = Console.ReadLine();

            }
        }

        private void ProccessInput(string input)
        {
            string command;
            CommandSplit(input, out command, out m_args);

            if (m_commandProccessers.ContainsKey(command))
            {
                var proc = m_commandProccessers[command];
                try
                {
                    proc.Invoke();
                }
                catch (Exception e)
                {

                }
            }
        }

        private void Command_Login()
        {
            if (m_account != null)
            {
                throw new QAlreadyLoginException();
            }

            IPEndPoint server_ep = new IPEndPoint(m_serverIp, m_serverPort);
            Socket server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server_socket.Connect(server_ep);

            m_account = m_args[0];
            m_password = m_args[1];

            // Success
            string to_send = string.Format("{0}_{1}", m_account, m_password);
            server_socket.Send(m_transferEncoding.GetBytes(to_send));

            byte[] recv_data = new byte[128];
            int recv_len = server_socket.Receive(recv_data);
            string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

            server_socket.Close();

            if (recv_string == "lol")
            {
                Print("登录成功！");
            }
            else
            {
                throw new QLoginErrorException();
            }
        }

        private void Command_Logout()
        {
            IPEndPoint server_ep = new IPEndPoint(m_serverIp, m_serverPort);
            Socket server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server_socket.Connect(server_ep);

            // Success
            string to_send = string.Format("logout{0}", m_account);
            server_socket.Send(m_transferEncoding.GetBytes(to_send));

            byte[] recv_data = new byte[128];
            int recv_len = server_socket.Receive(recv_data);
            string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

            server_socket.Close();

            if (recv_string == "loo")
            {
                Print("登出成功！");
            }
        }

        private void CommandSplit(string input, out string command, out string[] args)
        {
            int length = input.Length;
            List<int> split_pos = new List<int>();

            bool in_ref = false;
            for (int i = 0; i < length; i++)
            {
                if (input[i] == ' ')
                {
                    if (!in_ref)
                    {
                        split_pos.Add(i);
                    }
                }
                else if (input[i] == '\"')
                {
                    in_ref = !in_ref;
                }
            }

            if (split_pos.Count == 0)
            {
                command = input;
                args = null;
            }
            else
            {
                int last_pos = split_pos[0];
                command = input.Substring(0, last_pos);

                args = new string[split_pos.Count];
                length = split_pos.Count - 1;
                for (int i = 0; i < length; i++)
                {
                    if (input[last_pos + 1] == '\"')
                    {
                        args[i] = input.Substring(last_pos + 2, split_pos[i + 1] - last_pos - 3);
                    }
                    else
                    {
                        args[i] = input.Substring(last_pos + 1, split_pos[i + 1] - last_pos - 1);
                    }
                    last_pos = split_pos[i + 1];
                }

                last_pos = split_pos.Last();
                if (input[last_pos + 1] == '\"')
                {
                    args[length] = input.Substring(last_pos + 2, input.Length - last_pos - 3);
                }
                else
                {
                    args[length] = input.Substring(last_pos + 1, input.Length - last_pos - 1);
                }
            }
        }

        private static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }
    }
}
