using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceGenerator
{
    internal class Reference
    {
        public Reference(string name, Version version)
        {
            Name = name;
            Version = version;
        }

        public string Name
        {
            get;
            private set;
        }

        public Version Version
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Name + ", " + Version;
        }
    }
}
