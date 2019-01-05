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

            //string address;
            //int port;

            //string ip = Console.ReadLine();
            //int pos = ip.IndexOf(':');
            //if (pos == -1)
            //{
            //    address = ip;
            //    port = 8000;
            //}
            //else
            //{
            //    address = 
            //}

            client.ConnectServer("127.0.0.1", 8000);
        }
    }
}
