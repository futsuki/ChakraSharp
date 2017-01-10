using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using ChakraHost.Hosting;
using System.Linq.Expressions;
using System.Reflection;

namespace ChakraSharp.Port
{
    public class TypeWrapper
    {
        static Dictionary<Type, TypeWrapper> cache = new Dictionary<Type, TypeWrapper>();
        public static void ClearCache()
        {
            cache = new Dictionary<Type, TypeWrapper>();
        }

        public static TypeWrapper Wrap(Type type)
        {
            if (cache.ContainsKey(type))
                return cache[type];
            var tw = new TypeWrapper(type);
            cache[type] = tw;
            return tw;
        }

        public JavaScriptValue GetJavaScriptValue()
        {
            return constructorValue;
        }

        Type type;
        JavaScriptValue constructorValue;
        JavaScriptValue prototypeValue;
        TypeWrapper(Type type)
        {
            this.type = type;
            var ctors = type.GetConstructors();
            if (ctors.Length == 0)
            {
                constructorValue = JavaScriptValue.CreateFunction(NoConstructor);
            }
            else if (ctors.Length == 1)
            {
                var ctorw = new ConstructorWrapper(ctors[0]);
                constructorValue = JavaScriptValue.CreateFunction(ctorw.Wrap(), GCHandle.ToIntPtr(GCHandle.Alloc(ctorw)));
            }
            else
            {
                var os = new OverloadSelector();
                foreach (var m in ctors)
                {
                    os.AppendMethod(m);
                }
                constructorValue = JavaScriptValue.CreateFunction(os.Wrap(), GCHandle.ToIntPtr(GCHandle.Alloc(os)));
            }
            prototypeValue = JavaScriptValue.CreateObject();

            // statics
            //Console.WriteLine("register static");
            AssignTableProc(constructorValue,
                type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static));
            //Console.WriteLine("register instance");
            AssignTableProc(prototypeValue,
                type.GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance));

            constructorValue.SetIndexedProperty(JavaScriptValue.FromString("prototype"), prototypeValue);
        }

        void AssignTableProc(JavaScriptValue setTo, MethodInfo[] methods)
        {
            var methodDic = new Dictionary<string, List<MethodInfo>>();
            foreach (var m in methods)
            {
                if (m.IsSpecialName)
                    continue;
                if (!methodDic.ContainsKey(m.Name))
                    methodDic[m.Name] = new List<MethodInfo>();
                methodDic[m.Name].Add(m);
            }
            foreach (var methodName in methodDic.Keys)
            {
                var ms = methodDic[methodName];
                if (ms.Count == 1)
                {
                    var m = ms[0];
                    var smw = (m.IsStatic) ? (FunctionWrapper)new StaticMethodWrapper(m) : (FunctionWrapper)new InstanceMethodWrapper(m);
                    setTo.SetIndexedProperty(JavaScriptValue.FromString(m.Name),
                        JavaScriptValue.CreateFunction(smw.Wrap(), GCHandle.ToIntPtr(GCHandle.Alloc(smw))));
                    var val = setTo.GetIndexedProperty(JavaScriptValue.FromString(m.Name));
                    //Console.WriteLine("set " + type.Name + "." + methodName);
                    //Console.WriteLine("seted " + val.ConvertToString().ToString());
                }
                else
                {
                    var os = new OverloadSelector();
                    foreach (var m in ms)
                    {
                        os.AppendMethod(m);
                    }
                    setTo.SetIndexedProperty(JavaScriptValue.FromString(os.GetName()),
                        JavaScriptValue.CreateFunction(os.Wrap(), GCHandle.ToIntPtr(GCHandle.Alloc(os))));
                    var val = setTo.GetIndexedProperty(JavaScriptValue.FromString(os.GetName()));
                    //Console.WriteLine("ol set " + type.Name + "." + methodName);
                    //Console.WriteLine("seted " + val.ConvertToString().ToString());
                    //Console.WriteLine("skip " + type.Name + "." + methodName);
                }
            }
        }

        static JavaScriptValue NoConstructor(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            try
            {
                var that = (TypeWrapper)GCHandle.FromIntPtr(callbackData).Target;
                Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(that.type.Name + " has no constructor")));
                return JavaScriptValue.Invalid;
            }
            catch (Exception e)
            {
                Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(e.ToString())));
                return JavaScriptValue.Invalid;
            }
        }
    }
}
