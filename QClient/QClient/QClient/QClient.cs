using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace QClient
{
    class QClient
    {
        #region Defines

        private delegate void InputCommandHandler();
        private delegate string ReceiveCommandHandler();

        #endregion

        private Socket m_listenSocket;
        private Thread m_receiveThread;

        private Encoding m_inputEncoding;
        private Encoding m_transferEncoding;

        private bool m_logined;

        private string m_account;
        private string m_password;

        private int m_runningPort;

        private IPAddress m_serverIp;
        private int m_serverPort;

        private string[] m_inputArgs;
        private Dictionary<string, InputCommandHandler> m_inputProccessers;

        private bool m_checkInput;

        private List<QFriend> m_listOfFriend;

        private Dictionary<string, string> m_receiveArgs;
        private Dictionary<string, ReceiveCommandHandler> m_receiveProccessers;

        public QClient(string server_ip, int server_port, int client_port)
        {
            IPAddress local_ip = IPAddress.Parse("127.0.0.1");
            m_runningPort = client_port;
            IPEndPoint local_ep = new IPEndPoint(local_ip, m_runningPort);

            m_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_listenSocket.Bind(local_ep);
            m_listenSocket.Listen(100);

            m_serverIp = IPAddress.Parse(server_ip);
            m_serverPort = server_port;

            m_logined = false;

            m_inputEncoding = Encoding.GetEncoding("GB2312");
            m_transferEncoding = Encoding.UTF8;

            m_checkInput = true;

            m_listOfFriend = new List<QFriend>();

            Print(string.Format("客户端成功启动！"));

            m_inputProccessers = new Dictionary<string, InputCommandHandler>();
            m_inputProccessers.Add("LOGIN", Input_LOGIN);
            m_inputProccessers.Add("LOGOUT", Input_LOGOUT);
            m_inputProccessers.Add("TALK", Input_TALK);

            m_receiveProccessers = new Dictionary<string, ReceiveCommandHandler>();
            m_receiveProccessers.Add("TALK", Recevie_TALK);
        }

        public void Run()
        {
            // Debug
            DebugSetting();

            ThreadStart ts = new ThreadStart(ReceiveTask);
            m_receiveThread = new Thread(ts);
            m_receiveThread.Start();

            while (true)
            {
                string console_input = Console.ReadLine();

                // Convert to UTF8
                byte[] bytes = m_inputEncoding.GetBytes(console_input);
                bytes = Encoding.Convert(m_inputEncoding, m_transferEncoding, bytes);
                console_input = m_transferEncoding.GetString(bytes);

                ProccessInput(console_input);
            }
        }

        private void DebugSetting()
        {
            //m_listOfFriend.Add())
            m_account = "awer";
            m_password = "awer";
            m_logined = true;

            m_listOfFriend.Add(new QFriend("a", "127.0.0.1"));
        }

        private void ProccessInput(string input)
        {
            string command;

            // Set context
            m_checkInput = true;
            InputSplit(input, out command, out m_inputArgs);

            if (m_inputProccessers.ContainsKey(command))
            {
                var proc = m_inputProccessers[command];
                try
                {
                    proc.Invoke();
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }
            }
            else
            {
                Print("输入了无效的指令！");
            }
        }

        private void ProccessReceive(Socket socket, string received)
        {
            string command;

            // Set context 
            m_checkInput = false;
            Split_Receive_Argument(received, out command, out m_receiveArgs);

            string ret_string = "(Empty)";

            try
            {
                if (command == null)
                {
                    throw new QInvalidReceiveException();
                }

                var recv_proc = m_receiveProccessers[command];

                ret_string = recv_proc.Invoke();
            }
            catch (QInvalidReceiveException)
            {

            }
            catch (QInvalidArumentException)
            {

            }
            catch (Exception e)
            {
                Print(e.Message);
            }

            socket.Send(m_transferEncoding.GetBytes(ret_string));
        }

        #region Tasks

        private void ReceiveTask()
        {
            while (true)
            {
                Socket pier_socket = m_listenSocket.Accept();

                byte[] recv_data = new byte[4096];
                int recv_len = pier_socket.Receive(recv_data);
                string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

                ProccessReceive(pier_socket, recv_string);

                pier_socket.Close();
            }
        }

        #endregion

        #region Command Operations

        private string SendToAccount(string account, string content)
        {
            QFriend friend = FindFriend_Account(account);
            string friend_ip;
            if (friend == null)
            {
                friend_ip = Server_CHECKFRIEND(account);
                if (friend_ip != null)
                {
                    friend = new QFriend(account, friend_ip);
                    m_listOfFriend.Add(friend);
                }
            }
            else
            {
                friend_ip = friend.Address;
            }

            return SendToAddress(friend_ip, content);
        }

        private string SendToAddress(string address, string content)
        {
            IPAddress remote_ip = IPAddress.Parse(address);
            IPEndPoint frient_ep = new IPEndPoint(remote_ip, m_runningPort);
            Socket friend_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            friend_socket.Connect(frient_ep);

            friend_socket.Send(m_transferEncoding.GetBytes(content));

            byte[] recv_data = new byte[4096];
            int recv_len = friend_socket.Receive(recv_data);
            string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

            friend_socket.Close();

            return recv_string;
        }

        private QFriend FindFriend_Account (string account)
        {
            foreach (var friend in m_listOfFriend)
            {
                if (friend.Name == account)
                {
                    return friend;
                }
            }

            return null;
        }

        private QFriend FindFriend_Address(string address)
        {
            foreach (var friend in m_listOfFriend)
            {
                if (friend.Address == address)
                {
                    return friend;
                }
            }

            return null;
        }

        #region Server Operations

        private void Server_LOGIN(string account, string password)
        {
            IPEndPoint server_ep = new IPEndPoint(m_serverIp, m_serverPort);
            Socket server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server_socket.Connect(server_ep);

            // Success
            string to_send = string.Format("{0}_{1}", account, password);
            server_socket.Send(m_transferEncoding.GetBytes(to_send));

            byte[] recv_data = new byte[128];
            int recv_len = server_socket.Receive(recv_data);
            string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

            server_socket.Close();

            if (recv_string != "lol")
            {
                throw new QLoginErrorException();
            }
        }

        private void Server_LOGOUT()
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

            if (recv_string != "loo")
            {
                throw new QLogoutErrorException();
            }
        }

        private string Server_CHECKFRIEND(string account)
        {
            IPEndPoint server_ep = new IPEndPoint(m_serverIp, m_serverPort);
            Socket server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server_socket.Connect(server_ep);

            // Success
            string to_send = string.Format("q{0}", account);
            server_socket.Send(m_transferEncoding.GetBytes(to_send));

            byte[] recv_data = new byte[128];
            int recv_len = server_socket.Receive(recv_data);
            string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);

            server_socket.Close();

            if (recv_string.Length < 3 || recv_string[0] == 'n')
            {
                throw new QFriendNotOnlineException();
            }

            return recv_string;
        }

        #endregion

        #region Input Operations

        private void Input_LOGIN()
        {
            if (m_logined)
            {
                throw new QAlreadyLoginException();
            }

            CheckArgumentCount(2);

            m_account = m_inputArgs[0];
            m_password = m_inputArgs[1];

            Server_LOGIN(m_account, m_password);

            Print("登录成功！");
            m_logined = true;
        }

        private void Input_LOGOUT()
        {
            CheckLogin();

            Server_LOGOUT();

            Print("登出成功！");
            m_logined = false;
        }

        private void Input_TALK()
        {
            CheckLogin();
            CheckArgumentCount(2);

            string talk_to_user = m_inputArgs[0];
            string content = m_inputArgs[1];

            Dictionary<string, string> send_args = new Dictionary<string, string>()
            {
                { "Command", "TALK" },
                { "FROM", m_account.ToString() },
                { "CONTENT", content }
            };

            string to_send = SetUpSendString(send_args);

            string recv_code = SendToAccount(talk_to_user, to_send);
            CheckCode(recv_code);
            Print("发送成功!");
        }

        private string SetUpSendString(Dictionary<string, string> args)
        {
            string command = args["Command"];

            string result = command + ":";
            foreach (var pair in args)
            {
                result += string.Format("{0} \"{1}\",", pair.Key, pair.Value);
            }

            return result.Remove(result.Length - 1);
        }

        #endregion

        #region Receive Operations

        private string Recevie_TALK()
        {
            string firend_account = m_receiveArgs["FROM"];
            string content = m_receiveArgs["CONTENT"];

            Print(string.Format("{0}:{1}", firend_account, content));

            return "000";
        }

        #endregion

        #endregion

        #region Validations

        private void CheckArgumentCount(int length, bool strict = false)
        {
            int args_length = 0;
            if (m_checkInput)
            {
                args_length = m_inputArgs == null ? 0 : m_inputArgs.Length;
            }
            else
            {
                args_length = m_receiveArgs == null ? 0 : m_receiveArgs.Count;
            }

            if (strict)
            {
                if (args_length != length)
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                if (args_length < length)
                {
                    throw new ArgumentException();
                }
            }
        }

        private void CheckLogin()
        {
            if (!m_logined)
            {
                throw new QNotlogedinException();
            }
        }

        private void CheckCode(string code)
        {
            if (code != "000")
            {
                throw new QFrientRefuseException(); 
            }
        }

        #endregion

        private void InputSplit(string input, out string command, out string[] args)
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

        private void Split_Receive_Argument(string received, out string command, out Dictionary<string, string> args)
        {
            int i_pos = received.IndexOf(':');
            if (i_pos == -1)
            {
                command = null;
                args = null;
                return;
            }

            command = received.Substring(0, i_pos);

            Dictionary<string, string> result = new Dictionary<string, string>();
            while (i_pos != -1)
            {
                int ref_pos = received.IndexOf('\"', i_pos);
                int next_ref_pos = received.IndexOf('\"', ref_pos + 1);
                string index = received.Substring(i_pos + 1, ref_pos - i_pos - 2);
                string value = received.Substring(ref_pos + 1, next_ref_pos - ref_pos - 1);
                result[index] = value;

                i_pos = received.IndexOf(',', next_ref_pos + 1);
            }

            args = result;
        }

        private static void Print(object stuff)
        {
            Console.WriteLine(stuff);
        }
    }
}
