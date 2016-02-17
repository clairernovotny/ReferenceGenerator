using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ReferenceGenerator.Engine
{
    public class Project : IEquatable<Project>, IComparable<Project>
    {
        private string assemblyName;
        private string assemblyPath;
        private string configuration;
        private string platform;
        private string projectDirectory;
        private string originalPath;

        public static Project Parse(string pair)
        {
            var items = pair.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            // We have to have at least 1 element, which will be the fully qualified
            // path to the project.

            if (items.Length < 1 || items.Length > 2)
            {
                throw new InvalidOperationException("Unable to parse.");
            }

            var project = new Project(items[0]);

            // It's possible we were only given the path to the project file,
            // in which case we want to try and automatically figure out
            // the path to the assembly. This won't work if the project was
            // an XPROJ because we need to be told which configuration to look
            // at.
            if (project.IsXProject && items.Length == 1)
            {
                throw new InvalidOperationException("A path to the assembly or a configuration name must be provided.");
            }

            project.ConfigureAssemblyProperties(items.ElementAtOrDefault(1));

            return project;
        }

        private Project(string path)
        {
            originalPath = path;
            projectDirectory = Path.GetDirectoryName(path);
            ProjectFileName = Path.GetFileNameWithoutExtension(path);
            IsXProject = Path.GetExtension(path).Equals(".xproj", StringComparison.OrdinalIgnoreCase);
            ConfigurePackageProperties();
        }

        #region properties
        public bool HasProjectJson { get; private set; }

        public bool IsXProject { get; private set; }

        public string PackagesFile { get; private set; }

        public string ProjectFileName { get; private set; }
        #endregion

        #region methods

        private void ConfigureAssemblyProperties(string assemblyPathOrConfiguration)
        {
            if (File.Exists(assemblyPathOrConfiguration))
            {
                assemblyName = Path.GetFileName(assemblyPathOrConfiguration);
                assemblyPath = Path.GetDirectoryName(assemblyPathOrConfiguration);
            }
            else
            {
                var projectDocument = XElement.Load(originalPath);
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

                var currentConfiguration = GetCurrentProjectConfiguration(projectDocument, ns, assemblyPathOrConfiguration);
                configuration = currentConfiguration.Item1;
                platform = currentConfiguration.Item2;
                assemblyName = GetAssemblyName(projectDocument, ns);
                assemblyPath = GetOutputPathFromProject(projectDocument, ns);
            }
        }

        private void ConfigurePackageProperties()
        {
            if (File.Exists(Path.Combine(projectDirectory, $"{ProjectFileName}.project.json")))
            {
                HasProjectJson = true;
                PackagesFile = Path.Combine(projectDirectory, $"{ProjectFileName}.project.lock.json");
            }
            else if (File.Exists(Path.Combine(projectDirectory, "project.json")))
            {
                HasProjectJson = true;
                PackagesFile = Path.Combine(projectDirectory, "project.lock.json");
            }
            else if (File.Exists(Path.Combine(projectDirectory, $"packages.{ProjectFileName}.config")))
            {
                HasProjectJson = false;
                PackagesFile = Path.Combine(projectDirectory, $"packages.{ProjectFileName}.config");
            }
            else if (File.Exists(Path.Combine(projectDirectory, "packages.config")))
            {
                HasProjectJson = false;
                PackagesFile = Path.Combine(projectDirectory, "packages.config");
            }
            else
            {
                // Must be an "old" PCL without any refs. Best we can do is read the refs.
                HasProjectJson = false;
                PackagesFile = null;
            }
        }

        private string GetAssemblyName(XElement document, XNamespace ns)
        {
            return $"{document.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value ?? ProjectFileName}.dll";
        }

        private static Tuple<string, string> GetCurrentProjectConfiguration(XElement document, XNamespace ns, string configuration)
        {
            var computedConfiguration = document.Descendants(ns + "Configuration").SingleOrDefault()?.Value ?? configuration;
            var platformNode = document.Descendants(ns + "Platform").SingleOrDefault();
            return Tuple.Create(computedConfiguration, platformNode?.Value ?? String.Empty);
        }

        private string GetOutputPathFromProject(XElement document, XNamespace ns)
        {
            var binaryPath = String.Empty;
            var propertyGroup = GetPropertyGroup(document, ns, "Label", "Globals");
            if (propertyGroup == null)
            {
                var configurationAndPlatform = $"{configuration}{(String.IsNullOrWhiteSpace(platform) ? String.Empty : $"|{platform}")}";
                propertyGroup = GetPropertyGroup(document, ns, "Condition", configurationAndPlatform);
            }

            if (propertyGroup != null)
            {
                var outputPath = propertyGroup.Element(ns + "OutputPath").Value;
                if (outputPath.Contains("$(MSBuildProjectName)"))
                {
                    outputPath = outputPath.Replace("$(MSBuildProjectName)", ProjectFileName);
                }

                binaryPath = Path.Combine(projectDirectory, outputPath, IsXProject ? configuration : "");
            }

            return binaryPath;
        }

        private static XElement GetPropertyGroup(XElement document, XNamespace ns, string attributeName, string attributeValuePattern)
        {
            return document.Descendants(ns + "PropertyGroup").FirstOrDefault(p => p.Attribute(attributeName)?.Value.Contains(attributeValuePattern) ?? false);
        }

        public int CompareTo(Project other)
        {
            // sort on name for now
            return StringComparer.OrdinalIgnoreCase.Compare(originalPath, other?.originalPath);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Package);
        }

        public bool Equals(Project other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return string.Equals(originalPath, other.originalPath, StringComparison.OrdinalIgnoreCase) &&
                   Equals(assemblyPath, other.assemblyPath);
        }

        public AssemblyInfo GetAssemblyInfo()
        {
            return AssemblyInfo.GetAssemblyInfo(Path.Combine(assemblyPath, assemblyName));
        }

        public AssemblyInfo GetAssemblyInfo(NuGetFramework tfm)
        {
            return AssemblyInfo.GetAssemblyInfo(Path.Combine(assemblyPath, tfm.GetShortFolderName(), assemblyName));
        }

        public override int GetHashCode()
        {
            return unchecked(originalPath?.GetHashCode() ?? 1 ^ assemblyPath?.GetHashCode() ?? 1);
        }
        #endregion
    }
}
