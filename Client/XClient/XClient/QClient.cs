using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace XClient
{
    class QClient
    {
        #region Defines

        private delegate void SendCallBackHandler(string[] args);
        private delegate void ReceiveCallBackHandler(Dictionary<string, string> args);

        private delegate void OnFileReceiveCompleteHandler();
        private delegate void OnFileSendCompleteHandler();

        #endregion

        private Socket m_serverScoekt;

        private string m_tempFileRoot;

        private Encoding m_inputEncoding;
        private Encoding m_transferEncoding;

        private Dictionary<string, SendCallBackHandler> m_sendCallBacks;
        private Dictionary<string, ReceiveCallBackHandler> m_receiveCallBacks;

        public QClient()
        {
            m_tempFileRoot = "./Temp/";

            m_inputEncoding = Encoding.GetEncoding("GB2312");
            m_transferEncoding = Encoding.UTF8;

            // Add commands
            m_sendCallBacks = new Dictionary<string, SendCallBackHandler>()
            {
                { "quit", Send_Command_quit },
                { "FILE", Send_Command_File }
            };

            m_receiveCallBacks = new Dictionary<string, ReceiveCallBackHandler>()
            {
                { "006", Receive_Command_006 },
                { "007", Receive_Command_007 }
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

        #region Thread tasks

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

        #region File Transfer

        #region Receive

        private Thread m_fileReceiveThread;

        private void ReceiveFileFromSocket(
            string file_name,
            Socket socket,
            int file_size,
            OnFileReceiveCompleteHandler call_back
            )
        {
            var file_param = new ReceiveFileParameter(
                file_name,
                socket,
                file_size,
                call_back);
            ParameterizedThreadStart ts = new ParameterizedThreadStart(ReceiveFileFromSocket_Task);
            m_fileReceiveThread = new Thread(ts);
            m_fileReceiveThread.Start(file_param);
        }

        // Parameters
        private class ReceiveFileParameter
        {
            private string m_receiveFileFileName;
            private Socket m_receiveFileSocket;
            private int m_receiveFileFileSize;
            private OnFileReceiveCompleteHandler m_receiveFileFinish;

            public ReceiveFileParameter(string m_receiveFileFileName, Socket m_receiveFileSocket, int m_receiveFileFileSize, OnFileReceiveCompleteHandler m_receiveFileFinish)
            {
                this.m_receiveFileFileName = m_receiveFileFileName;
                this.m_receiveFileSocket = m_receiveFileSocket;
                this.m_receiveFileFileSize = m_receiveFileFileSize;
                this.m_receiveFileFinish = m_receiveFileFinish;
            }

            public string ReceiveFileFileName { get => m_receiveFileFileName; set => m_receiveFileFileName = value; }
            public Socket ReceiveFileSocket { get => m_receiveFileSocket; set => m_receiveFileSocket = value; }
            public int ReceiveFileFileSize { get => m_receiveFileFileSize; set => m_receiveFileFileSize = value; }
            public OnFileReceiveCompleteHandler ReceiveFileFinish { get => m_receiveFileFinish; set => m_receiveFileFinish = value; }
        }
        
        private void ReceiveFileFromSocket_Task(object file_param)
        {
            ReceiveFileParameter param = file_param as ReceiveFileParameter;

            FileStream fs = new FileStream(m_tempFileRoot + param.ReceiveFileFileName, FileMode.CreateNew);

            byte[] recv_data = new byte[4096];
            int recv_total = 0;
            while (true)
            {
                int recv_len = param.ReceiveFileSocket.Receive(recv_data);
                fs.Write(recv_data, 0, recv_len);

                recv_total += recv_len;

                if (recv_total == param.ReceiveFileFileSize)
                {
                    break;
                }
            }

            fs.Close();

            param.ReceiveFileFinish.Invoke();
        }

        #endregion

        #region Send

        private Thread m_fileSendThread;

        private string m_sendFilePath;
        private int m_sendFilePort;

        private void SendFileFromSocket(
            int socket_wait,
            int port,
            string file_path,
            OnFileSendCompleteHandler call_back
            )
        {
            var file_param = new SendFileParameter(
                socket_wait,
                port,
                file_path,
                call_back);
            ParameterizedThreadStart ts = new ParameterizedThreadStart(SendFileFromSocket_Task);
            m_fileSendThread = new Thread(ts);
            m_fileSendThread.Start(file_param);
        }

        // Parameters
        private class SendFileParameter
        {
            private int m_socketWait;
            private int m_port;
            private string m_fileName;
            private OnFileSendCompleteHandler call_back;

            public SendFileParameter(int m_socketWait, int port, string m_fileName, OnFileSendCompleteHandler call_back)
            {
                this.m_socketWait = m_socketWait;
                this.m_port = port;
                this.m_fileName = m_fileName;
                this.call_back = call_back;
            }

            public int SocketWait { get => m_socketWait; set => m_socketWait = value; }
            public int Port { get => m_port; set => m_port = value; }
            public string FileName { get => m_fileName; set => m_fileName = value; }
            public OnFileSendCompleteHandler Call_back { get => call_back; set => call_back = value; }
        }

        private void SendFileFromSocket_Task(object file_param)
        {
            SendFileParameter param = file_param as SendFileParameter;

            Socket[] sockets = new Socket[param.SocketWait];

            IPAddress local_ip = IPAddress.Parse("127.0.0.1");
            IPEndPoint local_ep = new IPEndPoint(local_ip, param.Port);

            Socket listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listen_socket.Bind(local_ep);
            listen_socket.Listen(100);

            // Accpets all the sockets
            for (int i = 0; i < param.SocketWait; i++)
            {
                sockets[0] = listen_socket.Accept();
            }

            FileStream fs = new FileStream(param.FileName, FileMode.Open);
            byte[] send_data = new byte[4096];
            while (true)
            {
                int read_len = fs.Read(send_data, 0, 4096);
                if (read_len == 0)
                {
                    break;
                }

                for (int i = 0; i < param.SocketWait; i++)
                {
                    sockets[i].Send(send_data, 0, read_len, SocketFlags.None);
                }
            }

            fs.Close();
            for (int i = 0; i < param.SocketWait; i++)
            {
                sockets[i].Close();
            }
            param.Call_back.Invoke();
        }

        #endregion

        #endregion

        #endregion

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

        private void Send_Command_File(string[] args)
        {
            m_sendFilePath = args[0];
            m_sendFilePort = 8002;

            // Calculate file size
            //FileStream fs = new FileStream(args[0], FileMode.Open);
            //fs.Seek(0, SeekOrigin.End);
            //int file_size = 
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

                i_pos = received.IndexOf(',', next_ref_pos + 1);
            }

            return result;
        }

        private void Receive_Command_006(Dictionary<string, string> args)
        {
            string user = args["USER"];
            string file_name = args["NAME"];
            int file_size = int.Parse(args["SIZE"]);
            string addr = args["IP"];

            int i_pos = addr.IndexOf(':');

            IPAddress file_ip = IPAddress.Parse(addr.Substring(0, i_pos));
            int port = int.Parse(addr.Substring(i_pos + 1));
            IPEndPoint file_ep = new IPEndPoint(file_ip, port);

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(file_ep);

            ReceiveFileFromSocket(file_name, socket, file_size, delegate ()
            {
                socket.Close();
                Print(string.Format("接收了来自{0}的文件：{1}，大小为{2}", user, file_name, file_size));
            });
        }

        private void Receive_Command_007(Dictionary<string, string> args)
        {
            int count = int.Parse(args["COUNT"]);
            SendFileFromSocket(count, m_sendFilePort, m_sendFilePath, delegate () {
                Print(string.Format("成功向{0}个用户发送了文件", count));
            });
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
