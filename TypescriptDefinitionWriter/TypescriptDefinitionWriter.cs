using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace TypescriptDefinitionWriter
{
    public class TypescriptDefinitionWriterException : Exception
    {
        public TypescriptDefinitionWriterException()
        {
        }

        public TypescriptDefinitionWriterException(string message)
        : base(message)
        {
        }

        public TypescriptDefinitionWriterException(string message, Exception inner)
        : base(message, inner)
        {
        }
    }

    public class TypescriptDefinitionWriter
    {
        static char[] SpecialCharacters = "<>+`".ToCharArray();
        List<Type> GetCLRTypes(Assembly assembly)
        {
            var types = new List<Type>();
            //foreach (var t in typeof(GameObject).Assembly.GetTypes())
            foreach (var t in assembly.GetTypes())
            {
                if (t.IsSpecialName) continue;
                if (t.IsSubclassOf(typeof(MulticastDelegate))) continue;
                types.Add(t);
            }
            return types;
        }
        Dictionary<string, bool> GetNamespaces(List<Type> ts)
        {
            var nslist = new Dictionary<string, bool>();
            foreach (var t in ts)
            {
                if (t.IsSpecialName) continue;
                if (!string.IsNullOrEmpty(t.Namespace))
                {
                    nslist[t.Namespace] = true;
                }
            }
            return nslist;
        }

        Dictionary<Type, string> typedic;
        string TypeToTSType(Type t)
        {
            string s;
            string tail = "";
            bool cont = true;
            while (cont)
            {
                cont = false;
                if (t.IsArray)
                {
                    tail += "[]";
                    t = t.GetElementType();
                    cont = true;
                }
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
                {
                    tail += "[]";
                    t = t.GetGenericArguments()[0];
                    cont = true;
                }
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    tail += "[]";
                    t = t.GetGenericArguments()[0];
                    cont = true;
                }
            }
            if (typedic.TryGetValue(t, out s))
            {
                return s + tail;
            }
            if (t.IsSubclassOf(typeof(MulticastDelegate)))
            {
                var mi = t.GetMethod("Invoke");
                var ps = mi.GetParameters();
                var r = mi.ReturnType;
                var rt = TypeToTSType(r);
                return string.Format("(({0}) => {1}){2}",
                    string.Join(", ", ps.Select(e => e.Name + (IsJSKeyword(e.Name) ? "_" : "") + ": " + TypeToTSType(e.ParameterType)).ToArray()),
                    TypeToTSType(r),
                    tail);
            }
            return "any" + tail;
        }
        void DefineTypeDic(Dictionary<Type, string> typedic, List<System.Type> types)
        {
            foreach (var t in types)
            {
                typedic[t] = t.FullName.Replace("+", ".");
            }
            typedic[typeof(string)] = "string";
            typedic[typeof(sbyte)] = "number";
            typedic[typeof(byte)] = "number";
            typedic[typeof(short)] = "number";
            typedic[typeof(ushort)] = "number";
            typedic[typeof(int)] = "number";
            typedic[typeof(uint)] = "number";
            typedic[typeof(long)] = "number";
            typedic[typeof(ulong)] = "number";
            typedic[typeof(float)] = "number";
            typedic[typeof(double)] = "number";
            typedic[typeof(decimal)] = "number";
            typedic[typeof(object)] = "any";
            typedic[typeof(void)] = "void";
            typedic[typeof(bool)] = "boolean";
        }
        void MakeTypeDic(List<System.Type> types)
        {
            typedic = new Dictionary<Type, string>();
            DefineTypeDic(typedic, types);
        }
        static string TypeToPath(Type t)
        {
            var path = t.FullName.Replace("+", ".");
            return path;
        }
        static Dictionary<string, bool> JSkeywords;
        static bool IsJSKeyword(string str)
        {
            if (JSkeywords == null)
            {
                var jskeywords = "abstract arguments boolean break byte case catch char class* const continue debugger default delete do double else enum* eval export* extends* false final finally float for function goto if implements import* in instanceof int interface let long native new null package private protected public return short static super* switch synchronized this throw throws transient true try typeof var void volatile while with yield";
                JSkeywords = new Dictionary<string, bool>();
                foreach (var s in jskeywords.Split(' '))
                {
                    JSkeywords[s] = true;
                }
            }
            return JSkeywords.ContainsKey(str);
        }
        public Dictionary<string, string> MakeDefineFile(Assembly[] assemblies, string outputdir)
        {
            var ret = new Dictionary<string, string>();
            System.IO.Directory.CreateDirectory(outputdir);
            var types = assemblies.Select(e => GetCLRTypes(e)).SelectMany(e => e).ToList();
            var tdic = new Dictionary<Type, bool>();
            Action<System.Type> tf = null;
            tf = (t) =>
            {
                if (t == null) return;
                tdic[t] = true;
                tf(t.BaseType);
            };
            types.ForEach(e => tf(e));
            var ignoredType = new HashSet<Type>
            {
            };
            ignoredType.ToList().ForEach(e => tdic.Remove(e));
            types = tdic.Keys.Where(e => !e.IsArray).ToList();

            MakeTypeDic(types);
            foreach (var t in types)
            {
                var path = TypeToPath(t);
                string ns = "";
                if (path.LastIndexOf('.') != -1)
                {
                    ns = path.Substring(0, path.LastIndexOf('.'));
                }
                if (path.IndexOfAny(SpecialCharacters) != -1)
                {
                    //if (t.FullName.IndexOfAny(SpecialCharacters) != -1) {
                    //Console.WriteLine("Ignored: " + path);
                    continue;
                }
                var sb = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(ns))
                    sb.AppendFormat("declare namespace {0} {{\n", ns);
                var bt = t.BaseType;
                if (ignoredType.Contains(bt)) bt = null;
                if (bt == null || bt.IsGenericType)
                {
                    sb.AppendFormat("  class {0} {{\n", t.Name);
                }
                else
                {
                    sb.AppendFormat("  class {0} extends {1} {{\n", t.Name, TypeToPath(t.BaseType));
                }

                //constructors
                sb.AppendFormat("    // constructors\n");
                bool anyCtor = false;
                foreach (var m in t.GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Instance))
                {
                    if (t.IsValueType)
                        continue;
                    if (m.IsGenericMethod)
                        continue;
                    anyCtor = true;
                    var ps = m.GetParameters();
                    sb.AppendFormat("    constructor(");
                    bool first = true;
                    foreach (var p in ps)
                    {
                        if (!first)
                        {
                            sb.Append(", ");
                        }
                        var pt = p.ParameterType;
                        var name = p.Name;
                        if (IsJSKeyword(name))
                            name = name + "_";
                        sb.AppendFormat("{0}{2}: {1}", name, TypeToTSType(pt), p.IsOptional ? "?" : "");
                        first = false;
                    }
                    sb.AppendFormat(");\n");
                }
                if (!anyCtor)
                {
                    // deny new
                    sb.AppendFormat("    protected constructor();\n");
                }

                Action<MethodInfo> writeMethod = (m) =>
                {
                    if (m == null)
                        return;
                    var ps = m.GetParameters();
                    var fname = m.Name;
                    if (IsJSKeyword(fname))
                        fname = "\"" + fname + "\"";
                    sb.AppendFormat("    {1}{0}(", fname, m.IsStatic ? "static " : "");
                    bool first = true;
                    foreach (var p in ps)
                    {
                        if (!first)
                        {
                            sb.Append(", ");
                        }
                        var pt = p.ParameterType;
                        var name = p.Name;
                        if (IsJSKeyword(name))
                            name = name + "_";
                        sb.AppendFormat("{0}{2}: {1}", name, TypeToTSType(pt), p.IsOptional ? "?" : "");
                        first = false;
                    }
                    var rettype = TypeToTSType(m.ReturnType);
                    sb.AppendFormat("): {0};\n", rettype);
                };
                //methods
                sb.AppendFormat("    // methods\n");
                var bindflag = BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Instance;
                if (t.BaseType != null && t.BaseType.IsGenericType)
                    bindflag ^= BindingFlags.DeclaredOnly;

                var allmethods = t.GetMethods(BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.Instance);
                var methods = t.GetMethods(bindflag);
                var methodsDic = new Dictionary<string, bool>();
                foreach (var m in methods)
                {
                    methodsDic[m.Name] = true;
                }
                var allmethodsDic = new Dictionary<string, List<MethodInfo>>();
                foreach (var m in allmethods)
                {
                    if (!allmethodsDic.ContainsKey(m.Name))
                    {
                        allmethodsDic[m.Name] = new List<MethodInfo>();
                    }
                    allmethodsDic[m.Name].Add(m);
                }
                foreach (var mname in methodsDic.Keys)
                {
                    /*
                    if (m.IsGenericMethod)
                        continue;
                    if (m.IsSpecialName) continue;
                    */
                    //if (!t.IsValueType && !m.IsStatic) continue;
                    //if (!t.IsValueType && noPublicConstructor) continue;
                    foreach (var m2 in allmethodsDic[mname].Distinct())
                    {
                        if (m2.IsSpecialName)
                            continue;
                        if (m2.IsGenericMethod)
                            continue;
                        writeMethod(m2);
                    }
                }

                //props
                sb.AppendFormat("    // properties\n");
                foreach (var m in t.GetProperties(bindflag))
                {
                    if (m.IsSpecialName) continue;
                    var idxps = m.GetIndexParameters();
                    var gm = m.GetGetMethod();
                    //if (!t.IsValueType && noPublicConstructor) continue;
                    if (idxps.Length > 0)
                    {
                        // indexer property
                        if (gm.IsStatic) continue;
                        var sm = m.GetSetMethod();
                        //sb.AppendFormat("    {1}[index: {2}]: {0};\n", TypeToTSType(m.PropertyType), ro, TypeToTSType(idxps[0].ParameterType));
                        //Debug.Log(path+" "+gm.Name+" "+sm.Name);
                        writeMethod(gm);
                        writeMethod(sm);
                        /*
                        sb.AppendFormat("    get_Item(index: {2}): {0};\n", TypeToTSType(m.PropertyType), ro, TypeToTSType(idxps[0].ParameterType));
                        if (sm != null)
                        {
                            sb.AppendFormat("    set_Item(index: {2}, value: {0}): void;\n", TypeToTSType(m.PropertyType), ro, TypeToTSType(idxps[0].ParameterType));
                        }
                        */
                    }
                    else if (idxps.Length > 1)
                    {
                        throw new TypescriptDefinitionWriterException("property panic!" + path);
                    }
                    else
                    {
                        var ro = (m.GetGetMethod() != null && m.GetSetMethod() == null) ? "readonly " : "";
                        var fname = m.Name;
                        if (gm != null)
                        {
                            if (IsJSKeyword(fname))
                                fname = "\"" + fname + "\"";
                            sb.AppendFormat("    {1}{3}{0}: {2};\n", fname, gm.IsStatic ? "static " : "", TypeToTSType(m.PropertyType), ro);
                        }
                    }
                }

                //fields
                sb.AppendFormat("    // fields\n");
                foreach (var m in t.GetFields(bindflag))
                {
                    //if (!t.IsValueType && !m.IsStatic) continue;
                    //if (!t.IsValueType && noPublicConstructor) continue;
                    var fname = m.Name;
                    if (IsJSKeyword(fname))
                        fname = "\"" + fname + "\"";
                    sb.AppendFormat("    {1}{0}: {2};\n", fname, m.IsStatic ? "static " : "", TypeToTSType(m.FieldType));
                }

                sb.AppendFormat("  }}\n");
                if (!string.IsNullOrEmpty(ns))
                    sb.AppendFormat("}}\n");
                //if (anyWrite)
                //{
                //always output for type definition
                //System.IO.File.WriteAllText("Z:\\out\\UnityEngine.dll.d.ts\\src\\" + path + ".d.ts", sb.ToString());
                var data = sb.ToString().Replace("\n", "\r\n").Replace("\r\r", "\r");
                ret[outputdir + path + ".d.ts"] = data;
                //System.IO.File.WriteAllText(outputdir + path + ".d.ts", data);
                //}
                //else
                //{
                //Debug.Log("Ignored empty class: "+path);
                //}
            }
            return ret;
        }
    }
}