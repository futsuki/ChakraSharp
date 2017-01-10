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
    public class OverloadSelector : FunctionWrapper
    {
        public struct OverloadEntry
        {
            //public MethodBase methodInfo;
            public ParameterData[] ps;
            public struct ParameterData
            {
                public Type parameterType;
                public bool isOut;
                public bool isParams;
            }
            public FunctionWrapper entityWrapper;
            public JavaScriptNativeFunction cachedFunction;
            public IntPtr cachedData;

            public OverloadEntry(MethodBase mi)
            {
                IEnumerable<ParameterData> ie;
                if (mi.IsStatic || mi is ConstructorInfo)
                {
                    ie = new ParameterData[] { new ParameterData() };
                }
                else
                {
                    ie = new ParameterData[] {
                            new ParameterData {
                                parameterType = mi.DeclaringType
                            }
                        };
                }
                ie = ie.Concat(mi.GetParameters().Select(e =>
                {
                    var d = new ParameterData();
                    d.parameterType = e.ParameterType;
                    d.isOut = e.IsOut;
                    d.isParams = e.IsDefined(typeof(ParamArrayAttribute), false);
                    return d;
                }));
                ps = ie.ToArray();

                cachedFunction = null;
                entityWrapper = null;
                cachedData = IntPtr.Zero;
                if (mi is ConstructorInfo)
                {
                    entityWrapper = new ConstructorWrapper((ConstructorInfo)mi);
                }
                else if (mi is MethodInfo)
                {
                    if (mi.IsStatic)
                    {
                        entityWrapper = new StaticMethodWrapper((MethodInfo)mi);
                    }
                    else
                    {
                        entityWrapper = new InstanceMethodWrapper((MethodInfo)mi);
                    }
                }
            }


            public const int DENIED = 100000;
            public const int MATCHED = 0;
            public const int ALLOWED = 1;
            public const int LOWER = 2;
            public int GetArgumentsScore(JavaScriptValue[] arguments)
            {
                // arguments[0] is 'this'
                if (arguments.Length < ps.Length)
                    return DENIED;
                //else if (arguments.Length > pis.Length && !IsParams(pis[pis.Length-1]))
                else if (arguments.Length > ps.Length)
                    return DENIED;
                int score = 0;
                for (int i = 0; i < arguments.Length; i++)
                {
                    score += CalcScore(ps[i].parameterType, arguments[i]);
                }
                return score;
            }
            static bool IsNumericType(Type t)
            {
                switch (Type.GetTypeCode(t))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Single:
                        return true;
                    default:
                        return false;
                }
            }
            static int CalcScore(Type pit, JavaScriptValue val)
            {
                if (pit == null) // any type
                    return ALLOWED;
                if (pit == typeof(JSValue))
                    return ALLOWED;
                if (pit == typeof(JavaScriptValue))
                    return ALLOWED;
                if (pit == typeof(object))
                    return ALLOWED;
                switch (val.ValueType)
                {
                    case JavaScriptValueType.Boolean:
                        if (pit != typeof(bool))
                            return DENIED;
                        else
                            return MATCHED;

                    case JavaScriptValueType.Function:
                        if (!typeof(Delegate).IsAssignableFrom(pit))
                            return DENIED;
                        else
                            return ALLOWED;

                    case JavaScriptValueType.Null:
                        if (pit.IsValueType)
                            return DENIED;
                        else
                            return ALLOWED;

                    case JavaScriptValueType.Number:
                        if (!IsNumericType(pit))
                            return DENIED;
                        if (pit == typeof(double))
                            return MATCHED;
                        else if (pit.IsEnum)
                            return LOWER;
                        else
                            return ALLOWED;

                    case JavaScriptValueType.String:
                        if (pit == typeof(string))
                            return MATCHED;
                        else if (pit.IsEnum)
                            return LOWER;
                        else
                            return DENIED;

                    case JavaScriptValueType.Object:
                        if (val.HasExternalData)
                        {
                            var obj = GCHandle.FromIntPtr(val.ExternalData).Target;
                            var objt = obj.GetType();
                            if (pit == objt)
                            {
                                return MATCHED;
                            }
                            else if (pit.IsAssignableFrom(objt))
                            {
                                return ALLOWED;
                            }
                        }
                        return DENIED;

                    case JavaScriptValueType.Undefined:
                        //if (!pit.IsValueType)
                        //return ALLOWED;
                        //else
                        return DENIED;

                    case JavaScriptValueType.Array:
                    case JavaScriptValueType.ArrayBuffer:
                    case JavaScriptValueType.DataView:
                    case JavaScriptValueType.Error:
                    case JavaScriptValueType.Symbol:
                    case JavaScriptValueType.TypedArray:
                    default:
                        return DENIED;
                }
            }
        }
        List<OverloadEntry> methodInfos = new List<OverloadEntry>();
        public bool hasConstructor { get; private set; }
        public bool hasStatic { get; private set; }
        HashSet<string> names = new HashSet<string>();
        string firstName = null;

        public void AppendMethod(MethodBase mi)
        {
            if (hasConstructor && !(mi is ConstructorInfo))
                throw new ChakraSharpException();
            if (mi is ConstructorInfo)
                hasConstructor = true;
            if (hasStatic && !mi.IsStatic)
                throw new ChakraSharpException();
            if (mi.IsStatic)
                hasStatic = true;

            var oe = new OverloadEntry(mi);
            methodInfos.Add(oe);

            names.Add(mi.Name);
            if (string.IsNullOrEmpty(firstName))
                firstName = mi.Name;
        }

        public override JavaScriptNativeFunction Wrap()
        {
            return body;
        }

        static JavaScriptValue body(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            try
            {
                var that = (OverloadSelector)GCHandle.FromIntPtr(callbackData).Target;
                var lastIdx = -1;
                var lowestScore = OverloadEntry.DENIED - 1;
                for (int i = 0; i < that.methodInfos.Count; i++)
                {
                    var mi = that.methodInfos[i];
                    var score = mi.GetArgumentsScore(arguments);
                    if (lowestScore > score)
                    {
                        //Console.WriteLine("Overload entry: " + string.Join(", ", mi.ps.Skip(1).Select(e => e.parameterType.ToString()).ToArray()));
                        lowestScore = score;
                        lastIdx = i;
                    }
                    else if (lowestScore == score)
                    {
                        //Console.WriteLine("Clear entry: " + string.Join(", ", mi.ps.Skip(1).Select(e => e.parameterType.ToString()).ToArray()));
                        lastIdx = -1; // deny ambiguous overload
                    }
                }
                if (lastIdx == -1)
                {
                    Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString("ambiguous overload")));
                    return JavaScriptValue.Invalid;
                }
                var method = that.methodInfos[lastIdx];
                //Console.WriteLine("Overload selected: " + string.Join(", ", method.ps.Skip(1).Select(e => e.parameterType.ToString()).ToArray()));
                if (method.cachedFunction == null)
                    method.cachedFunction = method.entityWrapper.Wrap();
                if (method.cachedData == IntPtr.Zero)
                    method.cachedData = GCHandle.ToIntPtr(GCHandle.Alloc(method.entityWrapper));
                return method.cachedFunction(callee, isConstructCall, arguments, argumentCount, method.cachedData);
            }
            catch (Exception e)
            {
                Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(e.ToString())));
                return JavaScriptValue.Invalid;
            }
        }


        public override Func<JavaScriptValue[], object> WrapFirst()
        {
            throw new NotImplementedException();
        }

        public override string GetName()
        {
            return firstName;
            //return string.Join("|", names.ToArray());
        }
    }
}
