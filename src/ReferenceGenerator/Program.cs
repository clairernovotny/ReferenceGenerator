using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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


            // Read nuspec as xml
            var xdoc = XDocument.Load(path);
            // get dependencies and add them.
            //try
            //{
            //    var assm = AssemblyInfo.GetAssemblyInfo(path);
            //    CheckRefs(assm);
            //}
            //catch (InvalidOperationException)
            //{ }

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
