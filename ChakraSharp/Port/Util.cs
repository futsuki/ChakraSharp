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
    public class Util
    {
        public static JavaScriptValue WrapConstructor(ConstructorInfo ci)
        {
            var c = new ConstructorWrapper(ci);
            return c.GetJavaScriptValue();
        }

        public static JavaScriptValue WrapMethod(MethodInfo mi)
        {
            if (mi.IsStatic)
            {
                var c = new StaticMethodWrapper(mi);
                return c.GetJavaScriptValue();
            }
            else
            {
                var c = new InstanceMethodWrapper(mi);
                return c.GetJavaScriptValue();
            }
        }
        public static JavaScriptValue WrapDelegate(Delegate dg)
        {
            var c = new DelegateWrapper(dg);
            return c.GetJavaScriptValue();
        }
        public static JavaScriptValue WrapType(Type t)
        {
            var c = TypeWrapper.Wrap(t);
            return c.GetJavaScriptValue();
        }
        public static JavaScriptValue WrapNamespace(string path)
        {
            var c = NamespaceWrapper.Get(path);
            return c.GetJavaScriptValue();
        }
        public static JavaScriptValue WrapObject(object obj)
        {
            var o = JSValue.FromObject(obj);
            return o.rawvalue;
        }

        public static void ClearCache()
        {
            NamespaceWrapper.ClearCache();
            TypeWrapper.ClearCache();
        }
    }
    internal class InternalUtil
    {
        internal static JavaScriptValue GetSavedString(JavaScriptValue callee,
    [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
    [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
    ushort argumentCount,
    IntPtr callbackData)
        {
            try
            {
                return JavaScriptValue.FromString((string)GCHandle.FromIntPtr(callbackData).Target);
            }
            catch (Exception e)
            {
                return ExceptionUtil.SetJSException(e);
            }
        }
    }
}