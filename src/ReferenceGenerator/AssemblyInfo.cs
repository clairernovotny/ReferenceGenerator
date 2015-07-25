using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ReferenceGenerator
{ 
    internal class AssemblyInfo
    {
        private AssemblyInfo(string path, string name, Version version, Reference[] references)
        {
            Path = path;
            Name = name;
            Version = version;
            References = references;
        }

        public string Path
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public Version Version
        {
            get; private set;
        }

        public Reference[] References
        {
            get;
            private set;
        }

        public static AssemblyInfo GetAssemblyInfo(string path)
        {
            using (PEReader peReader = new PEReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
            {
                MetadataReader contractReader = peReader.GetMetadataReader();
                AssemblyDefinition assembly = contractReader.GetAssemblyDefinition();

                string name = contractReader.GetString(assembly.Name);
                Version version = assembly.Version;
                Reference[] references = GetAssemblyReferences(contractReader);

                return new AssemblyInfo(path, name, version, references);
            }
        }

        private static Reference[] GetAssemblyReferences(MetadataReader reader)
        {
            List<Reference> references = new List<Reference>();

            foreach (var handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                references.Add(new Reference(reader.GetString(reference.Name), reference.Version));
            }

            return references.ToArray();
        }

    }
}
