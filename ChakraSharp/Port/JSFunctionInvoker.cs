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
    public class JSFunctionInvoker
    {
        static List<Delegate> allValues = new List<Delegate>();
        public JavaScriptValue value;
        public Type outType;
        void actionBody(object[] o)
        {
            body(o);
        }
        object body(object[] o)
        {

            JavaScriptValue res;
            var err = Native.JsCallFunction(value, o.Select(e => JSValue.FromObject(e).rawvalue).ToArray(), (ushort)o.Length, out res);
            if (err == JavaScriptErrorCode.ScriptException)
            {
                JavaScriptValue ex;
                Native.ThrowIfError(Native.JsGetAndClearException(out ex));
                throw new JavaScriptException(JavaScriptErrorCode.ScriptException, ex.ConvertToString().ToString());
            }
            else
            {
                Native.ThrowIfError(err);
            }

            bool b;
            Native.ThrowIfError(Native.JsHasException(out b));

            var objval = JSValue.Make(res);
            try
            {
                if (outType == typeof(void))
                {
                    return null;
                }
                else if (outType == typeof(JSValue))
                {
                    var obj = objval.GetObject();
                    return JSValue.FromObject(obj);
                }
                else if (outType == typeof(JavaScriptValue))
                {
                    return objval.rawvalue;
                }
                else
                {
                    var obj = objval.GetObject();
                    var obj2 = Convert.ChangeType(obj, outType);
                    return obj2;
                }
            }
            catch
            {
                if (outType.IsValueType)
                {
                    return Activator.CreateInstance(outType);
                }
                else
                {
                    return null;
                }
            }
        }

        static public Delegate Wrap(JavaScriptValue rawvalue, Type type)
        {
            JavaScriptValue global;
            Native.ThrowIfError(Native.JsGetGlobalObject(out global));

            var mi = type.GetMethod("Invoke");
            var ps = mi.GetParameters();
            var psnames = ps.Select(e => Expression.Parameter(e.ParameterType, e.Name)).ToArray();
            var fw = new JSFunctionInvoker();
            fw.value = rawvalue;
            fw.value.AddRef();
            fw.outType = mi.ReturnType;
            //var body = (Func<object[], object>)fw.body;
            var body = (Func<object[], object>)fw.body;
            var actionBody = (Action<object[]>)fw.actionBody;
            Expression[] parr = new Expression[ps.Length + 1];
            parr[0] = Expression.ConvertChecked(Expression.Constant(global), typeof(object));
            for (int i = 0; i < ps.Length; i++)
            {
                parr[i + 1] = Expression.ConvertChecked(psnames[i], typeof(object));
            }
            var array = Expression.NewArrayInit(typeof(object), parr);
            var ffv = Expression.Constant(fw);
            Expression call;
            if (mi.ReturnType == typeof(void))
            {
                var c1 = Expression.Call(ffv, actionBody.Method, new Expression[] { array });
                call = c1;
            }
            else
            {
                var c1 = Expression.Call(ffv, body.Method, new Expression[] { array });
                var c3 = Expression.ConvertChecked(c1, mi.ReturnType);
                call = c3;
            }
            var f2 = Expression.Lambda(type, call, psnames);
            var dg2 = f2.Compile();
            allValues.Add(dg2);
            return dg2;
        }


    }
}
