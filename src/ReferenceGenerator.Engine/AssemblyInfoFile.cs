using System;
using System.Reflection.Metadata;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;

namespace ReferenceGenerator.Engine
{
    public class AssemblyInfo
    {
        AssemblyInfo(string path, string name, Version version, Reference[] references)
        {
            Path = path;
            Name = name;
            Version = version;
            References = references;
        }

        public string Name { get; private set; }

        public string Path { get; private set; }

        public Reference[] References { get; private set; }

        public Version Version { get; private set; }

        public static AssemblyInfo GetAssemblyInfo(string path)
        {
            using (var peReader = new PEReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
            {
                var contractReader = peReader.GetMetadataReader();
                var assembly = contractReader.GetAssemblyDefinition();

                var name = contractReader.GetString(assembly.Name);
                var version = assembly.Version;
                var references = GetAssemblyReferences(contractReader);

                return new AssemblyInfo(path, name, version, references);
            }
        }

        static Reference[] GetAssemblyReferences(MetadataReader reader)
        {
            var references = new List<Reference>();

            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                references.Add(new Reference(reader.GetString(reference.Name), reference.Version));
            }

            return references.ToArray();
        }
    }
}