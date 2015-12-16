using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SampleClassLibrary
{
    [JsonConverter(typeof(StringEnumConverter))]
    public class Class1
    {
#pragma warning disable 169
        XDocument doc;
#pragma warning restore 169
        public Class1()
        {
            var md = this.GetType().GetMethod("foo");


        }
    }
}
