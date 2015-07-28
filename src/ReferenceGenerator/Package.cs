using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceGenerator
{
    class Package : IEquatable<Package>, IComparable<Package>
    {
        public Package(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id
        {
            get;
            private set;
        }

        // This needs a SemVer comparer
        public string Version
        {
            get;
            private set;
        }

        public int CompareTo(Package other)
        {
            // sort on name for now
            return StringComparer.OrdinalIgnoreCase.Compare(Id, other?.Id);
        }

        public bool Equals(Package other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) &&
                   Equals(Version, other.Version);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Package);
        }

        public override int GetHashCode()
        {
            return unchecked(Id?.GetHashCode() ?? 1 ^ Version?.GetHashCode() ?? 1);
        }

        public override string ToString()
        {
            return Id + ", " + Version;
        }
    }
}
