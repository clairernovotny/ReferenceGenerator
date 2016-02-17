using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using NuGet.Frameworks;

namespace ReferenceGenerator.Engine
{
    public class Project : IEquatable<Project>, IComparable<Project>
    {
        public Project(string path, string configuration)
        {
            ProjectPath = path;
            ProjectFileName = Path.GetFileNameWithoutExtension(path);
            ProjectDirectory = Path.GetDirectoryName(path);
            IsXProject = Path.GetExtension(path).Equals(".xproj", StringComparison.OrdinalIgnoreCase);
            ConfigurePackageProperties();
            ConfigurePropertiesFromProjectFile(configuration);
        }

        private string AssemblyName { get; set; }

        private string ProjectPath { get; }

        private string BinaryPath { get; set; }

        public string ProjectFileName { get; }

        private string Platform { get; set; }

        private string Configuration { get; set; }

        private string ProjectDirectory { get; }

        public bool HasProjectJson { get; private set; }

        public bool IsXProject { get; }

        public string PackagesFile { get; private set; }

        private static Tuple<string, string> GetCurrentProjectConfiguration(XElement document, XNamespace ns, string configuration)
        {
            var computedConfiguration = document.Descendants(ns + "Configuration").SingleOrDefault()?.Value ?? configuration;
            var platformNode = document.Descendants(ns + "Platform").SingleOrDefault();
            return Tuple.Create(computedConfiguration, platformNode?.Value ?? String.Empty);
        }

        private static XElement GetPropertyGroup(XElement document, XNamespace ns, string attributeName, string attributeValuePattern)
        {
            return document.Descendants(ns + "PropertyGroup").FirstOrDefault(p => p.Attribute(attributeName)?.Value.Contains(attributeValuePattern) ?? false);
        }

        private string GetAssemblyName(XElement document, XNamespace ns)
        {
            return $"{document.Descendants(ns + "AssemblyName").FirstOrDefault()?.Value ?? ProjectFileName}.dll";
        }

        private string GetOutputPathFromProject(XElement document, XNamespace ns)
        {
            var binaryPath = String.Empty;
            var propertyGroup = GetPropertyGroup(document, ns, "Label", "Globals");
            if (propertyGroup == null)
            {
                var configurationAndPlatform = $"{Configuration}{(String.IsNullOrWhiteSpace(Platform) ? String.Empty : $"|{Platform}")}";
                propertyGroup = GetPropertyGroup(document, ns, "Condition", configurationAndPlatform);
            }

            if (propertyGroup != null)
            {
                var outputPath = propertyGroup.Element(ns + "OutputPath").Value;
                if (outputPath.Contains("$(MSBuildProjectName)"))
                {
                    outputPath = outputPath.Replace("$(MSBuildProjectName)", ProjectFileName);
                }

                binaryPath = Path.Combine(ProjectDirectory, outputPath, IsXProject ? Configuration : "");
            }

            return binaryPath;
        }

        private void ConfigurePropertiesFromProjectFile(string configurationFallback)
        {
            var binaryPath = String.Empty;
            var projectDocument = XElement.Load(ProjectPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var currentConfiguration = GetCurrentProjectConfiguration(projectDocument, ns, configurationFallback);
            Configuration = currentConfiguration.Item1;
            Platform = currentConfiguration.Item2;
            AssemblyName = GetAssemblyName(projectDocument, ns);
            BinaryPath = GetOutputPathFromProject(projectDocument, ns);
        }

        private void ConfigurePackageProperties()
        {
            if (File.Exists(Path.Combine(ProjectDirectory, $"{ProjectFileName}.project.json")))
            {
                HasProjectJson = true;
                PackagesFile = Path.Combine(ProjectDirectory, $"{ProjectFileName}.project.lock.json");
            }
            else if (File.Exists(Path.Combine(ProjectDirectory, "project.json")))
            {
                HasProjectJson = true;
                PackagesFile = Path.Combine(ProjectDirectory, "project.lock.json");
            }
            else if (File.Exists(Path.Combine(ProjectDirectory, $"packages.{ProjectFileName}.config")))
            {
                HasProjectJson = false;
                PackagesFile = Path.Combine(ProjectDirectory, $"packages.{ProjectFileName}.config");
            }
            else if (File.Exists(Path.Combine(ProjectDirectory, "packages.config")))
            {
                HasProjectJson = false;
                PackagesFile = Path.Combine(ProjectDirectory, "packages.config");
            }
            else
            {
                // Must be an "old" PCL without any refs. Best we can do is read the refs.
                HasProjectJson = false;
                PackagesFile = null;
            }
        }

        public AssemblyInfo GetAssemblyInfo()
        {
            return AssemblyInfo.GetAssemblyInfo(Path.Combine(BinaryPath, AssemblyName));
        }

        public AssemblyInfo GetAssemblyInfo(NuGetFramework tfm)
        {
            return AssemblyInfo.GetAssemblyInfo(Path.Combine(BinaryPath, tfm.GetShortFolderName(), AssemblyName));
        }

        public int CompareTo(Project other)
        {
            // sort on name for now
            return StringComparer.OrdinalIgnoreCase.Compare(ProjectPath, other?.ProjectPath);
        }

        public bool Equals(Project other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return string.Equals(ProjectPath, other.ProjectPath, StringComparison.OrdinalIgnoreCase) &&
                   Equals(BinaryPath, other.BinaryPath);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Package);
        }

        public override int GetHashCode()
        {
            return unchecked(ProjectPath?.GetHashCode() ?? 1 ^ BinaryPath?.GetHashCode() ?? 1);
        }
    }
}
