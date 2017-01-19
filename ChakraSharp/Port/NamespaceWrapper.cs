using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using ChakraHost.Hosting;
using System.Linq.Expressions;
using System.Reflection;

namespace ChakraSharp.Port
{
    public class NamespaceWrapper
    {
        public NamespaceWrapper parent;
        public Dictionary<string, NamespaceWrapper> children;
        public bool isType;
        public string path;
        public Type type;
        public string name;
        public JavaScriptValue value;

        public override string ToString()
        {
            return "NamespaceWrapper(" + path + ")";
        }

        static NamespaceWrapper MakeType(Type t)
        {
            var p = t.FullName;
            if (t.IsGenericTypeDefinition)
                p = p.Substring(0, p.IndexOf('`'));
            //Console.WriteLine(p);
            p = p.Replace('+', '.');
            var node = new NamespaceWrapper();
            node.type = t;
            node.isType = true;
            node.path = p;
            var idx = p.LastIndexOf('.');
            node.name = idx == -1 ? p : p.Substring(idx + 1);
            return node;
        }
        static NamespaceWrapper MakeNamespace(string path)
        {
            var p = path;
            p = p.Replace('+', '.');
            var node = new NamespaceWrapper();
            node.type = null;
            node.isType = false;
            node.path = p;
            var idx = p.LastIndexOf('.');
            node.name = idx == -1 ? p : p.Substring(idx + 1);
            return node;
        }

        static Dictionary<Type, NamespaceWrapper> typeToNode;
        static Dictionary<string, NamespaceWrapper> allNodes;
        static NamespaceWrapper Root
        {
            get
            {
                return allNodes[""];
            }
        }
        static public void ClearCache()
        {
            typeToNode = null;
            allNodes = null;
        }

        static void RegisterTypeNode(NamespaceWrapper target)
        {
            var t = target.type;
            var node = target;
            var path = target.path;
            while (true)
            {
                bool finish = false;
                string parentPath, name;

                var idx = path.LastIndexOf('.');
                if (idx == -1)
                {
                    finish = true;
                    name = path;
                    parentPath = "";
                }
                else
                {
                    parentPath = path.Substring(0, idx);
                    name = path.Substring(idx + 1);
                }

                NamespaceWrapper parentNode;
                if (!allNodes.TryGetValue(parentPath, out parentNode))
                {
                    parentNode = NamespaceWrapper.MakeNamespace(parentPath);
                    allNodes[parentPath] = parentNode;
                }
                if (parentNode.children == null)
                    parentNode.children = new Dictionary<string, NamespaceWrapper>();
                if (!parentNode.children.ContainsKey(name))
                    parentNode.children[name] = node;
                node.parent = parentNode;
                if (finish)
                    break;
                path = parentPath;
                node = parentNode;
            }
        }

        static char[] SpecialChars = new[] { '<', '>' };
        static void EnsureNamespaceList()
        {
            Console.WriteLine("enxure: ");
            if (allNodes != null)
                return;
            typeToNode = new Dictionary<Type, NamespaceWrapper>();
            allNodes = new Dictionary<string, NamespaceWrapper>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type t in a.GetTypes())
                {
                    if (t.IsSpecialName)
                        continue;
                    if (t.IsGenericType)
                        continue;
                    if (t.IsGenericTypeDefinition)
                    {
                        Console.WriteLine("def: "+ t);
                        continue;
                    }
                    else
                    {
                        if (t.IsGenericType)
                            continue;
                    }
                    if (t.IsInterface)
                        continue;
                    if (!t.IsVisible)
                        continue;
                    var p = t.FullName;
                    if (!t.IsGenericTypeDefinition && p.IndexOfAny(SpecialChars) != -1)
                        continue;

                    var node = NamespaceWrapper.MakeType(t);
                    allNodes[node.path] = node;
                    typeToNode[node.type] = node;
                }
            }
            foreach (var k in allNodes.Keys.ToArray())
            {
                var node = allNodes[k];
                RegisterTypeNode(node);
            }
        }


        public static NamespaceWrapper Get(string path)
        {
            EnsureNamespaceList();
            if (!allNodes.ContainsKey(path))
                return null;
            var node = allNodes[path];
            return node;
        }

        List<PropertyProxy> himo = new List<PropertyProxy>();
        void AssignToObject(JavaScriptValue obj)
        {
            if (children == null)
                return;
            var getpropid = JavaScriptPropertyId.FromString("get");
            var setpropid = JavaScriptPropertyId.FromString("set");
            himo = new List<PropertyProxy>();
            foreach (var kv in children)
            {
                var n = kv.Value;
                var t = n.type;
                var prop = JavaScriptPropertyId.FromString(n.name);
                var desc = JavaScriptValue.CreateObject();
                var pp = new PropertyProxy();
                himo.Add(pp);
                pp.node = n;
                pp.thisPtr = GCHandle.Alloc(pp);
                desc.SetProperty(getpropid, JavaScriptValue.CreateFunction(PropertyProxy.PropertyGetterDg, GCHandle.ToIntPtr(pp.thisPtr)), true);
                desc.SetProperty(setpropid, JavaScriptValue.CreateFunction(PropertyProxy.PropertySetterDg, GCHandle.ToIntPtr(pp.thisPtr)), true);
                obj.DefineProperty(prop, desc);
            }
        }
        public JavaScriptValue GetJavaScriptValue()
        {
            if (!value.IsValid)
            {
                if (isType)
                {
                    value = Util.WrapType(type);
                }
                else
                {
                    value = JavaScriptValue.CreateObject();
                    value.SetIndexedProperty(JavaScriptValue.FromString("toString"),
                        JavaScriptValue.CreateFunction(InternalUtil.GetSavedStringDg, GCHandle.ToIntPtr(GCHandle.Alloc(path))));
                }
                AssignToObject(value);
            }
            return value;
        }
        


        class PropertyProxy
        {
            JavaScriptValue entitySymbol_;
            public GCHandle thisPtr;
            public NamespaceWrapper node;

            public static JavaScriptNativeFunction PropertyGetterDg = PropertyGetter;
            public static JavaScriptNativeFunction PropertySetterDg = PropertySetter;

            public JavaScriptValue EntitySymbol
            {
                get
                {
                    if (!entitySymbol_.IsValid)
                    {
                        Native.JsCreateSymbol(JavaScriptValue.FromString("CLRNativeValue"), out entitySymbol_);
                    }
                    return entitySymbol_;
                }
            }

            public static JavaScriptValue PropertyGetter(JavaScriptValue callee,
                [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
                ushort argumentCount,
                IntPtr callbackData)
            {
                try
                {
                    var that = (PropertyProxy)GCHandle.FromIntPtr(callbackData).Target;
                    var obj = arguments[0];
                    var entity = obj.GetIndexedProperty(that.EntitySymbol);
                    if (entity.ValueType == JavaScriptValueType.Undefined)
                    {
                        entity = that.node.GetJavaScriptValue();
                    }
                    return entity;
                }
                catch (Exception e)
                {
                    return ExceptionUtil.SetJSException(e);
                }
            }
            public static JavaScriptValue PropertySetter(JavaScriptValue callee,
                [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
                ushort argumentCount,
                IntPtr callbackData)
            {
                try
                {
                    var that = (PropertyProxy)GCHandle.FromIntPtr(callbackData).Target;
                    var obj = arguments[0];
                    var value = arguments[1];
                    obj.SetIndexedProperty(that.EntitySymbol, value);
                    return value;
                }
                catch (Exception e)
                {
                    return ExceptionUtil.SetJSException(e);
                }
            }
        }
    }
}
