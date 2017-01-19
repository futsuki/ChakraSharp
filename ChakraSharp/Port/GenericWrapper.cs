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
    public class GenericWrapper
    {
        static Dictionary<Type, GenericWrapper> cache = new Dictionary<Type, GenericWrapper>();
        public static void ClearCache()
        {
            cache = new Dictionary<Type, GenericWrapper>();
        }

        public static GenericWrapper Wrap(Type type)
        {
            if (cache.ContainsKey(type))
                return cache[type];
            var tw = new GenericWrapper(type);
            cache[type] = tw;
            return tw;
        }

        public JavaScriptValue GetJavaScriptValue()
        {
            return mainValue;
        }

        Type type;
        public JavaScriptValue mainValue;
        JavaScriptNativeFunction mainValueSrc;
        GCHandle thisPtr;
        GenericWrapper(Type type)
        {
            this.type = type;
            if (!type.IsGenericTypeDefinition)
                throw new ChakraSharpException("Generic definition only");
            thisPtr = GCHandle.Alloc(this);
            mainValueSrc = body;
            mainValue = JavaScriptValue.CreateFunction(mainValueSrc, GCHandle.ToIntPtr(thisPtr));
        }

        public static JavaScriptValue body(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            try
            {
                var that = (GenericWrapper)GCHandle.FromIntPtr(callbackData).Target;
                if (arguments.Length != that.type.GetGenericArguments().Length + 1)
                    throw new ChakraSharpException("Generic argument is not satisfied: " + that.type);
                var types = arguments.Skip(1).Select(e => JSValue.Make(e).GetObject() as Type).ToArray();
                if (types.Any(e => e == null))
                    throw new ChakraSharpException("Generic argument is invalid: " + that.type);
                var it = that.type.MakeGenericType(types);
                var itv = Util.WrapType(it);
                return itv;
            }
            catch (Exception e)
            {
                return ExceptionUtil.SetJSException(e);
            }
        }
    }
}
