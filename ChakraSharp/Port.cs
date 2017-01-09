using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using ChakraHost.Hosting;
using System.Linq.Expressions;
using System.Reflection;

namespace ChakraSharp
{
    namespace Port
    {
        /*
        abstract public class FunctionTableMaker
        {
            abstract public void AssignFunctions(JavaScriptValue obj);
        }
        public class StaticMethodTableMaker : FunctionTableMaker
        {
            Type type;
            public StaticMethodTableMaker(Type type)
            {
                this.type = type;
            }

            override public void AssignFunctions(JavaScriptValue obj)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (var m in methods)
                {
                    if (m.IsSpecialName)
                        continue;
                    var mw = new StaticMethodWrapper(m);
                    obj.SetIndexedProperty(JavaScriptValue.FromString(m.Name),
                        JavaScriptValue.CreateFunction(mw.Wrap()));
                }
            }
        }
        public class InstanceMethodTableMaker : FunctionTableMaker
        {
            Type type;
            public InstanceMethodTableMaker(Type type)
            {
                this.type = type;
            }

            override public void AssignFunctions(JavaScriptValue obj)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (var m in methods)
                {
                    if (m.IsSpecialName)
                        continue;
                    var mw = new InstanceMethodWrapper(m);
                    obj.SetIndexedProperty(JavaScriptValue.FromString(m.Name),
                        JavaScriptValue.CreateFunction(mw.Wrap()));
                }
            }
        }
        */
    }
}
