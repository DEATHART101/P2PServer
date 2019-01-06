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

    class QInvalidReceiveException : Exception
    {

    }

    class QInvalidArumentException : Exception
    {

    }

    class QNotlogedinException : Exception
    {

    }

    class QFrientRefuseException : Exception
    {

    }

    class QLoginErrorException : Exception
    {
    }

    class QLogoutErrorException : Exception
    {
    }

    class QFriendNotOnlineException : Exception
    {
    }
}
