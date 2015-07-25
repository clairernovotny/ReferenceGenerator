using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace SampleCoreLibrary
{
    public class Class1
    {
        XDocument doc;
        public Class1()
        {
            var md = this.GetType().GetRuntimeMethod("foo", null);

            var h = WebRequest.CreateHttp("http://foo.bar");
        }
    }
}
