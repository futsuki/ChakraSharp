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
    abstract public class FunctionWrapper
    {
        abstract public Func<JavaScriptValue[], object> WrapFirst();
        abstract public string GetName();

        protected int neededArgumentsCount = 0;
        protected bool constructOnly = false;

        public static object Conv(JavaScriptValue val, Type t)
        {
            var v = JSValue.Make(val);
            var obj = v.GetObject();
            if (t.IsEnum && obj is string)
            {
                try
                {
                    // 3.5にはEnum.TryParseがない
                    obj = Enum.Parse(t, (string)obj);
                }
                catch
                {
                    obj = Activator.CreateInstance(t);
                }
            }
            else
            {
                try
                {
                    obj = Convert.ChangeType(obj, t);
                }
                catch { }
            }
            return obj;
        }

        JavaScriptValue jsvalue;
        public JavaScriptValue GetJavaScriptValue()
        {
            if (!jsvalue.IsValid)
                jsvalue = JavaScriptValue.CreateFunction(Wrap());
            return jsvalue;
        }

        virtual public JavaScriptNativeFunction Wrap()
        {
            return body;
        }

        Func<JavaScriptValue[], object> wrapdg;
        JavaScriptValue body(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            try
            {
                //Console.WriteLine("called "+GetName());
                if (constructOnly && !isConstructCall)
                {
                    return JavaScriptValue.Undefined;
                    //Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(string.Format("Construct only function: {0}", GetName()))));
                    //return JavaScriptValue.Invalid;
                }
                if (arguments.Length - 1 != neededArgumentsCount)
                {
                    Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(string.Format("Argument count is not satisfacted. needs {0}, got {1}: {2}", neededArgumentsCount, arguments.Length - 1, GetName()))));
                    return JavaScriptValue.Invalid;
                }
                if (wrapdg == null)
                    wrapdg = WrapFirst();
                var obj = wrapdg(arguments);
                var v = JSValue.Make(obj).rawvalue;
                if (isConstructCall)
                {
                    var pt = callee.GetIndexedProperty(JavaScriptValue.FromString("prototype"));
                    if (pt.IsValid)
                    {
                        v.Prototype = pt;
                    }
                }
                return v;
            }
            catch (Exception e)
            {
                Native.JsSetException(JavaScriptValue.CreateError(JavaScriptValue.FromString(e.ToString())));
                return JavaScriptValue.Invalid;
            }
        }

        protected static MethodInfo convertMethod;
        protected static Expression ConverterExpression(Expression parameters, int paramsIndex, Type convTo)
        {
            if (convertMethod == null)
                convertMethod = typeof(FunctionWrapper).GetMethod("Conv");
            return Expression.ConvertChecked(
                Expression.Call(null, convertMethod, new Expression[] {
                        Expression.ArrayIndex(parameters, Expression.Constant(paramsIndex)), // skip 'this' parameter
                        Expression.Constant(convTo)
                }),
                convTo);
        }
    }


    public class ConstructorWrapper : FunctionWrapper
    {
        ConstructorInfo ci;
        ParameterInfo[] ps;

        override public string GetName()
        {
            return ci.DeclaringType.Name + ".ctor";
        }

        public ConstructorWrapper(ConstructorInfo ci)
        {
            this.ci = ci;
            ps = ci.GetParameters();
            neededArgumentsCount = ps.Length;
            constructOnly = true;
        }

        override public Func<JavaScriptValue[], object> WrapFirst()
        {
            var parameters = Expression.Parameter(typeof(JavaScriptValue[]), "parameters");
            var converted = ps.Select((e, i) => ConverterExpression(parameters, i + 1, e.ParameterType)).ToArray();
            var lam = Expression.Lambda(typeof(Func<JavaScriptValue[], object>),
                Expression.New(ci, converted),
                new[] { parameters });
            var dg = lam.Compile();
            return (Func<JavaScriptValue[], object>)dg;
        }
    }


    public class StaticMethodWrapper : FunctionWrapper
    {
        MethodInfo mi;
        ParameterInfo[] ps;

        override public string GetName()
        {
            return mi.DeclaringType.Name + "." + mi.Name;
        }

        public StaticMethodWrapper(MethodInfo mi)
        {
            if (!mi.IsStatic) throw new ChakraSharpException("static only");
            this.mi = mi;
            ps = mi.GetParameters();
            neededArgumentsCount = ps.Length;
        }

        override public Func<JavaScriptValue[], object> WrapFirst()
        {
            var parameters = Expression.Parameter(typeof(JavaScriptValue[]), "parameters");
            var converted = ps.Select((e, i) => ConverterExpression(parameters, i + 1, e.ParameterType)).ToArray();
            var lam = Expression.Lambda(typeof(Func<JavaScriptValue[], object>),
                Expression.ConvertChecked(Expression.Call(null, mi, converted), typeof(object)),
                new[] { parameters });
            var dg = lam.Compile();
            return (Func<JavaScriptValue[], object>)dg;
        }
    }


    public class DelegateWrapper : FunctionWrapper
    {
        Delegate dgvalue;
        MethodInfo dgmi;
        ParameterInfo[] ps;

        override public string GetName()
        {
            return dgmi.DeclaringType.Name + "." + dgmi.Name;
        }

        public DelegateWrapper(Delegate dg)
        {
            dgvalue = dg;
            dgmi = dg.GetType().GetMethod("Invoke");
            ps = dgmi.GetParameters();
            neededArgumentsCount = ps.Length;
        }

        override public Func<JavaScriptValue[], object> WrapFirst()
        {
            if (dgmi.ReturnType == typeof(void))
            {
                // Action は Expression Tree で 呼ぶことができない
                Func<JavaScriptValue[], object> ret = (values) => {
                    dgvalue.DynamicInvoke(values.Skip(1).Select((e, i) => Conv(e, ps[i].ParameterType)).ToArray());
                    return null;
                };
                return ret;
            }
            else
            {
                var parameters = Expression.Parameter(typeof(JavaScriptValue[]), "parameters");
                var converted = ps.Select((e, i) => ConverterExpression(parameters, i + 1, e.ParameterType)).ToArray();
                var lam = Expression.Lambda(typeof(Func<JavaScriptValue[], object>),
                    Expression.ConvertChecked(Expression.Invoke(Expression.Constant(dgvalue), converted), typeof(object)),
                    new[] { parameters });
                var dg = (Func<JavaScriptValue[], object>)lam.Compile();
                return dg;
            }
        }
    }


    public class InstanceMethodWrapper : FunctionWrapper
    {
        MethodInfo mi;
        ParameterInfo[] ps;

        override public string GetName()
        {
            return mi.DeclaringType.Name + "." + mi.Name;
        }

        public InstanceMethodWrapper(MethodInfo mi)
        {
            if (mi.IsStatic) throw new ChakraSharpException("static only");
            this.mi = mi;
            ps = mi.GetParameters();
            neededArgumentsCount = ps.Length;
        }

        override public Func<JavaScriptValue[], object> WrapFirst()
        {
            var parameters = Expression.Parameter(typeof(JavaScriptValue[]), "parameters");
            var thisval = ConverterExpression(parameters, 0, mi.DeclaringType);
            var converted = ps.Select((e, i) => ConverterExpression(parameters, i + 1, e.ParameterType)).ToArray();
            var lam = Expression.Lambda(typeof(Func<JavaScriptValue[], object>),
                Expression.ConvertChecked(Expression.Call(thisval, mi, converted), typeof(object)),
                new[] { parameters });
            var dg = lam.Compile();
            return (Func<JavaScriptValue[], object>)dg;
        }
    }
}
