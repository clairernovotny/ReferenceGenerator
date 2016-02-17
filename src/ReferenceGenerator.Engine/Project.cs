using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ReferenceGenerator.Engine
{
    public class Project : IEquatable<Project>, IComparable<Project>
    {
        public Project(string path, string binaryPath)
        {
            ProjectPath = path;
            BinaryPath = binaryPath;
            ProjectFileName = Path.GetFileNameWithoutExtension(path);
            ProjectDirectory = Path.GetDirectoryName(path);

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

        public string ProjectPath { get; }

        public string BinaryPath { get; }

        public string ProjectFileName { get; }

        public string ProjectDirectory { get; }
        public bool HasProjectJson { get; }

        public string PackagesFile { get; }

        public AssemblyInfo GetAssemblyInfo()
        {
            return AssemblyInfo.GetAssemblyInfo(BinaryPath);
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
