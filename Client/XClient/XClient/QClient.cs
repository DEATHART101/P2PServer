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
        #region Defines

        private delegate void SendCallBackHandler(string[] args);
        private delegate void ReceiveCallBackHandler(Dictionary<string, string> args);

        #endregion

        private Socket m_serverScoekt;

        private Encoding m_inputEncoding;
        private Encoding m_transferEncoding;

        private Dictionary<string, SendCallBackHandler> m_sendCallBacks;
        private Dictionary<string, ReceiveCallBackHandler> m_receiveCallBacks;

        public QClient()
        {
            m_inputEncoding = Encoding.GetEncoding("GB2312");
            m_transferEncoding = Encoding.UTF8;

            // Add commands
            m_sendCallBacks = new Dictionary<string, SendCallBackHandler>()
            {
                { "quit", Send_Command_quit }
            };

            m_receiveCallBacks = new Dictionary<string, ReceiveCallBackHandler>()
            {
                { "006", Receive_Command_006 }
            };
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

            
            while (true)
            {
                string send_string = Console.ReadLine();
                try
                {
                    ProcessSendString(send_string);
                }
                catch (QOnQuitException e)
                {
                    break;
                }
                catch (Exception e)
                {
                    Print(e.Message);
                }
            }

            recieve_thread.Abort();
            m_serverScoekt.Close();
            m_serverScoekt = null;
        }

        private void ProcessSendString(string to_send)
        {
            // Command
            string command;

            int space_pos = to_send.IndexOf(' ');
            if (space_pos == -1)
            {
                command = to_send;
            }
            else
            {
                command = to_send.Substring(0, space_pos);
            }

            if (m_sendCallBacks.ContainsKey(command))
            {
                var call_back = m_sendCallBacks[command];
                var args = Split_Send_Argument(to_send);

                call_back.Invoke(args);
            }

            byte[] send_data = m_inputEncoding.GetBytes(to_send);
            send_data = Encoding.Convert(m_inputEncoding, m_transferEncoding, send_data);
            m_serverScoekt.Send(send_data);
        }

        private void ProcessReceiveString(string received)
        {
            string code = received.Substring(0, 3);

            if (m_receiveCallBacks.ContainsKey(code))
            {
                var call_back = m_receiveCallBacks[code];
                var args = Split_Receive_Argument(received);

                call_back.Invoke(args);
            }
        }

        private void RecieveEverything()
        {
            while (true)
            {
                byte[] recv_data = new byte[4096];
                int recv_len = m_serverScoekt.Receive(recv_data);
                string recv_string = m_transferEncoding.GetString(recv_data, 0, recv_len);
                Print(recv_string);

                try
                {
                    ProcessReceiveString(recv_string);
                }
                catch (Exception e)
                {

                }
            }
        }

        #region Send CallBacks

        private string[] Split_Send_Argument(string to_send)
        {
            string[] result = to_send.Split(' ');

            return SubArray(result, 1);
        }

        private void Send_Command_quit(string[] args)
        {
            throw new QOnQuitException();
        }

        #endregion

        #region Receive CallBacks

        private Dictionary<string, string> Split_Receive_Argument(string received)
        {
            int i_pos = received.IndexOf(':');
            if (i_pos == -1)
            {
                return null;
            }

            Dictionary<string, string> result = new Dictionary<string, string>();
            while (i_pos !=  -1)
            {
                int ref_pos = received.IndexOf('\"', i_pos);
                int next_ref_pos = received.IndexOf('\"', ref_pos + 1);
                string index = received.Substring(i_pos + 1, ref_pos - i_pos - 2);
                string value = received.Substring(ref_pos + 1, next_ref_pos - ref_pos - 1);
                result[index] = value;

                i_pos = received.IndexOf(':', next_ref_pos + 1);
            }

            return result;
        }

        private void Receive_Command_006(Dictionary<string, string> args)
        {
            Print(string.Format("I'm about to send {0} files", args["COUNT"]));
        }

        #endregion

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
