using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = args[0];

            if (File.Exists(path))
            {
                try
                {
                    var assm = AssemblyInfo.GetAssemblyInfo(path);
                    CheckRefs(assm);
                }
                catch (InvalidOperationException)
                { }
            }
            else
            {
                Console.WriteLine("Error, file doesn't exist");
            }
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
