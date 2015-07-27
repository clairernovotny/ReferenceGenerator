using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            string nuspecFile = args[0];
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
                    foreach (var r in assm.References)
                    {
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

            
            // make sure there is no mscorlib
            if (groups.Any(g => string.Equals(g.Name, "mscorlib", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("mscorlib-based projects are not supported");

            IEnumerable<Package> packages;

            var projDir = Path.GetDirectoryName(projectFile);
            if(File.Exists(Path.Combine(projDir, "project.json")))
            {
                // Project.json
                packages = GetProjectJsonPackages(projectFile, groups);
            }
            else if(File.Exists(Path.Combine(projDir, $"packages.{Path.GetFileNameWithoutExtension(projectFile)}.config")))
            {
                packages = GetPackagesConfigPackages(projectFile, $"packages.{Path.GetFileNameWithoutExtension(projectFile)}.config", groups);
            }
            else if(File.Exists(Path.Combine(projDir, "packages.config")))
            {
                packages = GetPackagesConfigPackages(projectFile, $"packages.config", groups);
            }
            else
            {
                // Must be an "old" PCL without any refs. Best we can do is read the refs
                packages = GetPackagesFromAssemblyRefs(groups);

            }


            UpdateNuspecFile(nuspecFile, packages, tfms);
        }

        static void UpdateNuspecFile(string nuspecFile, IEnumerable<Package> packages, IEnumerable<string> tfms)
        {

            var refNames = new HashSet<string>(packages.Select(g => g.Id), StringComparer.OrdinalIgnoreCase);


            XmlNamespaceManager nsm;
            // Read nuspec as xml
            using (var reader = XmlReader.Create(nuspecFile))
            {
                nsm = new XmlNamespaceManager(reader.NameTable);
            }

            var xdoc = XDocument.Load(nuspecFile);

            // get the default namespace
            var name = xdoc.Root.Attribute("xmlns")?.Value ?? string.Empty;
            nsm.AddNamespace("ns", name);

            XNamespace nuspecNs = name;


            var deps = GetOrCreateDependenciesNode(xdoc, nuspecNs);

            foreach (var tfm in tfms)
            {
                var ele = CreateDependencyElement(tfm, packages, nuspecNs);

                // see if we have a node with this tfm
                var grp = deps.XPathSelectElement($"./ns:group[@targetFramework='{tfm}']", nsm);
                if (grp != null)
                {
                    // Need to merge
                    // find nodes that match by name, remove and then readd them
                    var existing = grp.Elements(nuspecNs + "dependency")?.Where(e => refNames.Contains(e.Attribute("id").Value))?.ToList() ?? new List<XElement>();

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



            xdoc.Save(nuspecFile, SaveOptions.OmitDuplicateNamespaces); // TODO: handle read-only files and return error
        }

        static IEnumerable<Package> GetProjectJsonPackages(string projectFile, IEnumerable<Reference> refs)
        {
            // This needs to load the project.lock.json, look for the reference under the 
            // targets -> ".NETPlatform,Version=v5.0", look for each package and the files in it, then pull out based on refs

            // Make sure the lock file is present. It should be if we're called after build
            var lockFile = Path.Combine(Path.GetDirectoryName(projectFile), "project.lock.json");
            if (!File.Exists(lockFile))
                throw new InvalidOperationException("project.lock.json is missing");

            JObject projectJson;
            using (var reader = new JsonTextReader(File.OpenText(lockFile)))
            {
                projectJson = JObject.Load(reader);
            }

            var netPlatform = (JObject)projectJson["targets"][".NETPlatform,Version=v5.0"];

            // build a lookup of filenames to packages
            var query = (from package in netPlatform.Properties()
                        let packageNameAndVer = package.Name.Split('/')
                        let packageObj = (JObject)package.Value
                        let compileProp = packageObj.Properties().FirstOrDefault(p => p.Name == "compile")
                        where compileProp != null
                        let compileObj = (JObject)compileProp.Value
                        from libProp in compileObj.Properties()
                        select new
                        {
                            Assembly = libProp.Name.Split('/').Last(),
                            Id = packageNameAndVer[0],
                            Version = packageNameAndVer[1]
                        })
                        .ToLookup(k =>  k.Assembly.Substring(0, k.Assembly.LastIndexOf('.')), e => new Package(e.Id, e.Version), StringComparer.OrdinalIgnoreCase);


            // Query is now a lookup of assemblies to packages. It's possible an assembly is in more than one package but we'll pick the first one always

            var assmToPackage = query.ToDictionary(q => q.Key, v => v.First());

            var results = (from assmRef in refs
                          join kvp in assmToPackage on assmRef.Name equals kvp.Key
                          select kvp.Value).ToList();

            return results;
        }

        static IEnumerable<Package> GetPackagesConfigPackages(string projectFile, string packagesConfig, IEnumerable<Reference> refs)
        {
            // TODO: impl
            return GetPackagesFromAssemblyRefs(refs);
        }

        static IEnumerable<Package> GetPackagesFromAssemblyRefs(IEnumerable<Reference> refs)
        {
            // These should only be system ones
            return refs.Select(r => new Package(r.Name, r.Version.ToString(3)));
        }

        static IEnumerable<Reference> FilterReferences(IEnumerable<Reference> references)
        {
            foreach (var r in references)
            {
                if (MicrosoftRefs.Contains(r.Name) || r.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                    yield return r;
            }
        }


        static XElement GetOrCreateDependenciesNode(XDocument doc, XNamespace nuspecNs)
        {
            var mde = doc.Root.Element(nuspecNs + "metadata");

            var deps = mde.Element(nuspecNs + "dependencies");

            if (deps == null)
            {
                deps = new XElement(nuspecNs + "dependencies");
                mde.Add(deps);
            }

            return deps;
        }

        static XElement CreateDependencyElement(string tfm, IEnumerable<Package> refs, XNamespace nuspecNs)
        {
            var ele = new XElement(nuspecNs + "group", new XAttribute("targetFramework", tfm),
                refs.Select(r =>
                    new XElement(nuspecNs + "dependency",
                        new XAttribute("id", r.Id),
                        new XAttribute("version", r.Version)
                                )));

            return ele;
        }

    }
}
