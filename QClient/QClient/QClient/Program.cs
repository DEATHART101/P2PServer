using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QClient
{
    class Program
    {
        static void Main(string[] args)
        {
            QClient main = new QClient("166.111.140.14", 8000, 8001);
            main.Run();
        }
    }
}
