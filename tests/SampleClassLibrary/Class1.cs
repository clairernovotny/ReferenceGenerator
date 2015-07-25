using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace SampleClassLibrary
{
    public class Class1
    {
        XDocument doc;
        public Class1()
        {
            var md = this.GetType().GetMethod("foo");
        }
    }
}
