using System;
using System.Collections.Generic;
using System.Linq;
#if NETSTANDARD1_5
using System.Net.Sockets;
#endif
using System.Threading.Tasks;

namespace SampleCrossCompiledLibrary
{

    public class Class1
    {
#if NETSTANDARD1_5
        TcpClient socket = new TcpClient();
#endif


        public Class1()
        {

        }
    }
}
