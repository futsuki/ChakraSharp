using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

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

            var exefile = Assembly.GetEntryAssembly().Location;
            var exedir = Path.GetDirectoryName(exefile);

            Directory.SetCurrentDirectory(exedir);
            assemblyFile = Path.GetFullPath(assemblyFile);
            var targetAssembly = Assembly.LoadFile(assemblyFile);
            var w = new TypescriptDefinitionWriter();
            var outdir = Path.Combine(exedir, "output");
            var outdata = w.MakeDefineFile(new[] { targetAssembly }, outdir+Path.DirectorySeparatorChar);
            var outfile = Path.Combine(outdir, Path.ChangeExtension(Path.GetFileName(assemblyFile), "d.ts"));
            Directory.CreateDirectory(outdir);
            Console.WriteLine(string.Format("Output file: {0}", outfile));
            var sb = new StringBuilder();
            foreach (var kv in outdata)
            {
                sb.AppendLine("// " + Path.GetFileName(kv.Key));
                sb.AppendLine(kv.Value);
            }
            File.WriteAllText(outfile, sb.ToString());
            Console.WriteLine(string.Format("Finish output {0} types", outdata.Keys.Count));
        }

        static void Help()
        {
            var exefile = Assembly.GetEntryAssembly().Location;
            var file = Path.GetFileName(exefile);
            Console.WriteLine(string.Format(@"
Usage:
    {0} assemblyfile
".Trim(), file));
        }
    }
}
