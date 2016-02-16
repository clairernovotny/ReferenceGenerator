using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceGenerator
{
    public static class ExtensionMethods
    {
        public static string GetDisplayVersion(this Version version)
        {
            var stringBuilder = new StringBuilder(string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));
            if (version.Build > 0 || version.Revision > 0)
            {
                stringBuilder.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);
                if (version.Revision > 0)
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
            }
            return stringBuilder.ToString();
        }

    }
}
