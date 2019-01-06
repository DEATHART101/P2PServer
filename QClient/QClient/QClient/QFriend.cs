using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;

namespace QClient
{
    class QFriend
    {
        private string m_name;
        private string m_address;

        public QFriend(string m_name, string m_address)
        {
            this.m_name = m_name;
            this.m_address = m_address;
        }

        public string Name { get => m_name; }
        public string Address { get => m_address; }
    }
}
