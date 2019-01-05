using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QClient
{
    class QAlreadyLoginException : Exception
    {
    }

    class QLoginException : Exception
    {
    }

    class QLoginErrorException : Exception
    {
    }
}
