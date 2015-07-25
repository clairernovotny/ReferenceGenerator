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
        static void Main(string[] args)
        {
            // args 0: nuspec file
            // args 1: TFM's to generate, semi-colon joined. E.g.: dotnet;uap10.0
            // args 2: target files, semi-colon joined

            string path = args[0];
            string[] tfms = args[1].Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray(); 
            string[] files = args[2].Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();


            // hashset for storage
            var references = new HashSet<Reference>();

            foreach(var file in files)
            {
                try
                {
                    var assm = AssemblyInfo.GetAssemblyInfo(file);
                    foreach (var r in assm.References)
                        references.Add(r);

                }
                catch(InvalidOperationException)
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

            XmlNamespaceManager nsm;
            // Read nuspec as xml
            using (var reader = XmlReader.Create(path))
            {
                nsm = new XmlNamespaceManager(reader.NameTable);
                nsm.AddNamespace("ns", ns.NamespaceName);
            }

            var xdoc = XDocument.Load(path);
            var deps = GetOrCreateDependenciesNode(xdoc);

                foreach (var tfm in tfms)
                {
                    var ele = CreateDependencyElement(tfm, groups);

                    // see if we have a node with this tfm
                    var grp = deps.XPathSelectElement($"./ns:group[@targetFramework='dotnet']", nsm);
                    if (grp != null)
                    {
                        grp.Remove();
                    }
                    deps.Add(ele);
                    
                }
            


            xdoc.Save(path, SaveOptions.OmitDuplicateNamespaces); // TODO: handle read-only files and return error

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
