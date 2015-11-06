using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        private static int Main(string[] args)
        {
            // args 0: NuGetTargetMonikers -- .NETPlatform,Version=v5.0  
            // args 1: TFM's to generate, semi-colon joined. E.g.: dotnet;uap10.0 
            // args 2: nuspec file
            // args 3: project file (csproj/vbproj, etc). Used to look for packages.config/project.json and references. should match order of target files
            // args 4: target files, semi-colon joined

            try
            {
                var nugetTargetMonikers = args[0].Split(';')
                                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                                 .ToArray();
                var tfms = args[1].Split(';')
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .ToArray();
                var nuspecFile = args[2];
                var projectFiles = args[3].Split(';')
                                          .Where(s => !string.IsNullOrWhiteSpace(s))
                                          .ToArray();
                var files = args[4].Split(';')
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .ToArray();

                var microsoftRefs = new[] {"Microsoft.CSharp", "Microsoft.VisualBasic", "Microsoft.Win32.Primitives"};
                MicrosoftRefs = new HashSet<string>(microsoftRefs, StringComparer.OrdinalIgnoreCase);


                var packages = new List<Package>();

                for (var i = 0; i < projectFiles.Length; i++)
                {

                    var assm = AssemblyInfo.GetAssemblyInfo(files[i]);
                    var projectFileName = Path.GetFileNameWithoutExtension(projectFiles[i]);

                    var projDir = Path.GetDirectoryName(projectFiles[i]);
                    if (File.Exists(Path.Combine(projDir, $"{projectFileName}.project.json")))
                    {
                        // ProjectName.Project.json
                        var lockFile = Path.Combine(projDir, $"{projectFileName}.project.lock.json");

                        var pkgs = GetProjectJsonPackages(lockFile, assm.References, nugetTargetMonikers);
                        packages.AddRange(pkgs);
                    }
                    else if (File.Exists(Path.Combine(projDir, "project.json")))
                    {
                        // Project.json
                        var lockFile = Path.Combine(projDir, "project.lock.json");
                        var pkgs = GetProjectJsonPackages(lockFile, assm.References, nugetTargetMonikers);
                        packages.AddRange(pkgs);
                    }
                    else if (File.Exists(Path.Combine(projDir, $"packages.{projectFileName}.config")))
                    {
                        var pkgs = GetPackagesConfigPackages(projectFiles[i], $"packages.{projectFileName}.config", assm.References);
                        packages.AddRange(pkgs);
                    }
                    else if (File.Exists(Path.Combine(projDir, "packages.config")))
                    {
                        var pkgs = GetPackagesConfigPackages(projectFiles[i], $"packages.config", assm.References);
                        packages.AddRange(pkgs);
                    }
                    else
                    {
                        // Must be an "old" PCL without any refs. Best we can do is read the refs
                        var pkgs = GetPackagesConfigPackages(projectFiles[i], null, assm.References);
                        packages.AddRange(pkgs);
                    }
                }


                // Now squash all but most recent
                var groups = packages.GroupBy(k => k.Id, StringComparer.OrdinalIgnoreCase)
                                     .Select(g =>
                                             g.OrderByDescending(r => r.Version)
                                              .First()
                    )
                                     .OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                                     .ToList();

                // make sure there is no mscorlib
                if (groups.Any(g => string.Equals(g.Id, "mscorlib", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("mscorlib-based projects are not supported");


                UpdateNuspecFile(nuspecFile, groups, tfms);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(new ErrorWithMessage(e));

                return -1;
            }
        }

        static void UpdateNuspecFile(string nuspecFile, IReadOnlyList<Package> packages, IEnumerable<string> tfms)
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
                    var existing = grp.Elements(nuspecNs + "dependency")
                                      .Where(e => refNames.Contains(e.Attribute("id").Value))
                                      .ToList();

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

        static IEnumerable<Package> GetProjectJsonPackages(string lockFile, IEnumerable<Reference> refs, string[] nugetTargetMonikers)
        {
            // This needs to load the project.lock.json, look for the reference under the 
            // targets -> ".NETPlatform,Version=v5.0", look for each package and the files in it, then pull out based on refs
            if (!File.Exists(lockFile))
                throw new InvalidOperationException("project.lock.json is missing");

            JObject projectJson;
            using (var reader = new JsonTextReader(File.OpenText(lockFile)))
            {
                projectJson = JObject.Load(reader);
            }

            JObject netPlatform = null;

            foreach (var tfm in nugetTargetMonikers)
            {
                // Look for the first .NETPlatform entry in the targets
                netPlatform = (JObject)((JObject)projectJson["targets"])
                                        .Properties()
                                        .FirstOrDefault(p => p.Name.StartsWith(tfm, StringComparison.OrdinalIgnoreCase))?
                                        .Value;

                // found one
                if (netPlatform != null)
                    break;
            }

            if (netPlatform == null)
            {
                // error
                throw new InvalidOperationException($"project.lock.json is missing TFM for {string.Join(" or ", nugetTargetMonikers)}");
            }

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

        static IEnumerable<Package> GetPackagesConfigPackages(string projectFile, string packagesConfig, IEnumerable<Reference> assemblyRefs)
        {
            // For projects that uses packages.config, we need to do a few things:
            // 1. Read the packages from the config file
            // 2. Read the references from the project file to match files to packages
            // 3. For System packages, we'll have to rely on assembly version -> package version
            //    This appears to hold for the System contract assms from within
            //    C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETPortable

            // To determine if it's a system ref, check the file's existence in the filesystem
            // Obviously this will only work on Windows

            

            var projDoc = XDocument.Load(projectFile);
            XNamespace projNs = projDoc.Root.Attribute("xmlns")?.Value ?? string.Empty;

            // get version and profile
            var profile = projDoc.Descendants(projNs + "TargetFrameworkProfile").FirstOrDefault()?.Value;
            var version = projDoc.Descendants(projNs + "TargetFrameworkVersion").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("Only PCLs are supported by this tool. TargetFrameworkProfile or TargetFrameworkVersion is missing");

            // check the version
            double ver;
            if (!double.TryParse(version.Substring(1), NumberStyles.Any, CultureInfo.InvariantCulture, out ver) || ver < 4.5)
                throw new InvalidOperationException("Only System.Runtime-based PCL's are supported. Ensure that you're targetting at least Net45, Win8 and wp8");


            // build out system refs
            var sysRefs = (from r in assemblyRefs
                          where IsFrameworkReference(r.Name, version, profile)
                          select r)
                          .ToList();

            var otherRefs = assemblyRefs.Except(sysRefs).ToList();

            // for sys refs, we use the assm ver
            var packages = new HashSet<Package>(GetPackagesFromAssemblyRefs(sysRefs));

            if(otherRefs.Count > 0)
            {
                // Dictionary of package paths to package
                Dictionary<string, Package> packageMap;

                // if we have a packages.config, parse that into packages
                if(packagesConfig != null)
                {
                    var packagesDoc = XDocument.Load(Path.Combine(Path.GetDirectoryName(projectFile), packagesConfig));
                    var packagesNs = packagesDoc.Root.Attribute("xmlns")?.Value ?? string.Empty;

                    packageMap = packagesDoc.Descendants(packagesNs + "package")
                                   .Select(e => new Package(e.Attribute("id").Value, e.Attribute("version").Value))
                                   .ToDictionary(k => $"{k.Id}.{k.VersionString}", StringComparer.OrdinalIgnoreCase);

                }
                else
                {
                    packageMap = new Dictionary<string, Package>();
                }


                // get reference nodes
                // try to get the source package from the hint path
                var projRefs = (from e in projDoc.Descendants(projNs + "Reference")
                               let assm = e.Attribute("Include").Value.Split(',')[0]
                               let hintPath = e.Element(projNs + "HintPath")?.Value
                               let startIndex = hintPath?.IndexOf("packages\\", StringComparison.OrdinalIgnoreCase) + 9 ?? -1
                               let endIndex = hintPath?.IndexOf('\\', startIndex) ?? -1
                               let packageDir = hintPath?.Substring(startIndex, endIndex - startIndex) ?? null
                               select new { Assembly = assm, PackageDir = packageDir })
                               .ToList();


                // see if we have a package
                foreach(var projRef in projRefs.Where(pr => pr.PackageDir != null))
                {
                    Package p;
                    if(packageMap.TryGetValue(projRef.PackageDir, out p))
                    {
                        packages.Add(p);
                    }
                }
            }

            return packages;
            
        }

        static readonly string ProgramFiles = 
            Environment.Is64BitOperatingSystem? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) : 
                                                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        static readonly string PortableDir = Path.Combine(ProgramFiles, "Reference Assemblies", "Microsoft", "Framework", ".NETPortable");
        static bool IsFrameworkReference(string assemblyName, string version, string profile)
        {
            var filePath = Path.Combine(PortableDir, version, "Profile", profile, $"{assemblyName}.dll");
            return File.Exists(filePath);
        }

        static IEnumerable<Package> GetPackagesFromAssemblyRefs(IEnumerable<Reference> refs)
        {
            // These should only be system ones
            return refs.Select(r => new Package(r.Name, r.Version.ToString(3)));
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
                        new XAttribute("version", $"{r.VersionString}")
                                )));

            return ele;
        }
    }
}
