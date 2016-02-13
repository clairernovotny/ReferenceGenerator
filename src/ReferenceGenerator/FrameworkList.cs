using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ReferenceGenerator
{
    class FrameworkList
    {
        readonly HashSet<Reference> references = new HashSet<Reference>();

        public FrameworkList(IEnumerable<XElement> fileNodes)
        {
            var zero = new Version(0, 0, 0, 0);
            foreach (var node in fileNodes)
            {
                var name = node.Attribute("AssemblyName")
                               .Value;
                var verString = node.Attribute("Version")
                                   ?.Value;
                var version = verString != null ? new Version(verString) : zero;

                references.Add(new Reference(name, version));
            }
        }
    }
}