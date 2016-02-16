using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ReferenceGenerator.Engine
{
    public class FrameworkList
    {
        readonly HashSet<Reference> references = new HashSet<Reference>(Comparer);
        static readonly ReferenceEqualityComparer Comparer = new ReferenceEqualityComparer();
        static readonly Version Zero = new Version(0, 0, 0, 0);

        public FrameworkList(IEnumerable<XElement> fileNodes)
        {
            foreach (var node in fileNodes)
            {
                var name = node.Attribute("AssemblyName")
                               .Value;
                var verString = node.Attribute("Version")
                                   ?.Value;
                var version = verString != null ? new Version(verString) : Zero;

                references.Add(new Reference(name, version));
            }
        }

        public bool ContainsReference(Reference reference)
        {
            if (ReferenceEquals(reference, null))
                return false;

            if (references.Contains(reference))
            {
                // see if the framework equal to or higher in version
                var fxRef = references.First(r => Comparer.Equals(r, reference));

                // If rx ref is zero, match all, otherwise check ver
                if(fxRef.Version.Equals(Zero) || fxRef.Version >= reference.Version)
                    return true; 
            }

            return false;
        }


        class ReferenceEqualityComparer : IEqualityComparer<Reference>
        {
            public bool Equals(Reference x, Reference y)
            {
                // Will match only on id if one of the versions is zero
                return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Reference obj)
            {
                return obj.Name.GetHashCode();
            }
        }

    }
}