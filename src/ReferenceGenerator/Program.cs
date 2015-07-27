using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ReferenceGenerator
{
    class Program
    {
        static HashSet<string> MicrosoftRefs;
        static void Main(string[] args)
        {
            // args 0: nuspec file
            // args 1: project file (csproj/vbproj, etc). Used to look for packages.config/project.json and references
            // args 2: TFM's to generate, semi-colon joined. E.g.: dotnet;uap10.0
            // args 3: target files, semi-colon joined

            string path = args[0];
            string projectFile = args[1];
            string[] tfms = args[2].Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            string[] files = args[3].Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();


            var microsoftRefs = new[] { "Microsoft.CSharp", "Microsoft.VisualBasic", "Microsoft.Win32.Primitives" };
            MicrosoftRefs = new HashSet<string>(microsoftRefs, StringComparer.OrdinalIgnoreCase);

            // hashset for storage
            var references = new HashSet<Reference>();

            foreach (var file in files)
            {
                try
                {
                    var assm = AssemblyInfo.GetAssemblyInfo(file);
                    foreach (var r in FilterReferences(assm.References))
                    {
                        // Only include System.* or Microsoft.CSharp, Microsoft.VisualBasic or Microsoft.Win32.Primitives
                        // Others may not have assm name + ver matching nuget package name + ver
                        references.Add(r);
                    }

                }
                catch (InvalidOperationException)
                {
                    // Log error
                }
            }


            // Now squash all but most recent
            var groups = references.GroupBy(k => k.Name, StringComparer.OrdinalIgnoreCase)
                                   .Select(g =>
                                            g.OrderByDescending(r => r.Version)
                                             .First()
                                           )
                                   .OrderBy(r => r.Name)
                                   .ToList();

            var refNames = new HashSet<string>(groups.Select(g => g.Name), StringComparer.OrdinalIgnoreCase);

            XmlNamespaceManager nsm;
            // Read nuspec as xml
            using (var reader = XmlReader.Create(path))
            {
                nsm = new XmlNamespaceManager(reader.NameTable);                
            }

            var xdoc = XDocument.Load(path);

            // get the default namespace
            var name = xdoc.Root.Attribute("xmlns")?.Value ?? string.Empty;
            nsm.AddNamespace("ns", name);

            ns = name;


            var deps = GetOrCreateDependenciesNode(xdoc);

            foreach (var tfm in tfms)
            {
                var ele = CreateDependencyElement(tfm, groups);

                // see if we have a node with this tfm
                var grp = deps.XPathSelectElement($"./ns:group[@targetFramework='{tfm}']", nsm);
                if (grp != null)
                {
                    // Need to merge
                    // find nodes that match by name, remove and then readd them
                    var existing = grp.Elements(ns + "dependency")?.Where(e => refNames.Contains(e.Attribute("id").Value))?.ToList() ?? new List<XElement>();

                    foreach (var xe in existing)
                    {
                        xe.Remove();
                    }

                    // Add the new ones back in 
                    grp.Add(ele.Elements());
                }
                else
                {
                    deps.Add(ele);
                }
                

            }



            xdoc.Save(path, SaveOptions.OmitDuplicateNamespaces); // TODO: handle read-only files and return error

        }

        private static IEnumerable<Reference> FilterReferences(IEnumerable<Reference> references)
        {
            foreach(var r in references)
            {
                if (MicrosoftRefs.Contains(r.Name) || r.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                    yield return r;
            }
        }

        static XNamespace ns = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";

        private static XElement GetOrCreateDependenciesNode(XDocument doc)
        {
            var mde = doc.Root.Element(ns + "metadata");

            var deps = mde.Element(ns + "dependencies");

            if (deps == null)
            {
                deps = new XElement(ns + "dependencies");
                mde.Add(deps);
            }

            return deps;
        }

        private static XElement CreateDependencyElement(string tfm, IEnumerable<Reference> refs)
        {
            var ele = new XElement(ns + "group", new XAttribute("targetFramework", tfm),
                refs.Select(r =>
                    new XElement(ns + "dependency",
                        new XAttribute("id", r.Name),
                        new XAttribute("version", r.Version.ToString(3))
                                )));

            return ele;
        }

        private static void CheckRefs(AssemblyInfo assm)
        {
            Console.WriteLine("<group targetFramework=\"dotnet\">");
            foreach (var refa in assm.References)
                Console.WriteLine("  <dependency id=\"{0}\" version=\"{1}\" />", refa.Name, refa.Version.ToString(3));
            Console.WriteLine("</group>");
        }
    }
}
