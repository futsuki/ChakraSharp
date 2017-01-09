# ChakraSharp
ChakraCore wrapper in C#

今のところLinuxなどでのネイティブバイナリライブラリとの連携方法に詳しくないのでwindowsでしか試しておりません。

.NET 3.5 で動作テストしております。

##Examples
###Simple
```C#
var c = new ChakraSharp.Controller();
c.Evaluate("function jsfun1(v) { return +v + 100; };");
System.Console.WriteLine(c.Evaluate("jsfun1(10)").ToDouble());
//-> 110
```
###CLR Type Wrapping
```C#
var c = new ChakraSharp.Controller();
c.Global["clrmath"] = ChakraSharp.Port.Util.WrapType(typeof(System.Math));
System.Console.WriteLine(c.Evaluate("clrmath.Sin(0.1)").ToDouble());
//-> 0.09983341664
```
###CLR Namespace Wrapping
```C#
var c = new ChakraSharp.Controller();
c.Global["System"] = ChakraSharp.Port.Util.WrapNamespace("System");
var js = @"
(function() {
var sb = new System.Text.StringBuilder();
sb.Append(""abc"");
sb.Append(""ABC"");
sb.Append(123);
return sb.ToString();
})()
";
System.Console.WriteLine(c.Evaluate(js).ToString());
//-> abcABC123
```
###JS Function Calling
```C#
var c = new ChakraSharp.Controller();
var o = c.Evaluate("({a:1, b:2})");
var f = c.Evaluate("(function(v){ return this.a + this.b + v; })");
System.Console.WriteLine(f.Call(o, 10).ToDouble());
//-> 13
```
###JS Function Wrapping & Invoking
```C#
var c = new ChakraSharp.Controller();
c.Evaluate("function jsfun1(v) { return +v + 100; };");
System.Func<double, double> jsfun1 = c.Global["jsfun1"].ConvertTo<System.Func<double, double>>();
Console.WriteLine(jsfun1(40));
//-> 140
```
###Javascript Value Transportation
```C#
var c = new ChakraSharp.Controller();
ChakraSharp.JSValue jsval = c.Evaluate("({a:2})");
var fun = c.Evaluate("(function (v) { return v.a; })").ConvertTo<System.Func<JSValue, double>>();
System.Console.WriteLine(fun(jsval));
//-> 2
```
