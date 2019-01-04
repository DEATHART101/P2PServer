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

        private delegate string CommandProccesserHandler(string[] args);

        #endregion

        private int m_port;
        private IPAddress m_address;

        private int m_roomLimit;

        private Socket m_listenSocket;

        private List<Socket> m_socketTourists;
        private Dictionary<Socket, QMember> m_socketMembers;
        private QChatRoom[] m_chatRooms;

        private Socket m_currentSocket;
        private Dictionary<string, CommandProccesserHandler> m_commandProcessers;

        private List<Socket> m_selectCache;

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
            

            Print(string.Format("服务器监听端口被设置为:{0}:{1}\n房间上限为：{2}个房间", listen_ip, port, m_roomLimit));

            // Init commands
            m_commandProcessers = new Dictionary<string, CommandProccesserHandler>();
            m_commandProcessers.Add("CONN", Command_CONN);
            m_commandProcessers.Add("LOGIN", Command_LOGIN);
            m_commandProcessers.Add("QUIT", Command_QUIT);
            m_commandProcessers.Add("ROOMS", Command_ROOMS);
            m_commandProcessers.Add("CREATEROOM", Command_CREATEROOM);
            m_commandProcessers.Add("ENTER", Command_ENTER);
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
                if (m_selectCache.Count != m_socketTourists.Count + m_socketMembers.Count + 1)
                {
                    // Remake
                    m_selectCache = new List<Socket>(m_socketTourists);
                    m_selectCache.AddRange(m_socketMembers.Keys);
                    m_selectCache.Add(m_listenSocket);
                } 

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
                        int recv_len = socket.Receive(recv_data);
                        recv_data[recv_len] = 0;
                        string recv_string = Encoding.UTF8.GetString(recv_data, 0, recv_len);

                        if (recv_len == 0)
                        {
                            // On client disconnected
                            if (!m_socketTourists.Remove(socket))
                            {
                                m_socketMembers.Remove(socket);
                            }
                        }
                        else
                        {
                            // Recieved something
                            var socket_ep = socket.LocalEndPoint;
                            Print(string.Format("从{0}接受了{1}字节，内容为\"{2}\"", ((IPEndPoint)socket_ep).Address.ToString(), recv_len, recv_string));

                            // Check to see if it is in a room
                            if (m_socketMembers.ContainsKey(socket))
                            {
                                QMember member = m_socketMembers[socket];
                                QChatRoom room = member.Room;
                                if (room != null)
                                {
                                    // It is a room member
                                    var dist_string = string.Format("{0}:{1}", member.Name, recv_string);
                                    room.DistributeData(Encoding.UTF8.GetBytes(dist_string), socket);
                                    break;
                                }
                            }

                            // Set context
                            m_currentSocket = socket;
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
                        string ret_message = proccesser.Invoke(SubArray<string>(splits, 1));
                        byte[] send_data = Encoding.UTF8.GetBytes(ret_message);
                        socket.Send(send_data);
                    }
                    catch (QInvalidRoomExeption)
                    {
                        socket.Send(Encoding.UTF8.GetBytes("101 No such room."));
                    }
                    catch (QNoMoreRoomExeption)
                    {
                        socket.Send(Encoding.UTF8.GetBytes("102 No moor room, please try again later."));
                    }
                    catch (QNotLoginExeption)
                    {
                        socket.Send(Encoding.UTF8.GetBytes("103 Please use LOGIN to login first."));
                    }
                }
                else
                {
                    // Not implement
                    Print(string.Format("未识别的指令:\"{0}\"", command));
                    socket.Send(Encoding.UTF8.GetBytes("100 Unrecognized command"));

                }
            }
            catch (Exception e)
            {

            }
        }

        #region Commands

        private string Command_CONN(string[] args)
        {
            return "000 Hello Client!";
        }

        private string Command_LOGIN(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException();
            }

            m_socketTourists.Remove(m_currentSocket);
            QMember member = new QMember(args[0], null);
            m_socketMembers.Add(m_currentSocket, member);

            return "001 Login successful.";
        }

        private string Command_QUIT(string[] args)
        {
            CheckLogin();

            m_socketMembers.Remove(m_currentSocket);
            m_socketTourists.Add(m_currentSocket);

            return "002 Bye.";
        }

        private string Command_ROOMS(string[] args)
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

        private string Command_CREATEROOM(string[] args)
        {
            CheckLogin();

            int new_id = GetNewRoomID();
            if (new_id == -1)
            {
                throw new QNoMoreRoomExeption();
            }

            QChatRoom new_room = new QChatRoom(new_id);
            m_chatRooms[new_id] = new_room;
            TransferMemeberToRoom(m_currentSocket, new_room);

            return string.Format("004 Room create successful!Room ID:{0}.", new_id);
        }

        private string Command_ENTER(string[] args)
        {
            int room_id = int.Parse(args[0]);
            if (m_chatRooms[room_id] == null)
            {
                throw new QInvalidRoomExeption();
            }

            TransferMemeberToRoom(m_currentSocket, m_chatRooms[room_id]);
            return string.Format("004 Enter room successful!Room ID:{0}", room_id);
        }

        #endregion

        #region Validations

        private void CheckLogin()
        {
            if (!m_socketMembers.ContainsKey(m_currentSocket))
            {
                throw new QNotLoginExeption();
            }
        }

        #endregion

        #region Operations

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
