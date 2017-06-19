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
            public ParameterData[] ps;
            public struct ParameterData
            {
                public string name;
                public Type parameterType;
                public bool isOut;
                public bool isParams;
            }
            public FunctionWrapper entityWrapper;
            public JavaScriptNativeFunction cachedFunction;
            public GCHandle cachedData;

            public OverloadEntry(MethodBase mi)
            {
                IEnumerable<ParameterData> ie;
                if (mi.IsStatic || mi is ConstructorInfo)
                {
                    ie = new ParameterData[] { new ParameterData() { name="(static this)" } };
                }
                else
                {
                    ie = new ParameterData[] {
                            new ParameterData {
                                name = "this",
                                parameterType = mi.DeclaringType
                            }
                        };
                }
                ie = ie.Concat(mi.GetParameters().Select(e =>
                {
                    var d = new ParameterData();
                    d.name = e.Name;
                    d.parameterType = e.ParameterType;
                    d.isOut = e.IsOut;
                    d.isParams = e.IsDefined(typeof(ParamArrayAttribute), false);
                    return d;
                }));
                ps = ie.ToArray();

                cachedFunction = null;
                entityWrapper = null;
                cachedData = default(GCHandle);
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
                else if (arguments.Length > ps.Length)
                    return DENIED;
                int score = 0;
                for (int i = 0; i < arguments.Length; i++)
                {
                    var se = CalcScore(ps[i].parameterType, arguments[i]);
                    score += se;
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
            static JavaScriptPropertyId clrtypevalueId;
            static int CalcScore(Type pit, JavaScriptValue val)
            {
                var min = DENIED;
                if (pit == null) // any type
                    min = ALLOWED;
                else if (pit == typeof(JSValue))
                    min = ALLOWED;
                else if (pit == typeof(JavaScriptValue))
                    min = ALLOWED;
                else if (pit == typeof(object))
                    min = ALLOWED;

                // CLR Type Matching
                if (clrtypevalueId == JavaScriptPropertyId.Invalid)
                {
                    clrtypevalueId = JavaScriptPropertyId.FromString("_clrtypevalue");
                }
                if (val.ValueType == JavaScriptValueType.Object ||
                    val.ValueType == JavaScriptValueType.Function) {
                    var tv = val.GetProperty(clrtypevalueId);
                    if (tv.ValueType != JavaScriptValueType.Undefined)
                    {
                        IntPtr p;
                        Native.JsGetExternalData(tv, out p);
                        if (p != IntPtr.Zero)
                        {
                            var obj = GCHandle.FromIntPtr(tv.ExternalData).Target;
                            var objt = obj.GetType();
                            if (pit == objt)
                            {
                                min = Math.Min(min, MATCHED);
                            }
                            else if (pit != null && pit.IsAssignableFrom(objt))
                            {
                                min = Math.Min(min, ALLOWED);
                            }
                        }
                    }
                }

                switch (val.ValueType)
                {
                    case JavaScriptValueType.Boolean:
                        if (pit == typeof(bool))
                            min = Math.Min(min, MATCHED);
                        break;

                    case JavaScriptValueType.Function:
                        if (typeof(Delegate).IsAssignableFrom(pit))
                            min = Math.Min(min, ALLOWED);
                        break;

                    case JavaScriptValueType.Null:
                        if (!pit.IsValueType)
                            min = Math.Min(min, ALLOWED);
                        break;

                    case JavaScriptValueType.Number:
                        if (IsNumericType(pit))
                        {
                            if (pit == typeof(double))
                                min = Math.Min(min, MATCHED);
                            else if (pit.IsEnum)
                                min = Math.Min(min, LOWER);
                            else
                                min = Math.Min(min, ALLOWED);
                        }
                        break;

                    case JavaScriptValueType.String:
                        if (pit == typeof(string))
                            min = Math.Min(min, MATCHED);
                        else if (pit.IsEnum)
                            min = Math.Min(min, LOWER);
                        break;

                    case JavaScriptValueType.Object:
                        if (val.HasExternalData)
                        {
                            var obj = GCHandle.FromIntPtr(val.ExternalData).Target;
                            var objt = obj.GetType();
                            if (pit == objt)
                            {
                                min = Math.Min(min, MATCHED);
                            }
                            else if (pit.IsAssignableFrom(objt))
                            {
                                min = Math.Min(min, ALLOWED);
                            }
                        }
                        break;

                    case JavaScriptValueType.Undefined:
                    case JavaScriptValueType.Array:
                    case JavaScriptValueType.ArrayBuffer:
                    case JavaScriptValueType.DataView:
                    case JavaScriptValueType.Error:
                    case JavaScriptValueType.Symbol:
                    case JavaScriptValueType.TypedArray:
                    default:
                        break;
                }
                return min;
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

        static string OverloadToString(string name, OverloadEntry oe)
        {
            var sb = new StringBuilder();
            sb.Append($"{name}(");
            bool first = true;
            foreach (var p in oe.ps)
            {
                sb.Append($"{p.parameterType} {p.name}");
                if (first)
                {
                    first = false;
                    sb.Append(", ");
                }
            }
            sb.Append($")");
            return sb.ToString();
        }
        static string CallToString(string name, JavaScriptValue[] arguments)
        {
            var sb = new StringBuilder();
            sb.Append($"{name}(");
            bool first = true;
            foreach (var arg in arguments)
            {
                sb.Append(arg.ConvertToString().ToString());
                if (first)
                {
                    first = false;
                    sb.Append(", ");
                }
            }
            sb.Append($")");
            return sb.ToString();
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
                var lowestScore = OverloadEntry.DENIED - 1; // block denied
                for (int i = 0; i < that.methodInfos.Count; i++)
                {
                    var mi = that.methodInfos[i];
                    var score = mi.GetArgumentsScore(arguments);
                    if (lowestScore > score)
                    {
                        lowestScore = score;
                        lastIdx = i;
                    }
                    else if (lowestScore == score)
                    {
                        lastIdx = -1; // deny ambiguous overload
                    }
                }
                if (lastIdx == -1)
                {
                    Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString("ambiguous overload "+that.GetName())));
                    return JavaScriptValue.Invalid;
                }
                var method = that.methodInfos[lastIdx];
                if (method.cachedFunction == null)
                    method.cachedFunction = method.entityWrapper.Wrap();
                if (!method.cachedData.IsAllocated)
                    method.cachedData = GCHandle.Alloc(method.entityWrapper);
                return method.cachedFunction(callee, isConstructCall, arguments, argumentCount, GCHandle.ToIntPtr(method.cachedData));
            }
            catch (Exception e)
            {
                return ExceptionUtil.SetJSException(e);
            }
        }


        public override Func<JavaScriptValue[], object> WrapFirst()
        {
            throw new NotImplementedException();
        }

        public override string GetName()
        {
            return firstName;
        }
    }
}
