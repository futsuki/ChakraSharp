using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChakraSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChakraHost.Hosting;
using System.Runtime.InteropServices;
using System.Reflection;


namespace ChakraSharp.Tests
{
    [TestClass()]
    public class ControllerTests
    {
        //Controller c;

        public static Controller MakeController()
        {
            var c = new Controller();

            GC.Collect(); // clear cache

            c.Evaluate("function jsfun1(v) { return +v + 100; };");
            c.Global["log"] = (JavaScriptNativeFunction)Log;

            return c;
        }


        [TestMethod()]
        public void SimpleTest()
        {
            using (var c = MakeController())
            {
                c.Global["M"] = Port.Util.WrapNamespace("System.Math");
                Assert.IsTrue((double)c.Evaluate("M.Sin(0)").GetObject() == Math.Sin(0));
                c.Global["clrmath"] = Port.Util.WrapType(typeof(Math));
                c.Global["clrmath2"] = Port.Util.WrapType(typeof(Math));
                c.Global["System"] = Port.Util.WrapNamespace("System");
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("System.Math.Sin(0.1)").GetObject(), Math.Sin(0.1)));
            }
        }

        [TestMethod()]
        public void DelegateTest1()
        {
            using (var c = MakeController())
            {
                c.Global["DGList1"] = Port.Util.WrapType(typeof(DGList1));
                c.Evaluate("var dgl = new DGList1();");
                c.Evaluate("var val = 10;");
                c.Evaluate("dgl.lst.Add(function() { val += 1; });");
                c.Evaluate("dgl.lst.Add(function() { val += 2; });");
                c.Evaluate("dgl.lst.Add(function() { val += 3; });");
                c.Evaluate("dgl.Invoke();");
                c.Evaluate("dgl.Invoke();");
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("val").GetObject(), 22));
            }
        }
        [TestMethod()]
        public void DelegateTest2()
        {
            using (var c = MakeController())
            {
                c.Global["DGList2"] = Port.Util.WrapType(typeof(DGList2));
                c.Evaluate("var dgl = new DGList2();");
                c.Evaluate("var val = 10;");
                c.Evaluate("dgl.Add(function() { val += 1; });");
                c.Evaluate("dgl.Add(function() { val += 2; });");
                c.Evaluate("dgl.Add(function() { val += 3; });");
                c.Evaluate("dgl.Invoke();");
                c.Evaluate("dgl.Invoke();");
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("val").GetObject(), 22));
            }
        }
        [TestMethod()]
        public void DelegateTest3()
        {
            using (var c = MakeController())
            {
                c.Global["DGTestClass1"] = Port.Util.WrapType(typeof(DGTestClass1));
                c.Evaluate("var val = 10;");
                c.Evaluate("var dg = function() { val += 1; };");
                c.Evaluate("DGTestClass1.dg = function() { val += 2; };");
                c.Evaluate("DGTestClass1.dg();");
                c.Evaluate("dg();");
                DGTestClass1.dg();
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("val").GetObject(), 15));
            }
        }
        [TestMethod()]
        public void DelegateTest4()
        {
            using (var c = MakeController())
            {
                c.Global["DGList3"] = Port.Util.WrapType(typeof(DGList3));
                c.Evaluate("var dgl = new DGList3();");
                c.Evaluate("var val = 40;");
                c.Evaluate("dgl.lst.Add(function() { val += 1; });");
                c.Evaluate("dgl.lst.Add(function() { val += 2; });");
                c.Evaluate("dgl.lst.Add(function() { val += 3; });");
                c.Evaluate("dgl.Invoke();");
                c.Evaluate("dgl.Invoke();");
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("val").GetObject(), 52));
            }
        }

        class DGTestClass1
        {
            public delegate void dgt();
            static public dgt dg;
        }
        public void fa(object a, object b)
        {
            fb(a, b);
        }
        public object fb(object a, object b)
        {
            return null;
        }
        public class DGList1
        {
            public delegate void DG();
            public List<DG> lst = new List<DG>();
            public void Invoke() { lst.ForEach(e => e.Invoke()); }
        }
        public class DGList3 : DGList1
        {
        }
        public class DGList2
        {
            public delegate void DG();
            public System.Collections.ArrayList lst = new System.Collections.ArrayList();
            public void Add(DG dg)
            {
                lst.Add(dg);
            }
            public void Invoke() {
                foreach (DG e in lst)
                {
                    e.Invoke();
                }
            }
        }

        static bool ApproxEquals(double a, double b, double delta=0.001)
        {
            return Math.Abs(a - b) < delta;
        }

        [TestMethod()]
        public void PortNamespaceTest1()
        {
            using (var c = MakeController())
            {
                c.Global["System"] = Port.Util.WrapNamespace("System");
                //Console.WriteLine(c.Evaluate("System.Math.Sin(0.1)").ToString());
                //Console.WriteLine(c.Evaluate("System.Math.Sin(0.1)()").ToString());
                Assert.IsTrue(ApproxEquals((double)c.Evaluate("System.Math.Sin(0.1)").GetObject(), Math.Sin(0.1)));
                var js = @"
(function() {
var sb = new System.Text.StringBuilder();
sb.Append(""abc"");
sb.Append(""ABC"");
sb.Append(123);
return sb.ToString();
})()
";
                Assert.IsTrue((string)c.Evaluate(js).GetObject() == "abcABC123");
            }
        }
        [TestMethod()]
        public void VarTest1()
        {
            using (var c = MakeController())
            {
                c.Evaluate("var val = 10.1;");
                Assert.IsTrue(c.Global["val"].ToDouble() == 10.1);
                c.Global["val"] = 20;
                Assert.IsTrue(c.Global["val"].ToDouble() == 20);
            }
        }
        [TestMethod()]
        public void FunctionTest1()
        {
            using (var c = MakeController())
            {
                c.Global["clrfun1"] = (Func<int, int>)((v) =>
            {
                return v + 1;
            });
                c.Evaluate("var val2 = clrfun1(10);");
                //Console.WriteLine(c.Global["val2"].ToDouble());
                Assert.IsTrue(c.Global["val2"].ToDouble() == 11);
            }
        }
        [TestMethod()]
        public void FunctionTest2()
        {
            using (var c = MakeController())
            {
                int var1 = 10;
                Assert.IsTrue(var1 == 10);
                c.Global["clrfun2"] = (Action<int>)((v) =>
                {
                    var1 = 20 + v;
                });
                c.Global["clrfun3"] = (Func<int, int>)((v) =>
                {
                    return var1 + v + 1;
                });
                c.Evaluate("clrfun2(10);");
                Assert.IsTrue(var1 == 30);
                Assert.IsTrue((double)c.Evaluate("clrfun3(10)").GetObject() == 41);
            }
        }
        [TestMethod()]
        public void FunctionTest3()
        {
            using (var c = MakeController())
            {
                Assert.IsTrue(c.Global["jsfun1"].Invoke(10).ToDouble() == 110);
            }
        }
        [TestMethod()]
        public void FunctionTest4()
        {
            using (var c = MakeController())
            {
                var jsfun1i = c.Global["jsfun1"].ConvertTo<Func<int, int>>();
                Assert.IsTrue(jsfun1i(20) == 120);
            }
        }
        [TestMethod()]
        public void FunctionTest5()
        {
            using (var c = MakeController())
            {
                var jsfun1d = c.Global["jsfun1"].ConvertTo<Func<double, double>>();
                Assert.IsTrue(jsfun1d(30.0) == 130);
            }
        }
        [TestMethod()]
        public void FunctionTest6()
        {
            using (var c = MakeController())
            {
                var jsfun1s = c.Global["jsfun1"].ConvertTo<Func<string, string>>();
                Assert.IsTrue(jsfun1s("40") == "140"); // convert automatically
            }
        }
        [TestMethod()]
        public void FunctionTest7()
        {
            using (var c = MakeController())
            {
                var jsfun1o = c.Global["jsfun1"].ConvertTo<Func<object, object>>();
                Assert.IsTrue((double)jsfun1o("40") == 140); // js native number type is double
            }
        }
        [TestMethod()]
        public void FunctionTest8()
        {
            using (var c = MakeController())
            {
                var jsonparse = c.Evaluate("JSON.parse").ConvertTo<Func<string, JSValue>>();
                Assert.IsTrue(jsonparse("1").ToDouble() == 1);
                Assert.IsTrue(jsonparse("{\"a\":10}")["a"].ToDouble() == 10);
            }
        }
        [TestMethod()]
        public void PrototypeTest1()
        {
            using (var c = MakeController())
            {
                c.Evaluate("var val3 = {};");
                c.Evaluate("var val4 = { a:10 };");
                c.Evaluate("var val5 = { b:20 };");
                var val3 = c.Global["val3"].rawvalue;
                val3.Prototype = c.Global["val4"].rawvalue;
                var val4 = c.Global["val4"].rawvalue;
                val4.Prototype = c.Global["val5"].rawvalue;
                Assert.IsTrue(c.Global["val3"]["a"].ToDouble() == 10);
                Assert.IsTrue(c.Global["val3"]["b"].ToDouble() == 20);
                Assert.IsTrue(c.Global["val4"]["b"].ToDouble() == 20);

            }
        }
        [TestMethod()]
        public void CLRTypeTest()
        {
            using (var c = MakeController())
            {
                c.Global["clrmath"] = Port.Util.WrapType(typeof(Math));
                c.Global["clrtest1"] = Port.Util.WrapType(typeof(TestCls1));

                Assert.IsTrue((double)c.Evaluate("clrmath.Sqrt(100)").GetObject() == 10);
                Assert.IsTrue((double)c.Evaluate("clrtest1.simpleFunction(100)").GetObject() == 110);
                Assert.IsTrue((string)c.Evaluate("clrtest1.jvtest1()").GetObject() == "ss1");
                Assert.IsTrue((string)c.Evaluate("clrtest1.jvtest2()").GetObject() == "ss2");
                Assert.IsTrue((double)c.Evaluate("clrtest1.overloadedFunction(100)").GetObject() == 120.1);
                Assert.IsTrue((string)c.Evaluate("clrtest1.overloadedFunction(\"100\")").GetObject() == "100str");

                Assert.IsTrue(c.Evaluate("clrtest1(1)").IsUndefined); // call constructor function
                c.Evaluate("var clrhogeval2 = new clrtest1(20)");
                Assert.IsTrue(!c.Evaluate("clrhogeval2").IsUndefined); // construct
                                                                       //Console.WriteLine(c.Evaluate("clrhogeval2.ijvtest1()").rawvalue.ValueType);
                Assert.IsTrue((string)c.Evaluate("clrhogeval2.ijvtest1()").GetObject() == "abc1");
                Assert.IsTrue((string)c.Evaluate("clrhogeval2.ijvtest2()").GetObject() == "abc2");

                //c.Evaluate("var clrhogeval3 = clrhogeval2.ho(30)");
                //Console.WriteLine(c.Evaluate("clrhogeval2"));
                //Console.WriteLine(c.Evaluate("clrhogeval3"));

                //Assert.IsTrue(c.Evaluate("clrhogeval.ho(100)").ToDouble() == 130);
            }
        }
        [TestMethod()]
        public void ActivatorTest()
        {
            var o = Activator.CreateInstance(typeof(TestCls1), new object[] { 1 });
            Assert.IsTrue(o is TestCls1);
            Assert.IsTrue(((TestCls1)o).simpleMethod(10) == 21);
        }
        [TestMethod()]
        public void TestJSNativeFunctionTest()
        {
            using (var c = MakeController())
            {
                c.Global["nativefun1"] = (JavaScriptNativeFunction)TestJSNativeFunction;
                c.Global["nativefun2"] = (JavaScriptNativeFunction)TestJSNativeFunction2;
                Assert.IsTrue((double)c.Evaluate("nativefun1()").GetObject() == 1234);
                c.Evaluate("(function() { try { nativefun2(); return false; } catch (e) { return true; } })()");
                //c.Evaluate("log(nativefun2())");
                c.Evaluate("log(\"test\")");
            }
        }
        [TestMethod()]
        public void PortTest1()
        {
            using (var c = MakeController())
            {
                c.Global["portctor1"] = Port.Util.WrapType(typeof(TestCls1));
                //c.Global["portctor1"] = Port.Util.WrapConstructor(typeof(TestCls1).GetConstructors()[0]);
                c.Global["portmethod1"] = Port.Util.WrapMethod(typeof(TestCls1).GetMethod("jvtest2"));
                c.Global["portmethod2"] = Port.Util.WrapMethod(typeof(TestCls1).GetMethod("jvtest3"));
                c.Global["portmethod2"] = Port.Util.WrapMethod(typeof(TestCls1).GetMethod("jvtest3"));
                c.Evaluate("var portobjerr = portctor1(1)");
                c.Evaluate("var portobj1 = new portctor1(1)");
                Assert.IsTrue(!(c.Evaluate("portobjerr").GetObject() is TestCls1));
                Assert.IsTrue(c.Evaluate("portobj1").GetObject() is TestCls1);
                Assert.IsTrue((string)c.Evaluate("portmethod1()").GetObject() == "ss2");
                Assert.IsTrue((double)c.Evaluate("portmethod2(portobj1)").GetObject() == 124);
                Assert.IsTrue((string)c.Evaluate("portctor1.jvtest1()").GetObject() == "ss1");
                Assert.IsTrue((string)c.Evaluate("portctor1.jvtest2()").GetObject() == "ss2");
                Assert.IsTrue((double)c.Evaluate("portctor1.jvtest3(portobj1)").GetObject() == 124);
                Assert.IsTrue((string)c.Evaluate("portobj1.ijvtest1()").GetObject() == "abc1");
                Assert.IsTrue((string)c.Evaluate("portobj1.ijvtest2()").GetObject() == "abc2");
                Assert.IsTrue((string)c.Evaluate("portobj1.ijvtest3()").GetObject() == "abc3");
            }
        }
        [TestMethod()]
        public void PortOverloadTestEx()
        {
            using (var c = MakeController())
            {
                c.Global["portoc1"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj1 = new portoc1(1)");
                c.Global["portoc2"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj2 = new portoc2(1)");
                c.Global["portoc3"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj3 = new portoc3(1)");
                c.Global["portoc4"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj4 = new portoc4(1)");
            }
        }
        [TestMethod()]
        public void PortOverloadTest1()
        {
            using (var c = MakeController())
            {
                c.Global["portoc1"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj1 = new portoc1(1)");
                var v = c.Evaluate("portoc1.overloadedFunction(10)");
                Assert.IsTrue(c.Evaluate("portocobj1").GetObject() is TestCls1);
                var o = c.Evaluate("portoc1.overloadedFunction(10)");
                Assert.AreEqual(c.Evaluate("portoc1.overloadedFunction(10)").GetObject().GetType(), typeof(double));
                Assert.IsTrue((double)c.Evaluate("portoc1.overloadedFunction(10)").GetObject() == 30.1);
            }
        }
        [TestMethod()]
        public void PortOverloadTest2()
        {
            using (var c = MakeController())
            {
                c.Global["portoc1"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj1 = new portoc1(1)");
                var v = c.Evaluate("portoc1.overloadedFunction(10)");
                Assert.IsTrue(c.Evaluate("portocobj1").GetObject() is TestCls1);
                //Console.WriteLine(v.GetObject().GetType());
                var val = c.Evaluate("portoc1.overloadedFunction(0.0)");
                //Console.WriteLine(val);
                Assert.IsTrue((double)val.GetObject() == 20.1);
            }
        }
        [TestMethod()]
        public void PortOverloadTest3()
        {
            using (var c = MakeController())
            {
                c.Global["portoc1"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("var portocobj1 = new portoc1(1)");
                Assert.IsTrue(c.Evaluate("portocobj1").GetObject() is TestCls1);
                Assert.IsTrue((string)c.Evaluate("portoc1.overloadedFunction(\"hoge\")").GetObject() == "hogestr");
            }
        }
        [TestMethod()]
        public void PortOverloadTest4()
        {
            using (var c = MakeController())
            {
                c.Global["portmath"] = Port.Util.WrapType(typeof(Math));
                Assert.IsTrue((double)c.Evaluate("portmath.Sin(0)").GetObject() == Math.Sin(0.0));
                Assert.IsTrue((double)c.Evaluate("portmath.Ceiling(0.2)").GetObject() == Math.Ceiling(0.2));
                Assert.IsTrue((double)c.Evaluate("portmath.Round(0.2)").GetObject() == Math.Round(0.2));
                //Console.WriteLine(c.Evaluate("JSON.stringify(portmath.Round(0.2222, 2))").GetObject());
                Assert.IsTrue((double)c.Evaluate("portmath.Round(0.2222, \"AwayFromZero\")").GetObject() == Math.Round(0.2222, MidpointRounding.AwayFromZero));
                Assert.IsTrue((double)c.Evaluate("portmath.Round(0.2222, 2)").GetObject() == Math.Round(0.2222, 2));
            }
        }

        [TestMethod()]
        public void PortTest2()
        {
            using (var c = MakeController())
            {
                var jsval = c.Evaluate("({a:2})");
                var fun = c.Evaluate("(function (v) { return v.a; })").ConvertTo<Func<JSValue, double>>();
                Assert.IsTrue(fun(jsval) == 2);
            }
        }

        [TestMethod()]
        public void ActionTest()
        {
            using (var c = MakeController())
            {
                c.Global["portoc1"] = Port.Util.WrapType(typeof(TestCls1));
                c.Evaluate("portoc1.actionFunction()");
                c.Evaluate("(new portoc1(1)).actionMethod()");
            }
        }

        [TestMethod()]
        public void ActionTest2()
        {
            Console.WriteLine("1");
            {
                var dg = Port.InstanceMethodWrapper.ActionToFunc(((Action<int, int>)ControllerTests.a));
                var f = (Func<int, int, object>)dg;
                var o = f(1, 2);
                a(1, 2);
            }
            Console.WriteLine("2");
            {
                var dg = Port.FunctionWrapper.ActionToFunc(((Action<int, int>)ControllerTests.a).Method);
                Console.WriteLine("2_2: " + dg.GetType());
                var f = (Func<int, int, object>)dg;
                Console.WriteLine("2_3" + f);
                var o = f(3, 4);
                Console.WriteLine("2_4: " + o);
                a(1, 2);
            }
            Console.WriteLine("3");
            {
                var dg = Port.FunctionWrapper.ActionToFunc(((Action<int, int>)this.b).Method);
                Console.WriteLine("3_2: " + dg.GetType());
                var f = (Func<ControllerTests, int, int, object>)dg;
                Console.WriteLine("3_3" + f);
                var o = f(this, 5, 6);
                Console.WriteLine("3_4: " + o);
                a(1, 2);
            }
            /*
            {
                Action actdg = () => { Console.WriteLine("dg"); };
                //var dg = Port.InstanceMethodWrapper.ActionToFunc(actdg.GetType().GetMethod("Invoke"), actdg);
                var dg = Port.InstanceMethodWrapper.ActionToFunc(actdg.Method, actdg.Target);
                Console.WriteLine("dg2");
                var f = (Func<object>)dg;
                f();
                actdg();
            }
            {
                var dg = Port.InstanceMethodWrapper.ActionToFunc(((Action)this.b).Method);
                var f = (Func<ControllerTests, object>)dg;
                f(this);
                b();
            }
            */
        }
        static void a(int i, int j)
        {
            Console.WriteLine("aa " + i + "," + j);
        }
        void b(int i, int j)
        {
            Console.WriteLine("bb "+i+ ","+j);
        }


        [TestMethod()]
        public void ErrorTest1()
        {
            using (var c = MakeController())
            {
                try
                {
                    c.Evaluate("(void 0)[0] = 10;");
                    Assert.Fail();
                }
                catch (ChakraSharpException e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        [TestMethod()]
        public void ErrorTest2()
        {
            using (var c = MakeController())
            {
                try
                {
                    c.Global["errorfunc"] = (Func<int>)(() => {
                        if (true)
                        {
                            throw new Exception("throw!!");
                        }
                        return 1;
                    });
                    c.Evaluate("errorfunc();");
                    Assert.Fail();
                }
                catch (ChakraSharpException e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        [TestMethod()]
        public void ErrorTest3()
        {
            using (var c = MakeController())
            {
                try
                {
                    c.Evaluate("throw \"throw!\";"); // no stacktrace
                    Assert.Fail();
                }
                catch (ChakraSharpException e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        [TestMethod()]
        public void ErrorTest4()
        {
            using (var c = MakeController())
            {
                try
                {
                    c.Evaluate("throw new Error(\"throw!\");"); // with stacktrace
                    Assert.Fail();
                }
                catch (ChakraSharpException e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        [TestMethod()]
        public void ClassWrapTest1()
        {
            using (var c = MakeController())
            {
                c.Global["TestClass1"] = Port.Util.WrapType(typeof(TestClass1));
                c.Evaluate("var obj = new TestClass1()");
                c.Evaluate("TestClass1.stNumber = 10;");
                Assert.IsTrue((double)c.Evaluate("TestClass1.stNumber").GetObject() == 10);
                c.Evaluate("TestClass1.stNumprop = 20;");
                Assert.IsTrue((double)c.Evaluate("TestClass1.stNumprop").GetObject() == 20);

                c.Evaluate("obj.number = 30;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 30);
                c.Evaluate("obj.numprop = 40;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 40);

                var obj = (TestClass1)c.Evaluate("obj").GetObject();
                Assert.IsTrue(TestClass1.stNumber == 10);
                Assert.IsTrue(TestClass1.stNumprop == 20);
                Assert.IsTrue(obj.number == 30);
                Assert.IsTrue(obj.numprop == 40);
            }
        }
        [TestMethod()]
        public void ClassWrapTest2()
        {
            using (var c = MakeController())
            {
                c.Global["obj"] = JSValue.FromObject(new TestClass1());

                c.Evaluate("obj.number = 60;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 60);
                c.Evaluate("obj.numprop = 70;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 70);

                var obj = (TestClass1)c.Evaluate("obj").GetObject();
                Assert.IsTrue(obj.number == 60);
                Assert.IsTrue(obj.numprop == 70);
            }
        }
        [TestMethod()]
        public void ClassWrapTest3()
        {
            using (var c = MakeController())
            {
                c.Global["TestClass2"] = Port.Util.WrapType(typeof(TestClass2));
                c.Evaluate("var obj = new TestClass2()");
                c.Evaluate("TestClass2.stNumber = 100;");
                Assert.IsTrue((double)c.Evaluate("TestClass2.stNumber").GetObject() == 100);
                c.Evaluate("TestClass2.stNumprop = 200;");
                Assert.IsTrue((double)c.Evaluate("TestClass2.stNumprop").GetObject() == 200);

                c.Evaluate("obj.number = 300;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 300);
                c.Evaluate("obj.numprop = 400;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 400);

                var obj = (TestClass2)c.Evaluate("obj").GetObject();
                Assert.IsTrue(TestClass2.stNumber == 100);
                Assert.IsTrue(TestClass2.stNumprop == 200);
                Assert.IsTrue(obj.number == 300);
                Assert.IsTrue(obj.numprop == 400);
            }
        }
        [TestMethod()]
        public void NestedClassWrapTest1()
        {
            using (var c = MakeController())
            {
                c.Global["TestClass3"] = Port.Util.WrapType(typeof(TestClass2.TestClass3));
                c.Evaluate("var obj = new TestClass3()");
                c.Evaluate("TestClass3.stNumber = 100;");
                Assert.IsTrue((double)c.Evaluate("TestClass3.stNumber").GetObject() == 100);
                c.Evaluate("TestClass3.stNumprop = 200;");
                Assert.IsTrue((double)c.Evaluate("TestClass3.stNumprop").GetObject() == 200);

                c.Evaluate("obj.number = 300;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 300);
                c.Evaluate("obj.numprop = 400;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 400);

                var obj = (TestClass2.TestClass3)c.Evaluate("obj").GetObject();
                Assert.IsTrue(TestClass2.TestClass3.stNumber == 100);
                Assert.IsTrue(TestClass2.TestClass3.stNumprop == 200);
                Assert.IsTrue(obj.number == 300);
                Assert.IsTrue(obj.numprop == 400);
            }
        }

        [TestMethod()]
        public void NestedClassWrapTest2()
        {
            using (var c = MakeController())
            {
                c.Global["Tests"] = Port.Util.WrapNamespace("ChakraSharp.Tests");
                c.Evaluate("var obj = new Tests.ControllerTests.TestClass2.TestClass3()");
                c.Evaluate("Tests.ControllerTests.TestClass2.TestClass3.stNumber = 100;");
                Assert.IsTrue((double)c.Evaluate("Tests.ControllerTests.TestClass2.TestClass3.stNumber").GetObject() == 100);
                c.Evaluate("Tests.ControllerTests.TestClass2.TestClass3.stNumprop = 200;");
                Assert.IsTrue((double)c.Evaluate("Tests.ControllerTests.TestClass2.TestClass3.stNumprop").GetObject() == 200);

                c.Evaluate("obj.number = 300;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 300);
                c.Evaluate("obj.numprop = 400;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 400);

                var obj = (TestClass2.TestClass3)c.Evaluate("obj").GetObject();
                Assert.IsTrue(TestClass2.TestClass3.stNumber == 100);
                Assert.IsTrue(TestClass2.TestClass3.stNumprop == 200);
                Assert.IsTrue(obj.number == 300);
                Assert.IsTrue(obj.numprop == 400);
            }
        }
        //ChakraSharp.Tests
        [TestMethod()]
        public void ClassWrapTest4()
        {
            using (var c = MakeController())
            {
                c.Global["obj"] = JSValue.FromObject(new TestClass2());

                c.Evaluate("obj.number = 600;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 600);
                c.Evaluate("obj.numprop = 700;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 700);

                var obj = (TestClass2)c.Evaluate("obj").GetObject();
                Assert.IsTrue(obj.number == 600);
                Assert.IsTrue(obj.numprop == 700);
            }
        }
        [TestMethod()]
        public void ClassWrapTest5()
        {
            using (var c = MakeController())
            {
                c.Global["obj"] = JSValue.FromObject(new TestClass2());

                c.Evaluate("obj.dg1 = function(v) { return +v + 10 };");

                var obj = (TestClass2)c.Evaluate("obj").GetObject();
                Assert.IsTrue(obj.dg1(10) == 20);
            }
        }

        [TestMethod()]
        public void ClassWrapTest6()
        {
            using (var c = MakeController())
            {
                c.Global["System"] = Port.Util.WrapNamespace("System");
                Assert.AreEqual(c.Evaluate("System").ToString(), "System");
                Assert.AreEqual(c.Evaluate("System.Math").ToString(), "System.Math");
                Assert.AreEqual(c.Evaluate("System.Linq").ToString(), "System.Linq");
                Assert.AreEqual(c.Evaluate("System.Linq.Expressions").ToString(), "System.Linq.Expressions");
                Assert.AreEqual(c.Evaluate("System.Linq.Expressions.Expression").ToString(), "System.Linq.Expressions.Expression");
            }
        }

        public class TestClass1
        {
            public static int stNumber;
            public static int stNumprop { get; set; }
            public static int stNumprop2 { get; private set; }

            public int number;
            public int numprop { get; set; }
            public int numprop2 { get; private set; }

            public Func<int, int> dg1;
        }
        public class TestClass2 : TestClass1
        {
            public class TestClass3 : TestClass1
            {
            }
        }

        [TestMethod()]
        public void StructWrapTest1()
        {
            using (var c = MakeController())
            {
                c.Global["TestStruct1"] = Port.Util.WrapType(typeof(TestStruct1));
                c.Evaluate("var obj = new TestStruct1()");
                c.Evaluate("TestStruct1.stNumber = 100;");
                Assert.IsTrue((double)c.Evaluate("TestStruct1.stNumber").GetObject() == 100);
                c.Evaluate("TestStruct1.stNumprop = 200;");
                Assert.IsTrue((double)c.Evaluate("TestStruct1.stNumprop").GetObject() == 200);

                c.Evaluate("obj.number = 300;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 300);
                c.Evaluate("obj.numprop = 400;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 400);

                var obj = (TestStruct1)c.Evaluate("obj").GetObject();
                Assert.IsTrue(TestStruct1.stNumber == 100);
                Assert.IsTrue(TestStruct1.stNumprop == 200);
                Assert.IsTrue(obj.number == 300);
                Assert.IsTrue(obj.numprop == 400);
            }
        }
        [TestMethod()]
        public void StructWrapTest2()
        {
            using (var c = MakeController())
            {
                c.Global["obj"] = JSValue.FromObject(new TestStruct1());

                c.Evaluate("obj.number = 600;");
                Assert.IsTrue((double)c.Evaluate("obj.number").GetObject() == 600);
                c.Evaluate("obj.numprop = 700;");
                Assert.IsTrue((double)c.Evaluate("obj.numprop").GetObject() == 700);

                var obj = (TestStruct1)c.Evaluate("obj").GetObject();
                Assert.IsTrue(obj.number == 600);
                Assert.IsTrue(obj.numprop == 700);
            }
        }

        public struct TestStruct1
        {
            public static int stNumber;
            public static int stNumprop { get; set; }
            public static int stNumprop2 { get; private set; }

            public int number;
            public int numprop { get; set; }
            public int numprop2 { get; private set; }

            public Func<int, int> dg1;
        }

        [TestMethod()]
        public void ConverterTest1()
        {
            using (var c = MakeController())
            {
                var arr = c.Evaluate("([10, 20, 30])").ConvertTo<List<double>>();
                Assert.IsTrue(arr[0] == 10);
                Assert.IsTrue(arr[1] == 20);
                Assert.IsTrue(arr[2] == 30);
            }
        }
        [TestMethod()]
        public void ConverterTest2()
        {
            using (var c = MakeController())
            {
                var arr = c.Evaluate("({a:10, b:20, c:30})").ConvertTo<Dictionary<string, double>>();
                Assert.IsTrue(arr["a"] == 10);
                Assert.IsTrue(arr["b"] == 20);
                Assert.IsTrue(arr["c"] == 30);
            }
        }

        [TestMethod()]
        public void TypeWrapTest1()
        {
            using (var c = MakeController())
            {
                c.Global["val1"] = JSValue.FromObject(1);
                Assert.IsTrue((double)c.Evaluate("val1").GetObject() == 1);
                c.Global["val2"] = JSValue.FromObject("ssss");
                Assert.IsTrue((string)c.Evaluate("val2").GetObject() == "ssss");
                c.Global["type"] = JSValue.FromObject(typeof(TestType1));
                c.Evaluate("type.sfield1 = 1;");
                Assert.IsTrue((Type)c.Evaluate("type").GetObject() == typeof(TestType1));
                Assert.IsTrue(TestType1.sfield1 == 1);
            }
        }
        public class TestType1
        {
            public int field1;
            public string field2;
            static public int sfield1;
            static public string sfield2;
        }

        static JavaScriptValue Log(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            Console.WriteLine(string.Join(", ", arguments.Select(e => JSValue.Make(e).ToString()).ToArray()));
            return JavaScriptValue.Undefined;
        }
        static JavaScriptValue TestJSNativeFunction(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            return JavaScriptValue.FromDouble(1234);
        }
        static JavaScriptValue TestJSNativeFunction2(JavaScriptValue callee,
            [MarshalAs(UnmanagedType.U1)] bool isConstructCall,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] JavaScriptValue[] arguments,
            ushort argumentCount,
            IntPtr callbackData)
        {
            JavaScriptContext.SetException(JavaScriptValue.CreateError(JavaScriptValue.FromString("test error")));
            return JavaScriptValue.Invalid;
        }


        [TestMethod()]
        public void Why()
        {
            using (var c = MakeController())
            {
                var o = new Object();
                var v1 = JavaScriptValue.CreateExternalObject(GCHandle.ToIntPtr(GCHandle.Alloc(o)), Free);
                Assert.IsTrue(v1.HasExternalData == true);
                var v2 = JavaScriptValue.CreateObject();
                Assert.IsTrue(v2.HasExternalData == false); // !?
            }
        }
        static void Free(IntPtr p)
        {
            GCHandle.FromIntPtr(p).Free();
        }
    }


    public class TestCls1
    {
        int v;
        public TestCls1(int s)
        {
            v = s;
        }
        static public JSValue jvtest1()
        {
            return JSValue.FromObject("ss1");
        }
        static public JavaScriptValue jvtest2()
        {
            return JavaScriptValue.FromString("ss2");
        }
        static public JSValue jvtest3(TestCls1 o)
        {
            return JSValue.FromObject(o.v + 123);
        }
        public JSValue ijvtest1()
        {
            return JSValue.FromObject("abc1");
        }
        public JavaScriptValue ijvtest2()
        {
            return JavaScriptValue.FromString("abc2");
        }
        public string ijvtest3()
        {
            return "abc3";
        }
        public int simpleMethod(int h)
        {
            return v + 10 + h;
        }
        public string overloadedMethod(string h)
        {
            return v + 10 + h;
        }
        public double overloadedMethod(double h)
        {
            return v + 10 + h;
        }
        static public int simpleFunction(int n)
        {
            return n + 10;
        }
        static public int overloadedFunction(int n)
        {
            return n + 20;
        }
        static public double overloadedFunction(double n)
        {
            return n + 20.1;
        }
        static public string overloadedFunction(string n)
        {
            return n + "str";
        }
        static public void actionFunction()
        {

        }
        public void actionMethod()
        {

        }
    }
    public class TestCls2
    {
        string v;
        public TestCls2(int s)
        {
            //Console.WriteLine("CTOR2 " + s);
            v = s.ToString();
        }
        public TestCls2(string s)
        {
            //Console.WriteLine("CTOR2 " + s);
            v = s;
        }
    }
}