using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using ChakraHost.Hosting;
using System.Linq.Expressions;
using System.Reflection;


namespace ChakraSharp
{
    public class Controller: IDisposable
    {
        public JavaScriptRuntime runtime;
        public JavaScriptContext context;
        public JavaScriptSourceContext currentSourceContext;

        public Controller()
        {
            Start();
        }

        public void CollectGarbage()
        {
            runtime.CollectGarbage();
        }

        public void Execute(string js)
        {
            Evaluate(js);
        }
        static void ThrowError(JavaScriptErrorCode err, string location)
        {
            var sb = new System.Text.StringBuilder();
            JavaScriptValue ex;
            bool hasEx;
            Native.ThrowIfError(Native.JsHasException(out hasEx));
            object obj=null;
            if (hasEx)
            {
                Native.ThrowIfError(Native.JsGetAndClearException(out ex));
                IntPtr p = IntPtr.Zero;
                Native.JsGetExternalData(ex, out p);
                if (p != IntPtr.Zero)
                {
                    obj = GCHandle.FromIntPtr(p).Target;
                }
                if (err == JavaScriptErrorCode.ScriptCompile)
                {
                    var message = ex.GetIndexedProperty(JavaScriptValue.FromString("message")).ConvertToString().ToString();
                    var line = ex.GetIndexedProperty(JavaScriptValue.FromString("line")).ConvertToString().ToString();
                    var column = ex.GetIndexedProperty(JavaScriptValue.FromString("column")).ConvertToString().ToString();
                    sb.AppendFormat("{0}\n   at code ({3}:{1}:{2})", message, line, column, location);
                }
                /*
                else if (err == JavaScriptErrorCode.ScriptException)
                {
                    var message = ex.GetIndexedProperty(JavaScriptValue.FromString("message")).ConvertToString().ToString();
                    var stack = ex.GetIndexedProperty(JavaScriptValue.FromString("stack")).ConvertToString().ToString();
                    sb.AppendFormat("{0}\n{1}", message, stack);
                }
                */
                else
                {
                    var errorobj = ex.GetIndexedProperty(JavaScriptValue.FromString("message"));
                    p = IntPtr.Zero;
                    Native.JsGetExternalData(errorobj, out p);
                    if (p != IntPtr.Zero)
                    {
                        obj = GCHandle.FromIntPtr(p).Target;
                    }
                    sb.Append(System.Convert.ToString(obj));
                    //sb.Append(ex.ConvertToString().ToString());
                }
            }
            else
            {
                sb.Append(err);
            }
            throw new ChakraSharpException(sb.ToString(), obj as Exception);
            //return sb.ToString();
        }
        public JSValue Evaluate(string js)
        {
            return Evaluate(js, "Evaluate");
        }
        public JSValue Evaluate(string js, string sourceName)
        {
            JavaScriptValue result;

            var err = Native.JsRunScript(js, currentSourceContext++, sourceName, out result);
            if (err == JavaScriptErrorCode.ScriptException ||
                err == JavaScriptErrorCode.ScriptCompile ||
                err == JavaScriptErrorCode.InExceptionState)
            {
                ThrowError(err, sourceName);
                //throw new ChakraSharpException(ErrorToString(err, sourceName));
            }
            else
            {
                Native.ThrowIfError(err);
            }
            return JSValue.Make(result);
        }

        public void Dispose()
        {
            JavaScriptContext.Current = JavaScriptContext.Invalid;
            runtime.Dispose();
            Port.Util.ClearCache();
        }


        public void Start()
        {
            Dispose();

            // Create a runtime. 
            //Native.ThrowIfError(Native.JsCreateRuntime(JavaScriptRuntimeAttributes.None, null, out runtime));
            
            Native.ThrowIfError(Native.JsCreateRuntime(JavaScriptRuntimeAttributes.DisableBackgroundWork, null, out runtime));
            Native.ThrowIfError(Native.JsEnableRuntimeExecution(runtime));
            // Create an execution context. 
            Native.ThrowIfError(Native.JsCreateContext(runtime, out context));

            // Now set the execution context as being the current one on this thread.
            Native.ThrowIfError(Native.JsSetCurrentContext(context));
            {
                JavaScriptValue v;
                Native.ThrowIfError(Native.JsGetGlobalObject(out v));
                Global = JSValue.Make(v);
            }
        }

        public JSValue Global;
        /*
        public JSValue Global
        {
            get
            {
                JavaScriptValue v;
                Native.ThrowIfError(Native.JsGetGlobalObject(out v));
                return new JSValue(v);
            }
        }
        */
    }
}
