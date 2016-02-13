using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using ReferenceGenerator.Properties;

namespace ReferenceGenerator
{
    class Program
    {
        static HashSet<string> MicrosoftRefs;

        private static int Main(string[] args)
        {
            // args 0: NuGetTargetMonikers -- .NETStandard,Version=v1.4  
            // args 1: TFM's to generate, semi-colon joined. E.g.: auto;uap10.0 
            // args 2: nuspec file
            // args 3: project file (csproj/vbproj, etc). Used to look for packages.config/project.json and references. should match order of target files
            // args 4: target files, semi-colon joined

            try
            {
                var nugetTargetMonikers = args[0].Split(';')
                                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                                 .Select(NuGetFramework.Parse)
                                                 .ToArray();
                var tfms = args[1].Split(';')
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .Select( NuGetFramework.Parse)
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



                // calc target for PCL profiles
                var firstTfm = nugetTargetMonikers.FirstOrDefault();
                if (firstTfm != null)
                {
                    for (var i = 0; i < tfms.Length; i++)
                    {
                        // look for an unsupported TFM and calc the result
                        if (!tfms[i].IsUnsupported)
                            continue;

                        if (firstTfm.IsPCL)
                        {
                            var profileVer = int.Parse(nugetTargetMonikers[0].Profile.Substring(7), CultureInfo.InvariantCulture);
                            // map the PCL profile to a netstandard target
                            tfms[i] = DefaultPortableFrameworkMappings.Instance.CompatibilityMappings.First(t => t.Key == profileVer).Value.Max;
                        }
                        else if (firstTfm.IsPackageBased)
                        {
                            tfms[i] = firstTfm;
                        }
                        else
                        {
                            Console.Error.WriteLine(ErrorWithMessage.TargetFrameworkNotFound);
                        }
                    }
                }
              
                


                var packages = new List<PackageWithReference>();

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
                        var pkgs = GetPackagesConfigPackages(projectFiles[i], "packages.config", assm.References);
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
            catch (UnixNotSupportedException)
            {
                // If we're in a place where we cannot check reference assemblies on Unix, issue a 
                // warning and return a non-error code 
                Console.Error.WriteLine(WarningWithMessage.ClassicPclUnix);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(new ErrorWithMessage(e));

                return -1;
            }
        }

        static void UpdateNuspecFile(string nuspecFile, IReadOnlyList<PackageWithReference> packages, IEnumerable<NuGetFramework> tfms)
        {
            // Takes the input TFMs that the user specified and writes. For portable, we squash "inbox" references and then apply baseline updates
            foreach (var tfm in tfms)
            {
                foreach(var tuple in SquashBuiltInPackages(packages, tfm))
                {
                    var baselined = ApplyBaselinePackageVersions(tuple.Item2).ToList();
                    UpdateNuSpecFileForTfm(nuspecFile, baselined, tuple.Item1);
                }
            }
        }

        static  IEnumerable<Tuple<NuGetFramework, IEnumerable<PackageWithReference>>> SquashBuiltInPackages(IReadOnlyList<PackageWithReference> packages, NuGetFramework framework)
        {
            // This method will calculate compatible NuGetFrameworks where the input can run on and then trim the package list based on what's in-box

            if (framework.IsPackageBased)
            {
                // Return the package-based group untouched
                yield return new Tuple<NuGetFramework, IEnumerable<PackageWithReference>>(framework, packages);
                foreach (var fx in CompatibilityListProvider.Default.GetFrameworksSupporting(framework))
                {
                    if (!FrameworkListCollection.Contains(fx))
                        continue;

                    var frameworkList = FrameworkListCollection.GetFrameworkList(fx);


                    // Filter down the packages if based on the frameworklist
                    var toDrop = packages.Where(p => frameworkList.ContainsReference(p.Reference)).ToList();

                    var filtered = packages.Except(toDrop).OrderBy(p => p.Id).ToList();
                    
                    yield return new Tuple<NuGetFramework, IEnumerable<PackageWithReference>>(fx, filtered);
                }
            }
            else
            {
                yield return new Tuple<NuGetFramework, IEnumerable<PackageWithReference>>(framework, packages);
            }
        }

        /// <summary>
        /// Writes a specific TFM and its packages into the nuspec file
        /// </summary>
        /// <param name="nuspecFile"></param>
        /// <param name="framework"></param>
        /// <param name="packages"></param>
        static void UpdateNuSpecFileForTfm(string nuspecFile, IReadOnlyList<Package> packages, NuGetFramework framework)
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

         
            var ele = CreateDependencyElement(framework, packages, nuspecNs);

            // see if we have a node with this tfm
            var grp = deps.XPathSelectElement($"./ns:group[@targetFramework='{framework.GetShortFolderName()}']", nsm);
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
            

            xdoc.Save(nuspecFile, SaveOptions.OmitDuplicateNamespaces); // TODO: handle read-only files and return error
        }

        static IEnumerable<PackageWithReference> GetProjectJsonPackages(string lockFile, IEnumerable<Reference> refs, NuGetFramework[] nugetTargetMonikers)
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
            NuGetFramework chosenTfm = null;

            foreach (var tfm in nugetTargetMonikers)
            {
                // Look for the first .NETPlatform entry in the targets
                netPlatform = (JObject)((JObject)projectJson["targets"])
                                        .Properties()
                                        .FirstOrDefault(p => p.Name.StartsWith(tfm.DotNetFrameworkName, StringComparison.OrdinalIgnoreCase))?
                                        .Value;

                // found one
                if (netPlatform != null)
                {
                    chosenTfm = tfm;
                    break;
                }
            }

            if (netPlatform == null)
            {
                // error
                throw new InvalidOperationException($"project.lock.json is missing TFM for {string.Join(" or ", nugetTargetMonikers.Select(t => t.DotNetFrameworkName))}");
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
                           select new PackageWithReference(kvp.Value.Id, kvp.Value.VersionString, assmRef)).ToList();
            

            if (chosenTfm.IsPCL)
            {
                // we're dealing with an "classic PCL"
                // We need to determine system refs

                

                // build out system refs
                var sysRefs = (from r in refs
                               where IsFrameworkReference(r.Name, chosenTfm)
                               select r)
                              .ToList();

                // filter out any system packages that might be present as a nuget package
                var toAdd = from sr in sysRefs
                            where !results.Any(p => p.Id.Equals(sr.Name, StringComparison.OrdinalIgnoreCase))
                            select sr;

                // for sys refs, we use the assm ver
                results.AddRange(GetPackagesFromAssemblyRefs(toAdd));
            }
            
            return results;
        }

        static IEnumerable<PackageWithReference> GetPackagesConfigPackages(string projectFile, string packagesConfig, IEnumerable<Reference> assemblyRefs)
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


            var framework = new NuGetFramework(".NETPortable", Version.Parse(version.Substring(1)), profile);
            // build out system refs
            var sysRefs = (from r in assemblyRefs
                          where IsFrameworkReference(r.Name, framework)
                          select r)
                          .ToList();

            var otherRefs = assemblyRefs.Except(sysRefs).ToList();

            // for sys refs, we use the assm ver
            var packages = new HashSet<PackageWithReference>(GetPackagesFromAssemblyRefs(sysRefs));

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
                               select new { Assembly = assm, PackageDir = packageDir, Reference = otherRefs.FirstOrDefault(r => r.Name.Equals(assm, StringComparison.OrdinalIgnoreCase)) })
                               .ToList();


                // see if we have a package
                foreach(var projRef in projRefs.Where(pr => pr.PackageDir != null))
                {
                    Package p;
                    if(packageMap.TryGetValue(projRef.PackageDir, out p))
                    {
                        packages.Add(new PackageWithReference(p.Id, p.VersionString, projRef.Reference));
                    }
                }
            }

            return packages;
        }

        static readonly string PortableDir = GetPortableDirWindows();
        static bool IsFrameworkReference(string assemblyName, NuGetFramework framework)
        {

            // If we're not on Windows, we cannot reliably know the reference assemblies
            if (!IsWindows)
            {
                throw new UnixNotSupportedException();
            }

            var filePath = Path.Combine(PortableDir, $"v{framework.Version.GetDisplayVersion()}", "Profile", framework.Profile, $"{assemblyName}.dll");
            return File.Exists(filePath);
        }
        
        static IEnumerable<PackageWithReference> GetPackagesFromAssemblyRefs(IEnumerable<Reference> refs)
        {
            // These should only be system ones
            return refs.Select(r => new PackageWithReference(r.Name, r.Version.ToString(3), r));
        }

        static IEnumerable<Package> ApplyBaselinePackageVersions(IEnumerable<Package> packages)
        {
            var blp = BaseLinePackages.Value;
            foreach (var package in packages)
            {
                Package baseline;
                if (blp.TryGetValue(package.Id, out baseline))
                {
                    // baseline knows about the package, make sure we use the higher ver
                    yield return (baseline.Version > package.Version ? baseline : package);
                    
                }
                else
                {
                    yield return package;
                }
            }
        }

        static XElement GetOrCreateDependenciesNode(XDocument doc, XNamespace nuspecNs)
        {
            var mde = doc.Root.Element(nuspecNs + "metadata");
            if (mde == null)
                throw new ArgumentException("NuSpec XML namespaces are not correctly formed. Ensure the xmlns is on the root package element", nameof(doc));

            var deps = mde.Element(nuspecNs + "dependencies");

            if (deps == null)
            {
                deps = new XElement(nuspecNs + "dependencies");
                mde.Add(deps);
            }

            return deps;
        }

        static XElement CreateDependencyElement(NuGetFramework tfm, IEnumerable<Package> refs, XNamespace nuspecNs)
        {
            var ele = new XElement(nuspecNs + "group", new XAttribute("targetFramework", tfm.GetShortFolderName()),
                refs.Select(r =>
                    new XElement(nuspecNs + "dependency",
                        new XAttribute("id", r.Id),
                        new XAttribute("version", $"{r.VersionString}")
                                )));

            return ele;
        }

        static string GetPortableDirWindows()
        {
            var programFiles =
            Environment.Is64BitOperatingSystem ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) :
                                                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var portableDir = Path.Combine(programFiles, "Reference Assemblies", "Microsoft", "Framework", ".NETPortable");

            return portableDir;
        }

        static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        static Dictionary<string, Package> LoadBaselinePackagesFromResources()
        {
            var doc = XDocument.Parse(Resources.baseline_packages);

            var ns = doc.Root.Name.Namespace;
            var baseLineNodes = doc.Descendants(ns + "BaseLinePackage");
            var packageEnum = baseLineNodes.Select(p => new Package(p.Attribute("Include").Value, p.Element(ns + "Version").Value));

            return packageEnum.ToDictionary(k => k.Id);
        }

        static readonly Lazy<Dictionary<string, Package>> BaseLinePackages = new Lazy<Dictionary<string, Package>>(LoadBaselinePackagesFromResources);
        
    }

    [Serializable]
    class UnixNotSupportedException : Exception
    {
        
    }
}
