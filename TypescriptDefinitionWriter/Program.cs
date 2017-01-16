using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TypescriptDefinitionWriter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Help();
                return;
            }
            var assemblyFile = args[0];

            var exefile = System.Reflection.Assembly.GetEntryAssembly().Location;
            var exedir = System.IO.Path.GetDirectoryName(exefile);

            System.IO.Directory.SetCurrentDirectory(exedir);
            assemblyFile = System.IO.Path.GetFullPath(assemblyFile);
            var targetAssembly = System.Reflection.Assembly.LoadFile(assemblyFile);
            var w = new TypescriptDefinitionWriter();
            var outdir = System.IO.Path.Combine(exedir, "output");
            var outdata = w.MakeDefineFile(new[] { targetAssembly }, outdir+System.IO.Path.DirectorySeparatorChar);

            //System.IO.Directory.CreateDirectory(outdir);
            foreach (var kv in outdata)
            {
                System.IO.File.WriteAllText(kv.Key, kv.Value);
                Console.WriteLine("Output "+System.IO.Path.GetFileName(kv.Key));
            }
            Console.WriteLine("Finish!");
        }

        static void Help()
        {
            var exefile = System.Reflection.Assembly.GetEntryAssembly().Location;
            var file = System.IO.Path.GetFileName(exefile);
            Console.WriteLine(string.Format(@"
Usage:
    {0} assemblyfile
".Trim(), file));
        }
    }
}
