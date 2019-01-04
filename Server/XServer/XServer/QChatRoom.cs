using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XServer
{
    class QChatRoom
    {
        private int m_roomId;
        private List<Socket> m_socketMembers;

        public int RoomId
        {
            get
            {
                return m_roomId;
            }
        }

        public List<Socket> Members
        {
            get
            {
                return m_socketMembers;
            }
        }

        public QChatRoom(int room_id)
        {
            m_roomId = room_id;
            m_socketMembers = new List<Socket>();
        }

        public void DistributeData(byte[] data, Socket except)
        {
            foreach (Socket socket in m_socketMembers)
            {
                if (socket.GetHashCode() != except.GetHashCode())
                {
                    socket.Send(data);
                }
            }
        }

        public void AddMember(Socket socket_member)
        {
            m_socketMembers.Add(socket_member);
        }

        public void AddMembers(IEnumerable<Socket> socket_members)
        {
            m_socketMembers.AddRange(socket_members);
        }

        public void RemoveMember(Socket socket_member)
        {
            m_socketMembers.Remove(socket_member);
        }
    }
}
