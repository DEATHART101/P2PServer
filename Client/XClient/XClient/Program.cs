using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XClient
{
    class Program
    {
        static void Main(string[] args)
        {
            QClient client = new QClient();
            client.ConnectServer("127.0.0.1", 8000);
        }
    }
}
