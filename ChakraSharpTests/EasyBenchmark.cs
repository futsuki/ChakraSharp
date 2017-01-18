using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChakraSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChakraHost.Hosting;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;

namespace ChakraSharp.Tests
{
    [TestClass()]
    public class EasyBenchmark
    {
        static void Go(Action a)
        {
            var watch = new Stopwatch();
            watch.Start();
            a();
            watch.Stop();
            Console.WriteLine(watch.ElapsedMilliseconds + "ms");
        }
        [TestMethod()]
        public void Benchmark1()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                c.Evaluate("var f = function() { i += 1; }");
                c.Evaluate("f(1, 2)");
                Go(() =>
                {
                    c.Evaluate("for (var i=0; i<10000; i++) { f(); }");
                });
            }
        }
        [TestMethod()]
        public void Benchmark2()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                var f = c.Evaluate("(function() { i += 1; })").ConvertTo<Action<JavaScriptValue>>();
                var u = JavaScriptValue.Undefined;
                f(u);
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f(u);
                    }
                });
            }
        }
        [TestMethod()]
        public void Benchmark3()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                var f = c.Evaluate("(function() { i += 1; })").ConvertTo<Func<JavaScriptValue, JavaScriptValue>>();
                var u = JavaScriptValue.Undefined;
                f(u);
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f(u);
                    }
                });
            }
        }
        static int j = 1;
        static int i = 0;
        static void nativefun1(int n, int m)
        {
            i += j;
        }
        static double nativefun2(int n, int m)
        {
            i += j;
            return 0;
        }
        [TestMethod()]
        public void Benchmark4()
        {
            using (var c = ControllerTests.MakeController())
            {
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        nativefun1(1, 2);
                    }
                });
                Console.WriteLine(i);
            }
        }

        [TestMethod()]
        public void Benchmark5()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (Action<int, int>)(nativefun1);
                c.Evaluate("f(1, 2)");
                Go(() =>
                {
                    c.Evaluate("for (var i=0; i<10000; i++) { f(1, 2); }");
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark5_2()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (Func<int, int, double>)(nativefun2);
                c.Evaluate("f(1, 2)");
                Go(() =>
                {
                    c.Evaluate("for (var i=0; i<10000; i++) { f(1, 2); }");
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark5_3()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (Func<int, int, double>)(nativefun2);
                c.Evaluate("f(1, 2)");
                Go(() =>
                {
                    c.Evaluate("for (var i=0; i<10000; i++) { Math.sin(1); }");
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark7()
        {
            // fastest js native function calling?
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                var f = c.Evaluate("(function() { i += 1; })");
                var vs = new JavaScriptValue[1] {
                    JavaScriptValue.Undefined
                };
                f.Call(vs);
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f.Call(vs);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark7_2()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                c.Evaluate("var f = (function() { i += 1; })");
                c.Evaluate("f()");
                var buf = new byte[10240];
                var size = JavaScriptContext.SerializeScript("f()", buf);
                buf = buf.Take((int)size).ToArray();
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        JavaScriptContext.RunScript("f()", buf);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark7_3()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                c.Evaluate("var f = (function() { i += 1; })");
                c.Evaluate("f()");
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        JavaScriptContext.RunScript("f()");
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark7_4()
        {
            // fastest js native function calling?
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                var f = c.Evaluate("(function() { i += 1; })");
                var vs = new JavaScriptValue[] {
                    JavaScriptValue.GlobalObject
                };
                f.Call(vs);
                var f2 = f.rawvalue;
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f2.CallFunction(vs);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark7_5()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Evaluate("var i = 1");
                c.Evaluate("var f = (function() { i += 1; })");
                c.Evaluate("f()");
                Go(() =>
                {
                    JavaScriptContext.RunScript("for (var i=0; i<10000;i++) { f() }");
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark8()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (Func<int, int, double>)(nativefun2);
                var f = c.Evaluate("f");
                var vs = new JavaScriptValue[] {
                    JavaScriptValue.Null,
                    JavaScriptValue.FromInt32(1),
                    JavaScriptValue.FromInt32(1)
                };
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f.Call(vs);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void Benchmark9()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (Action<int, int>)(nativefun1);
                var f = c.Evaluate("f");
                var vs = new JavaScriptValue[] {
                    JavaScriptValue.Null,
                    JavaScriptValue.FromInt32(1),
                    JavaScriptValue.FromInt32(1)
                };
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f.Call(vs);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void BenchmarkA()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (JavaScriptNativeFunction)(jsnativefun1);
                var f = c.Evaluate("f");
                var vs = new JavaScriptValue[] {
                    JavaScriptValue.Null,
                };
                f.Call(vs);
                Go(() =>
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        f.Call(vs);
                    }
                });
                Console.WriteLine(i);
            }
        }
        [TestMethod()]
        public void BenchmarkA_2()
        {
            using (var c = ControllerTests.MakeController())
            {
                c.Global["f"] = (JavaScriptNativeFunction)(jsnativefun1);
                var f = c.Evaluate("f");
                var vs = new JavaScriptValue[] {
                    JavaScriptValue.Null,
                };
                f.Call(vs);
                Go(() =>
                {
                    JavaScriptContext.RunScript("for (var i=0; i<10000;i++) { f() }");
                });
                Console.WriteLine(i);
            }
        }

        static JavaScriptValue jsnativefun1(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            return JavaScriptValue.Undefined;
        }
    }
}

