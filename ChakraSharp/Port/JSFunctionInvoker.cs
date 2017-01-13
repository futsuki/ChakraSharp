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
        public JavaScriptValue val;
        public Type outType;
        object body(object[] o)
        {

            JavaScriptValue res;
            var err = Native.JsCallFunction(val, o.Select(e => JSValue.FromObject(e).rawvalue).ToArray(), (ushort)o.Length, out res);
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
            var obj = objval.GetObject();
            try
            {
                if (outType == typeof(void))
                {
                    return null;
                }
                else if (outType == typeof(JSValue))
                {
                    return JSValue.FromObject(obj);
                }
                else
                {
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
            fw.val = rawvalue;
            fw.outType = mi.ReturnType;
            var body = (Func<object[], object>)fw.body;
            Expression[] parr = new Expression[ps.Length + 1];
            parr[0] = Expression.ConvertChecked(Expression.Constant(global), typeof(object));
            for (int i = 0; i < ps.Length; i++)
            {
                parr[i + 1] = Expression.ConvertChecked(psnames[i], typeof(object));
            }
            var array = Expression.NewArrayInit(typeof(object), parr);
            var ffv = Expression.Constant(fw);
            var c1 = Expression.Call(ffv, body.Method, new Expression[] { array });
            var rt = Expression.Constant(mi.ReturnType);
            var c3 = Expression.ConvertChecked(c1, mi.ReturnType);
            var f2 = Expression.Lambda(type, c3, psnames);
            var dg2 = f2.Compile();
            return dg2;
        }


    }
}
