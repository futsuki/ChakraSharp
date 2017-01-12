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
    public struct JSValue
    {
        public JavaScriptValue rawvalue;
        /*
        public JSValue(JavaScriptValue value)
        {
            this.rawvalue = value;
        }
        public JSValue(object value)
        {
            this.rawvalue = JavaScriptValue.Undefined;
            SetValue(value);
        }
        */

        static public JSValue FromObject(object value)
        {
            var v = new JSValue();
            v.rawvalue = JavaScriptValue.Undefined;
            v.SetValue(value);
            return v;
        }
        static public JSValue Make(JavaScriptValue value)
        {
            var v = new JSValue();
            v.rawvalue = value;
            return v;
        }

        public bool IsNull
        {
            get
            {
                return rawvalue.ValueType == JavaScriptValueType.Null;
            }
        }
        public bool IsUndefined
        {
            get
            {
                return rawvalue.ValueType == JavaScriptValueType.Undefined;
            }
        }

        public T ConvertTo<T>()
        {
            return (T)ConvertTo(typeof(T));
        }
        public object ConvertTo(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return ToBoolean();

                case TypeCode.Char:
                    return (char)ToDouble();

                case TypeCode.DateTime:
                    return default(DateTime);

                case TypeCode.DBNull:
                case TypeCode.Empty:
                    return null;

                case TypeCode.Single:
                    return (float)ToDouble();
                case TypeCode.Double:
                    return ToDouble();
                case TypeCode.SByte:
                    return (sbyte)ToDouble();
                case TypeCode.Int16:
                    return (short)ToDouble();
                case TypeCode.Int32:
                    return (int)ToDouble();
                case TypeCode.Int64:
                    return (long)ToDouble();
                case TypeCode.Byte:
                    return (byte)ToDouble();
                case TypeCode.UInt16:
                    return (ushort)ToDouble();
                case TypeCode.UInt32:
                    return (uint)ToDouble();
                case TypeCode.UInt64:
                    return (ulong)ToDouble();
                case TypeCode.Decimal:
                    return (decimal)ToDouble();

                case TypeCode.String:
                    return ToString();

                case TypeCode.Object:
                    if (type == typeof(JSValue))
                    {
                        return this;
                    }
                    else if (type == typeof(JavaScriptValue))
                    {
                        return rawvalue;
                    }
                    else if (typeof(Delegate).IsAssignableFrom(type))
                    {
                        if (rawvalue.ValueType == JavaScriptValueType.Function)
                        {
                            return Port.JSFunctionInvoker.Wrap(rawvalue, type);
                        }
                        else
                        {
                            throw new ChakraSharpException("non-function value is not convertible to delegate");
                        }
                    }
                    else
                    {
                        if (rawvalue.HasExternalData)
                        {
                            var h = (GCHandle)rawvalue.ExternalData;
                            return h.Target;
                        }
                        else
                        {
                            return this;
                        }
                    }
                default:
                    return this;
            }
        }


        public object GetObject()
        {
            switch (rawvalue.ValueType)
            {
                case JavaScriptValueType.Array:
                    return this;
                case JavaScriptValueType.ArrayBuffer:
                    return this;
                case JavaScriptValueType.Boolean:
                    return rawvalue.ToBoolean();
                case JavaScriptValueType.DataView:
                    return this;
                case JavaScriptValueType.Error:
                    return this;
                case JavaScriptValueType.Function:
                    return this;
                case JavaScriptValueType.Null:
                    return null;
                case JavaScriptValueType.Number:
                    return rawvalue.ToDouble();
                case JavaScriptValueType.Object:
                    //if (rawvalue.HasExternalData)
                    //{
                        IntPtr data;
                        bool haveEx = false;
                        var err = Native.JsGetExternalData(rawvalue, out data);
                        haveEx = err != JavaScriptErrorCode.InvalidArgument;
                        if (err != JavaScriptErrorCode.InvalidArgument)
                        {
                            Native.ThrowIfError(err);
                        }
                        if (haveEx)
                        {
                            var h = GCHandle.FromIntPtr(data);
                            return h.Target;
                        }
                        else
                        {
                            return this;
                        }
                    //}
                    //else
                    //{
                        //return this;
                    //}
                case JavaScriptValueType.String:
                    return rawvalue.ToString();
                case JavaScriptValueType.Symbol:
                    return this;
                case JavaScriptValueType.TypedArray:
                    return this;
                case JavaScriptValueType.Undefined:
                    return this;
                default:
                    return null;
            }
        }
        public void SetValue(object o)
        {
            if (o == null)
            {
                rawvalue = JavaScriptValue.Null;
                return;
            }
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Boolean:
                    rawvalue = JavaScriptValue.FromBoolean((bool)o);
                    break;

                case TypeCode.Char:
                    rawvalue = JavaScriptValue.FromString(new string((char)o, 1));
                    break;

                case TypeCode.DateTime:
                    rawvalue = JavaScriptValue.Null;
                    break;

                case TypeCode.DBNull:
                case TypeCode.Empty:
                    rawvalue = JavaScriptValue.Null;
                    break;

                case TypeCode.Single:
                    rawvalue = JavaScriptValue.FromDouble((double)(float)o);
                    break;
                case TypeCode.Double:
                    rawvalue = JavaScriptValue.FromDouble((double)o);
                    break;
                case TypeCode.SByte:
                    rawvalue = JavaScriptValue.FromInt32((int)(sbyte)o);
                    break;
                case TypeCode.Int16:
                    rawvalue = JavaScriptValue.FromInt32((int)(Int16)o);
                    break;
                case TypeCode.Int32:
                    rawvalue = JavaScriptValue.FromInt32((int)(Int32)o);
                    break;
                case TypeCode.Int64:
                    rawvalue = JavaScriptValue.FromDouble((double)(Int64)o);
                    break;
                case TypeCode.Byte:
                    rawvalue = JavaScriptValue.FromInt32((int)(byte)o);
                    break;
                case TypeCode.UInt16:
                    rawvalue = JavaScriptValue.FromInt32((int)(UInt16)o);
                    break;
                case TypeCode.UInt32:
                    rawvalue = JavaScriptValue.FromDouble((double)(UInt32)o);
                    break;
                case TypeCode.UInt64:
                    rawvalue = JavaScriptValue.FromDouble((double)(UInt64)o);
                    break;
                case TypeCode.Decimal:
                    rawvalue = JavaScriptValue.FromDouble((double)(decimal)o);
                    break;

                case TypeCode.String:
                    rawvalue = JavaScriptValue.FromString((string)o);
                    break;

                case TypeCode.Object:
                    if (o is JavaScriptNativeFunction)
                    {
                        rawvalue = JavaScriptValue.CreateFunction((JavaScriptNativeFunction)o);
                    }
                    else if (o is Delegate)
                    {
                        rawvalue = Port.Util.WrapDelegate((Delegate)o);
                    }
                    else if (o is JavaScriptValue)
                    {
                        rawvalue = (JavaScriptValue)o;
                    }
                    else if (o is JSValue)
                    {
                        rawvalue = ((JSValue)o).rawvalue;
                    }
                    else
                    {
                        var wrapped = Port.TypeWrapper.Wrap(o.GetType());
                        rawvalue = JavaScriptValue.CreateExternalObject(GCHandle.ToIntPtr(GCHandle.Alloc(o)), HandleFinalize);
                        rawvalue.Prototype = wrapped.prototypeValue;
                    }
                    break;

            }
        }
        static void HandleFinalize(IntPtr ptr)
        {
            var handle = GCHandle.FromIntPtr(ptr);
            handle.Free();
        }

        public static implicit operator JSValue(bool v)
        {
            return JSValue.FromObject(v);
        }
        public static implicit operator JSValue(int v)
        {
            return JSValue.FromObject(v);
        }
        public static implicit operator JSValue(double v)
        {
            return JSValue.FromObject(v);
        }
        public static implicit operator JSValue(string v)
        {
            return JSValue.FromObject(v);
        }
        public static implicit operator JSValue(JavaScriptNativeFunction v)
        {
            return JSValue.FromObject(v);
        }
        public static implicit operator JSValue(JavaScriptValue v)
        {
            return JSValue.Make(v);
        }
        public static implicit operator JSValue(Delegate v)
        {
            return JSValue.FromObject(v);
        }

        public JSValue ConvertBoolean()
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsConvertValueToBoolean(this.rawvalue, out ret));
            return JSValue.Make(ret);
        }
        public JSValue ConvertNumber()
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsConvertValueToNumber(this.rawvalue, out ret));
            return JSValue.Make(ret);
        }
        public JSValue ConvertString()
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsConvertValueToString(this.rawvalue, out ret));
            return JSValue.Make(ret);
        }
        public JSValue ConvertObject()
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsConvertValueToObject(this.rawvalue, out ret));
            return JSValue.Make(ret);
        }
        public JSValue Invoke(params JSValue[] arguments)
        {
            JavaScriptValue ret, that;
            JavaScriptValue[] args = new JavaScriptValue[arguments.Length+1];
            Native.ThrowIfError(Native.JsGetGlobalObject(out that));
            args[0] = that;
            for (var i = 0; i < arguments.Length; i++)
                args[i + 1] = arguments[i].rawvalue;
            Native.ThrowIfError(Native.JsCallFunction(this.rawvalue, args, (ushort)args.Length, out ret));
            return JSValue.Make(ret);
        }
        public JSValue Call(params JSValue[] contextAndArguments)
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsCallFunction(this.rawvalue, contextAndArguments.Select(e => e.rawvalue).ToArray(), (ushort)contextAndArguments.Length, out ret));
            return JSValue.Make(ret);
        }
        public JSValue New(JSValue[] contextAndArguments)
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsConstructObject(this.rawvalue, contextAndArguments.Select(e => e.rawvalue).ToArray(), (ushort)contextAndArguments.Length, out ret));
            return JSValue.Make(ret);
        }

        public override string ToString()
        {
            IntPtr resultPtr;
            UIntPtr stringLength;
            JavaScriptValue resultJSString;
            if (this.rawvalue.ValueType == JavaScriptValueType.String)
            {
                resultJSString = this.rawvalue;
            }
            else
            {
                Native.ThrowIfError(Native.JsConvertValueToString(rawvalue, out resultJSString));
            }
            Native.ThrowIfError(Native.JsStringToPointer(resultJSString, out resultPtr, out stringLength));
            return Marshal.PtrToStringUni(resultPtr, (int)stringLength);
        }
        public double ToDouble()
        {
            if (rawvalue.ValueType == JavaScriptValueType.Number)
            {
                return rawvalue.ToDouble();
            }
            else
            {
                return rawvalue.ConvertToNumber().ToDouble();
            }
        }
        public bool ToBoolean()
        {
            if (rawvalue.ValueType == JavaScriptValueType.Boolean)
            {
                return rawvalue.ToBoolean();
            }
            else
            {
                return rawvalue.ConvertToBoolean().ToBoolean();
            }
        }
        public JSValue this[string key]
        {
            get
            {
                JavaScriptValue v;
                Native.ThrowIfError(Native.JsPointerToString(key, (UIntPtr)key.Length, out v));
                return JSValue.Make(rawvalue.GetIndexedProperty(v));
            }
            set
            {
                JavaScriptValue v;
                Native.ThrowIfError(Native.JsPointerToString(key, (UIntPtr)key.Length, out v));
                rawvalue.SetIndexedProperty(v, value.rawvalue);
            }
        }
        public JSValue this[int key]
        {
            get
            {
                return JSValue.Make(rawvalue.GetIndexedProperty(JavaScriptValue.FromInt32(key)));
            }
            set
            {
                rawvalue.SetIndexedProperty(JavaScriptValue.FromInt32(key), value.rawvalue);
            }
        }
    }
}
