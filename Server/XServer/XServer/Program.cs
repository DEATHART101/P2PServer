using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XServer
{
    class Program
    {
        static void Main(string[] args)
        {
            QServer main_server = new QServer(8000, "127.0.0.1");
            main_server.Fire();
        }
    }
}
