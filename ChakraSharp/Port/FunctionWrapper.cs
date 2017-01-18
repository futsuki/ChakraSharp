using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using ChakraHost.Hosting;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace ChakraSharp.Port
{
    abstract public class FunctionWrapper : IDisposable
    {
        abstract public Func<JavaScriptValue[], object> WrapFirst();
        abstract public string GetName();

        protected int neededArgumentsCount = 0;
        protected bool constructOnly = false;

        public static object Conv(JavaScriptValue val, Type t)
        {
            object obj = null;
            if (t.IsEnum && val.ValueType == JavaScriptValueType.String)
            {
                try
                {
                    // 3.5にはEnum.TryParseがない
                    obj = Enum.Parse(t, val.ToString());
                }
                catch
                {
                    obj = Activator.CreateInstance(t);
                }
            }
            else
            {
                var v = JSValue.Make(val);
                obj = v.ConvertTo(t);
            }
            return obj;
        }

        JavaScriptValue jsvalue;
        GCHandle thisPtr;
        public JavaScriptValue GetJavaScriptValue()
        {
            if (!thisPtr.IsAllocated)
            {
                thisPtr = GCHandle.Alloc(this);
            }
            if (!jsvalue.IsValid)
            {
                jsvalue = JavaScriptValue.CreateFunction(Wrap(), GCHandle.ToIntPtr(thisPtr));
            }
            return jsvalue;
        }
        public void Dispose()
        {
            if (thisPtr.IsAllocated) {
                thisPtr.Free();
            }
        }

        virtual public JavaScriptNativeFunction Wrap()
        {
            return body;
        }

        Func<JavaScriptValue[], object> wrapdg;
        static JavaScriptValue body(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            try
            {
                var that = (FunctionWrapper)GCHandle.FromIntPtr(callbackData).Target;
                if (that.constructOnly && !isConstructCall)
                {
                    return JavaScriptValue.Undefined;
                }
                if (arguments.Length - 1 != that.neededArgumentsCount)
                {
                    Native.JsSetException(
                        JavaScriptValue.CreateError(
                            JavaScriptValue.FromString(
                                string.Format("Argument count is not satisfacted. needs {0}, got {1}: {2}",
                                that.neededArgumentsCount, arguments.Length - 1, that.GetName()))));
                    return JavaScriptValue.Invalid;
                }
                if (that.wrapdg == null)
                    that.wrapdg = that.WrapFirst();
                var obj = that.wrapdg(arguments);
                var v = JSValue.FromObject(obj).rawvalue;
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
                return ExceptionUtil.SetJSException(e);
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


        public static Delegate ActionToFunc(Delegate dg)
        {
            if (dg == null) throw new ArgumentNullException();
            var mi = dg.Method;
            if (mi.ReturnType != typeof(void))
            {
                throw new ChakraSharpException("Action only");
            }
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("MyAssembly"), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("MyModule", true);
            var name = mi.Name + "_ActionToFunc";
            var tb = module.DefineType(name, TypeAttributes.Class | TypeAttributes.Public);
            var ptypes = mi.GetParameters().Select(e => e.ParameterType).ToArray();
            var ptypes_ret = ptypes.Concat(new[] { typeof(object) }).ToArray();
            var method = tb.DefineMethod("body", MethodAttributes.Public, typeof(object), ptypes);
            var defparam = mi.GetParameters().Select((e, i) =>
            {
                return method.DefineParameter(i + 1, ParameterAttributes.In, e.Name);
            }).ToArray();
            var dgfield = tb.DefineField("dg", dg.GetType(), FieldAttributes.Public | FieldAttributes.Family);
            var invokeMethod = dg.GetType().GetMethod("Invoke");
            var generator = method.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, dgfield);
            for (var i = 0; i < ptypes.Length; i++)
            {
                generator.Emit(OpCodes.Ldarg_S, i + 1);
            }
            generator.Emit(OpCodes.Call, invokeMethod);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ret);

            var tbt = tb.CreateType();
            var ins = Activator.CreateInstance(tbt);
            var dgf = tbt.GetField("dg");
            dgf.SetValue(ins, dg);

            var methodi = tbt.GetMethod("body");
            var retdg = Delegate.CreateDelegate(Expression.GetFuncType(ptypes_ret), ins, methodi);
            return retdg;
        }
        public static Delegate ActionToFunc(MethodInfo mi)
        {
            if (mi == null) throw new ArgumentNullException();
            if (mi.ReturnType != typeof(void))
            {
                throw new ChakraSharpException("Action only");
            }
            var name = mi.Name + "_ActionToFunc";
            var ptypes = mi.GetParameters().Select(e => e.ParameterType).ToArray();
            ptypes = mi.IsStatic ? ptypes : new[] { mi.DeclaringType }.Concat(ptypes).ToArray();
            var ptypes_ret = ptypes.Concat(new[] { typeof(object) }).ToArray();
            var method = new DynamicMethod(name, typeof(object), ptypes, true);
            var defparam = mi.GetParameters().Select((e, i) =>
            {
                return method.DefineParameter(i + 1, ParameterAttributes.In, e.Name);
            }).ToArray();
            var generator = method.GetILGenerator();
            if (mi.IsStatic)
            {
                for (var i = 0; i < ptypes.Length; i++)
                {
                    generator.Emit(OpCodes.Ldarg_S, i);
                }
            }
            else
            {
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Castclass, mi.DeclaringType);
                for (var i = 1; i < ptypes.Length; i++)
                {
                    generator.Emit(OpCodes.Ldarg_S, i);
                }
            }
            generator.Emit(OpCodes.Call, mi);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ret);

            var retdg = method.CreateDelegate(Expression.GetFuncType(ptypes_ret), null);
            return retdg;
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
            if (mi.ReturnType == typeof(void))
            {
                // Action は Expression Tree で 呼ぶことができない
                Func<JavaScriptValue[], object> ret = (values) => {
                    mi.Invoke(null, values.Skip(1).Select((e, i) => Conv(e, ps[i].ParameterType)).ToArray());
                    return null;
                };
                return ret;
            }
            else
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
            if (mi.ReturnType == typeof(void))
            {
                /*
                var wrapfun = ActionToFunc(mi);
                var wrapfunexpr = Expression.Constant(wrapfun);
                var parameters = Expression.Parameter(typeof(JavaScriptValue[]), "parameters");
                var thisval = ConverterExpression(parameters, 0, mi.DeclaringType);
                var converted = ps.Select((e, i) => ConverterExpression(parameters, i + 1, e.ParameterType)).ToArray();
                if (!mi.IsStatic)
                {
                    converted = new [] { thisval }.Concat(converted).ToArray();
                }
                var lam = Expression.Lambda(typeof(Func<JavaScriptValue[], object>),
                    Expression.Invoke(wrapfunexpr, converted),
                    new[] { parameters });
                var dg = lam.Compile();
                return (Func<JavaScriptValue[], object>)dg;
                */
                // Action は Expression Tree で 呼ぶことができない
                Func<JavaScriptValue[], object> ret = (values) =>
                {
                    var t = Conv(values[0], mi.DeclaringType);
                    mi.Invoke(t, values.Skip(1).Select((e, i) => Conv(e, ps[i].ParameterType)).ToArray());
                    return null;
                };
                return ret;
            }
            else
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
}
