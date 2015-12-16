using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace SampleCoreLibrary
{
    [JsonConverter(null)]
    public class Class1
    {
#pragma warning disable 169
        XDocument doc;
#pragma warning restore 169
        public Class1()
        {
            var md = this.GetType().GetRuntimeMethod("foo", null);

            var h = WebRequest.CreateHttp("http://foo.bar");
        }
    }
}
