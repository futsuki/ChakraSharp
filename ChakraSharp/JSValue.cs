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
        readonly public JavaScriptValue rawvalue;

        public JSValue(JavaScriptValue val)
        {
            rawvalue = val;
        }

        static public JSValue FromObject(object value)
        {
            var v = new JSValue(FromValue(value));
            return v;
        }
        static public JSValue Make(JavaScriptValue value)
        {
            var v = new JSValue(value);
            return v;
        }
        static public JSValue Null
        {
            get
            {
                var v = new JSValue(JavaScriptValue.Null);
                return v;
            }
        }
        static public JSValue Undefined
        {
            get
            {
                var v = new JSValue(JavaScriptValue.Undefined);
                return v;
            }
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
                    if (type == typeof(JavaScriptValue))
                    {
                        return rawvalue;
                    }
                    IntPtr ptr = IntPtr.Zero;
                    Native.JsGetExternalData(rawvalue, out ptr);
                    if (ptr != IntPtr.Zero)
                    {
                        var o = GCHandle.FromIntPtr(ptr).Target;
                        if (type.IsAssignableFrom(o.GetType()))
                        {
                            return o;
                        }
                    }
                    if (type == typeof(Type))
                    {
                        var tv = rawvalue.GetIndexedProperty(JavaScriptValue.FromString("_clrtypevalue"));
                        if (tv.ValueType != JavaScriptValueType.Undefined)
                        {
                            IntPtr p;
                            Native.JsGetExternalData(tv, out p);
                            if (p != IntPtr.Zero)
                            {
                                var h = GCHandle.FromIntPtr(tv.ExternalData).Target;
                                var t = h as Type;
                                if (t != null)
                                {
                                    return t;
                                }
                            }
                        }
                    }
                    if (typeof(Delegate).IsAssignableFrom(type))
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
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var len = rawvalue.GetIndexedProperty(JavaScriptValue.FromString("length"));
                        if (len.ValueType == JavaScriptValueType.Number)
                        {
                            System.Collections.IList lst = (System.Collections.IList)Activator.CreateInstance(type);
                            var leni = (int)Math.Floor(len.ToDouble());
                            var elemtype = type.GetGenericArguments()[0];
                            for (var i = 0; i < leni; i++)
                            {
                                var elem = rawvalue.GetIndexedProperty(JavaScriptValue.FromInt32(i));
                                lst.Add(JSValue.Make(elem).ConvertTo(elemtype));
                            }
                            return lst;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        var keytype = type.GetGenericArguments()[0];
                        if (keytype == typeof(string))
                        {
                            System.Collections.IDictionary dic = (System.Collections.IDictionary)Activator.CreateInstance(type);
                            var elemtype = type.GetGenericArguments()[1];
                            var arr = rawvalue.GetOwnPropertyNames();
                            var len = (int)arr.GetIndexedProperty(JavaScriptValue.FromString("length")).ToDouble();
                            for (var i = 0; i < len; i++)
                            {
                                var keyjs = arr.GetIndexedProperty(JavaScriptValue.FromInt32(i));
                                var key = keyjs.ToString();
                                var valjs = rawvalue.GetIndexedProperty(keyjs);
                                dic.Add(key, JSValue.Make(valjs).ConvertTo(elemtype));
                            }
                            return dic;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    //else
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
                        var h = (GCHandle)rawvalue.ExternalData;
                        return h.Target;
                    }
                    else
                    {
                        return this;
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
                case JavaScriptValueType.Null:
                    return null;
                case JavaScriptValueType.Number:
                    return rawvalue.ToDouble();
                case JavaScriptValueType.Function:
                case JavaScriptValueType.Object:
                    IntPtr data;
                    bool haveEx = false;
                    var err = Native.JsGetExternalData(rawvalue, out data);
                    haveEx = data != IntPtr.Zero;
                    if (err != JavaScriptErrorCode.InvalidArgument)
                    {
                        Native.ThrowIfError(err);
                    }
                    if (haveEx)
                    {
                        var h = GCHandle.FromIntPtr(data);
                        return h.Target;
                    }

                    var tv = rawvalue.GetIndexedProperty(JavaScriptValue.FromString("_clrtypevalue"));
                    if (tv.ValueType != JavaScriptValueType.Undefined)
                    {
                        IntPtr p;
                        Native.JsGetExternalData(tv, out p);
                        if (p != IntPtr.Zero)
                        {
                            var h = GCHandle.FromIntPtr(tv.ExternalData).Target;
                            var t = h as Type;
                            if (t != null)
                            {
                                return t;
                            }
                        }
                    }
                    return this;
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
        static JavaScriptValue FromValue(object o)
        {
            if (o == null)
            {
                return JavaScriptValue.Null;
            }
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Boolean:
                    return JavaScriptValue.FromBoolean((bool)o);

                case TypeCode.Char:
                    return JavaScriptValue.FromString(new string((char)o, 1));

                case TypeCode.DateTime:
                    return JavaScriptValue.Null;

                case TypeCode.DBNull:
                case TypeCode.Empty:
                    return JavaScriptValue.Null;

                case TypeCode.Single:
                    return JavaScriptValue.FromDouble((double)(float)o);
                case TypeCode.Double:
                    return JavaScriptValue.FromDouble((double)o);
                case TypeCode.SByte:
                    return JavaScriptValue.FromInt32((int)(sbyte)o);
                case TypeCode.Int16:
                    return JavaScriptValue.FromInt32((int)(Int16)o);
                case TypeCode.Int32:
                    return JavaScriptValue.FromInt32((int)(Int32)o);
                case TypeCode.Int64:
                    return JavaScriptValue.FromDouble((double)(Int64)o);
                case TypeCode.Byte:
                    return JavaScriptValue.FromInt32((int)(byte)o);
                case TypeCode.UInt16:
                    return JavaScriptValue.FromInt32((int)(UInt16)o);
                case TypeCode.UInt32:
                    return JavaScriptValue.FromDouble((double)(UInt32)o);
                case TypeCode.UInt64:
                    return JavaScriptValue.FromDouble((double)(UInt64)o);
                case TypeCode.Decimal:
                    return JavaScriptValue.FromDouble((double)(decimal)o);

                case TypeCode.String:
                    return JavaScriptValue.FromString((string)o);

                case TypeCode.Object:
                    if (o is JavaScriptNativeFunction)
                    {
                        var dg = (JavaScriptNativeFunction)o;
                        GCHandle.Alloc(dg);
                        return JavaScriptValue.CreateFunction(dg);
                    }
                    else if (o is Delegate)
                    {
                        return Port.Util.WrapDelegate((Delegate)o);
                    }
                    else if (o is JavaScriptValue)
                    {
                        return (JavaScriptValue)o;
                    }
                    else if (o is JSValue)
                    {
                        return ((JSValue)o).rawvalue;
                    }
                    else if (o is Type)
                    {
                        var wrapped = Port.TypeWrapper.Wrap((Type)o);
                        return wrapped.constructorValue;
                    }
                    else
                    {
                        var wrapped = Port.TypeWrapper.Wrap(o.GetType());
                        var rawvalue = JavaScriptValue.CreateExternalObject(GCHandle.ToIntPtr(GCHandle.Alloc(o)), HandleFinalizeDg);
                        rawvalue.Prototype = wrapped.prototypeValue;
                        return rawvalue;
                    }
                default:
                    return JavaScriptValue.Undefined;
            }
        }
        static JavaScriptObjectFinalizeCallback HandleFinalizeDg = HandleFinalize;
        static void HandleFinalize(IntPtr ptr)
        {
            try
            {
                //var handle = GCHandle.FromIntPtr(ptr);
                //handle.Free();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
        public JSValue Call(params JavaScriptValue[] contextAndArguments)
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsCallFunction(this.rawvalue, contextAndArguments, (ushort)contextAndArguments.Length, out ret));
            return JSValue.Make(ret);
        }
        public JavaScriptValue CallFast(params JavaScriptValue[] contextAndArguments)
        {
            JavaScriptValue ret;
            Native.ThrowIfError(Native.JsCallFunction(this.rawvalue, contextAndArguments, (ushort)contextAndArguments.Length, out ret));
            return ret;
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


        public uint AddRef()
        {
            return rawvalue.AddRef();
        } 
    }
}
