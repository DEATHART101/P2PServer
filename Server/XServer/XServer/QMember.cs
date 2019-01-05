using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XServer
{
    class QMember
    {
        private string m_name;
        private QChatRoom m_chatRoom;

        public string Name
        {
            get
            {
                return m_name;
            }
        }

        public QChatRoom Room
        {
            get
            {
                return m_chatRoom;
            }

            set
            {
                m_chatRoom = value;
            }
        }

        public QMember(string name, QChatRoom room)
        {
            this.m_name = name;
            this.m_chatRoom = room;
        }
    }
}
