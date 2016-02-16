using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Frameworks;

namespace ReferenceGenerator.Engine
{
    public static class FrameworkListCollection
    {
        const string ResourceRoot = "ReferenceGenerator.Engine.Res.";
        static readonly Assembly ThisAssembly = typeof(FrameworkListCollection).Assembly;
        static readonly string[] ThisAssemblyResources = ThisAssembly.GetManifestResourceNames();
        static readonly HashSet<NuGetFramework> ThisAssemblyFrameworks = new HashSet<NuGetFramework>(ThisAssemblyResources.Select(FromResourceString));
        static readonly ConcurrentDictionary<NuGetFramework, FrameworkList> FrameworkLists = new ConcurrentDictionary<NuGetFramework, FrameworkList>();

        public static bool Contains(NuGetFramework framework)
        {
            // if framework ver is 0, return any
            if (framework.Version.Major == 0 && framework.Version.Minor == 0)
            {
                return ThisAssemblyFrameworks.Any(fx => fx.Framework == framework.Framework);
            }

            // Otherwise look for an exact match
            return ThisAssemblyFrameworks.Contains(framework);
        }

        static NuGetFramework FromResourceString(string resource)
        {
            var fxStr = resource.Substring(ResourceRoot.Length);
            fxStr = fxStr.Substring(0, fxStr.LastIndexOf('.', fxStr.Length - 5));

            fxStr = fxStr.Replace("_Version_", ",Version=")
                         .Replace("._", ".");

            return NuGetFramework.Parse(fxStr);

        }

        static string ResourceNameFromNuGetFramework(NuGetFramework framework)
        {
            var resourceSafeString = $"{framework.Framework}_Version_v{framework.Version.GetDisplayVersion().Replace(".", "._")}";
            
            return resourceSafeString;
        }

        public static FrameworkList GetFrameworkList(NuGetFramework framework)
        {

            // if framework ver is 0, find matching one
            if (framework.Version.Major == 0 && framework.Version.Minor == 0)
            {
                framework = ThisAssemblyFrameworks.First(fx => fx.Framework == framework.Framework);
            }

            return FrameworkLists.GetOrAdd(framework, CreateFrameworkList);
        }

        static FrameworkList CreateFrameworkList(NuGetFramework framework)
        {
            if (!Contains(framework))
                throw new ArgumentOutOfRangeException(nameof(framework), $"Framework {framework} does not have an embedded file list");

            var resName = $"{ResourceRoot}{ResourceNameFromNuGetFramework(framework)}";

            var nodes = new List<XElement>();
            using (var sr = new StreamReader(ThisAssembly.GetManifestResourceStream($"{resName}.FrameworkList.xml")))
            {
                var doc = XDocument.Load(sr);
                nodes.AddRange(doc.Descendants("File"));
            }

            // See if there's a suppliment
            if (ThisAssemblyResources.Contains($"{resName}.FrameworkList_Supplement.xml"))
            {
                using (var sr = new StreamReader(ThisAssembly.GetManifestResourceStream($"{resName}.FrameworkList_Supplement.xml")))
                {
                    var doc = XDocument.Load(sr);
                    nodes.AddRange(doc.Descendants("File"));
                }
            }

            return new FrameworkList(nodes);
        }
    }
}
