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

        private delegate string CommandProccesserHandler();

        #endregion

        private int m_port;
        private IPAddress m_address;

        private int m_roomLimit;

        private Socket m_listenSocket;

        private List<Socket> m_socketTourists;
        private Dictionary<Socket, QMember> m_socketMembers;
        private QChatRoom[] m_chatRooms;

        private Socket m_currentSocket;
        private string[] m_args;
        private QMember m_currentMember;
        private Dictionary<string, CommandProccesserHandler> m_commandProcessers;

        private List<Socket> m_selectCache;

        // Use UTF-8 to support all language
        private Encoding m_encoding;

        #region Properties

        public int RoomCount
        {
            get
            {
                int result = 0;
                for (int i = 0; i < m_roomLimit; i++)
                {
                    if (m_chatRooms[i] != null)
                    {
                        result++;
                    }
                }

                return result;
            }
        }

        #endregion

        public QServer(int port, string listen_ip, int room_limit = 100)
        {
            m_port = port;
            m_address = IPAddress.Parse(listen_ip);
            m_socketTourists = new List<Socket>();
            m_socketMembers = new Dictionary<Socket, QMember>();
            m_roomLimit = room_limit;
            m_chatRooms = new QChatRoom[m_roomLimit];
            m_selectCache = new List<Socket>();
            m_encoding = Encoding.UTF8;

            Print(string.Format("服务器监听端口被设置为:{0}:{1}\n房间上限为：{2}个房间", listen_ip, port, m_roomLimit));

            // Init commands
            m_commandProcessers = new Dictionary<string, CommandProccesserHandler>();
            m_commandProcessers.Add("CONN", Command_CONN);
            m_commandProcessers.Add("LOGIN", Command_LOGIN);
            m_commandProcessers.Add("QUIT", Command_QUIT);
            m_commandProcessers.Add("ROOMS", Command_ROOMS);
            m_commandProcessers.Add("CREATEROOM", Command_CREATEROOM);
            m_commandProcessers.Add("ENTER", Command_ENTER);
            m_commandProcessers.Add("LEAVE", Command_LEAVE);
            m_commandProcessers.Add("TALK", Command_TALK);
            m_commandProcessers.Add("FILE", Command_FILE);
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
                m_selectCache.Clear();
                m_selectCache.AddRange(m_socketTourists);
                m_selectCache.AddRange(m_socketMembers.Keys);
                m_selectCache.Add(m_listenSocket);

                Socket.Select(m_selectCache, null, null, 1000);

                foreach (Socket socket in m_selectCache)
                {
                    if (socket.GetHashCode() == m_listenSocket.GetHashCode())
                    {
                        Socket new_socket = m_listenSocket.Accept();
                        m_socketTourists.Add(new_socket);
                    }
                    else
                    {
                        byte[] recv_data = new byte[1024];

                        try
                        {
                            int recv_len = socket.Receive(recv_data);
                            string recv_string = GetString(recv_data, recv_len);

                            if (recv_len == 0)
                            {
                                // On client disconnected
                                SocketClose(socket);
                            }
                            else
                            {
                                // Recieved something
                                var socket_ep = socket.LocalEndPoint;
                                Print(string.Format("从{0}接受了{1}字节，内容为\"{2}\"", ((IPEndPoint)socket_ep).Address.ToString(), recv_len, recv_string));

                                Proccess(socket, recv_string);
                            }
                        }
                        catch (SocketException)
                        {
                            SocketAbort(socket);
                        }
                    }
                }
            }
        }

        private void Proccess(Socket socket, string socket_input)
        {
            try
            {
                string[] args;
                string command;
                CommandSplit(socket_input, out command, out args);

                // Set context
                m_currentSocket = socket;
                m_currentMember = null;
                m_args = args;

                if (m_commandProcessers.ContainsKey(command))
                {
                    var proccesser = m_commandProcessers[command];

                    try
                    {
                        string ret_message = proccesser.Invoke();
                        byte[] send_data = GetBytes(ret_message);
                        socket.Send(send_data);
                    }
                    catch (QInvalidRoomExeption)
                    {
                        socket.Send(GetBytes("101 No such room."));
                    }
                    catch (QNoMoreRoomExeption)
                    {
                        socket.Send(GetBytes("102 No moor room, please try again later."));
                    }
                    catch (QNotLoginExeption)
                    {
                        socket.Send(GetBytes("103 Please use LOGIN to login first."));
                    }
                    catch (ArgumentException)
                    {
                        socket.Send(GetBytes("104 Arguments not satisfied."));
                    }
                    catch (QNotInRoomException)
                    {
                        socket.Send(GetBytes("105 You have to enter a room first."));
                    }
                }
                else
                {
                    // Not implement
                    Print(string.Format("未识别的指令:\"{0}\"", command));
                    socket.Send(GetBytes("100 Unrecognized command"));

                }
            }
            catch (Exception e)
            {
                Print(e.Message);
            }
        }

        #region Commands

        private string Command_CONN()
        {
            return "000 Hello Client!";
        }

        private string Command_LOGIN()
        {
            CheckArgumentCount(1);

            m_socketTourists.Remove(m_currentSocket);
            QMember member = new QMember(m_args[0], null);
            m_socketMembers.Add(m_currentSocket, member);

            return "001 Login successful.";
        }

        private string Command_QUIT()
        {
            CheckLogin();

            m_socketMembers.Remove(m_currentSocket);
            m_socketTourists.Add(m_currentSocket);

            return "002 Bye.";
        }

        private string Command_ROOMS()
        {
            string result;

            int room_count = RoomCount;
            if (room_count == 0)
            {
                result = "003 There is currently no room in use.";
            }
            else
            {
                result = string.Format("003 There are total of {0} rooms\n", room_count);
                foreach (var room in m_chatRooms)
                {
                    if (room == null)
                    {
                        continue;
                    }

                    List<Socket> socket_members = room.Members;
                    string room_info = string.Format("Room Id:{0}, {1} People in the room\nRoom members:", room.RoomId, socket_members.Count);
                    foreach (Socket socket_member in socket_members)
                    {
                        QMember member = m_socketMembers[socket_member];

                        if (socket_member.GetHashCode() == socket_members.Last().GetHashCode())
                        {
                            room_info += string.Format("{0}.", member.Name);
                        }
                        else
                        {
                            room_info += string.Format("{0},", member.Name);
                        }
                    }
                    room_info += "\n";

                    result += room_info;
                }
            }

            return result;
        }

        private string Command_CREATEROOM()
        {
            CheckLogin();

            if (m_currentMember.Room != null)
            {
                return string.Format("102 You have to leave this room first.");
            }

            int new_id = GetNewRoomID();
            if (new_id == -1)
            {
                throw new QNoMoreRoomExeption();
            }

            QChatRoom new_room = new QChatRoom(new_id);
            m_chatRooms[new_id] = new_room;
            TransferMemeberToRoom(m_currentSocket, new_room);

            return string.Format("004 Room create successful. Room ID:{0}.", new_id);
        }

        private string Command_ENTER()
        {
            CheckLogin();
            CheckArgumentCount(1);

            int room_id = int.Parse(m_args[0]);
            if (m_chatRooms[room_id] == null)
            {
                throw new QInvalidRoomExeption();
            }

            if (m_currentMember.Room != null)
            {
                TransferMemberOutofRoom(m_currentSocket);
            }

            TransferMemeberToRoom(m_currentSocket, m_chatRooms[room_id]);
            return string.Format("004 Enter room successful. Room ID:{0}", room_id);
        }

        private string Command_LEAVE()
        {
            CheckLogin();
            CheckRoom();

            TransferMemberOutofRoom(m_currentSocket);

            return "006 Left room.";
        }

        private string Command_TALK()
        {
            CheckLogin();
            CheckRoom();

            var dist_string = string.Format("005 TALK FROM {0}:\"{1}\"", m_currentMember.Name, m_args[0].Substring(1, m_args[0].Length - 2));
            m_currentMember.Room.DistributeData(GetBytes(dist_string), m_currentSocket);

            return "005 Talk successful.";
        }

        private string Command_FILE()
        {
            CheckLogin();
            CheckRoom();
            CheckArgumentCount(3, true);

            string file_name = m_args[0];
            int file_size = int.Parse(m_args[1]);
            int socket_port = int.Parse(m_args[2]);

            IPEndPoint file_ep = (IPEndPoint)m_currentSocket.LocalEndPoint;
            string dist_string = string.Format(
                "006 FILE FROM:USER \"{0}\",NAME \"{3}\",SIZE \"{4}\",IP \"{1}:{2}\"",
                m_currentMember.Name,
                file_ep.Address,
                socket_port,
                file_name,
                file_size
                );

            m_currentMember.Room.DistributeData(GetBytes(dist_string), m_currentSocket);

            return string.Format("007 FILE TO:COUNT \"{0}\"", m_currentMember.Room.Members.Count - 1);
        }

        #endregion

        #region Validations

        private void CheckArgumentCount(int length, bool strict = false)
        {
            if (strict)
            {
                if (m_args.Length != length)
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                if (m_args.Length < length)
                {
                    throw new ArgumentException();
                }
            }
        }

        private void CheckLogin()
        {
            if (!m_socketMembers.ContainsKey(m_currentSocket))
            {
                throw new QNotLoginExeption();
            }

            m_currentMember = m_socketMembers[m_currentSocket];
        }

        private void CheckRoom()
        {
            if (m_currentMember.Room == null)
            {
                throw new QNotInRoomException();
            }
        }

        #endregion

        #region Operations

        #region Socket Operations

        private byte[] GetBytes(string str)
        {
            return m_encoding.GetBytes(str);
        }

        private string GetString(byte[] bytes, int length)
        {
            return m_encoding.GetString(bytes, 0, length);
        }

        private void SocketAbort(Socket socket)
        {
            SocketClose(socket);
        }

        private void SocketClose(Socket socket)
        {
            if (!m_socketTourists.Remove(socket))
            {
                QMember member = m_socketMembers[socket];
                if (member.Room != null)
                {
                    TransferMemberOutofRoom(socket);
                }

                m_socketMembers.Remove(socket);
            }
        }

        private void CommandSplit(string socket_input, out string command, out string[] args)
        {
            int length = socket_input.Length;
            List<int> split_pos = new List<int>();

            bool in_ref = false;
            for (int i = 0; i < length; i++)
            {
                if (socket_input[i] == ' ')
                {
                    if (!in_ref)
                    {
                        split_pos.Add(i);
                    }
                }
                else if (socket_input[i] == '\"')
                {
                    in_ref = !in_ref;
                }
            }

            if (split_pos.Count == 0)
            {
                command = socket_input;
                args = null;
            }
            else
            {
                int last_pos = split_pos[0];
                command = socket_input.Substring(0, last_pos);

                args = new string[split_pos.Count];
                length = split_pos.Count - 1;
                for (int i = 0; i < length; i++)
                {
                    if (socket_input[last_pos + 1] == '\"')
                    {
                        args[i] = socket_input.Substring(last_pos + 2, split_pos[i + 1] - last_pos - 3);
                    }
                    else
                    {
                        args[i] = socket_input.Substring(last_pos + 1, split_pos[i + 1] - last_pos - 1);
                    }
                    last_pos = split_pos[i + 1];
                }

                last_pos = split_pos.Last();
                if (socket_input[last_pos + 1] == '\"')
                {
                    args[length] = socket_input.Substring(last_pos + 2, socket_input.Length - last_pos - 3);
                }
                else
                {
                    args[length] = socket_input.Substring(last_pos + 1, socket_input.Length - last_pos - 1);
                }
            }
        }

        #endregion

        #region Room Operations

        private int GetNewRoomID()
        {
            for (int i = 0; i < m_roomLimit; i++)
            {
                if (m_chatRooms[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private void TransferMemeberToRoom(Socket socket_member, QChatRoom room)
        {
            QMember member = m_socketMembers[socket_member];
            member.Room = room;
            room.AddMember(socket_member);
        }

        private void TransferMemberOutofRoom(Socket socket_member)
        {
            QMember member = m_socketMembers[socket_member];
            QChatRoom room = member.Room;
            room.RemoveMember(socket_member);
            member.Room = null;

            if (room.Members.Count == 0)
            {
                m_chatRooms[room.RoomId] = null;
            }
        }

        #endregion

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
