using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using FluentAssertions;

namespace SampleProjJsonPcl
{
    public class Class1
    {
        private HttpWebRequest req = WebRequest.CreateHttp("foo");

        private void DoSomething()
        {
            5.Should().Be(5);
        }
    }
}
