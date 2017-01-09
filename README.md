# ChakraSharp
ChakraCore wrapper in C#

���̂Ƃ���Linux�Ȃǂł̃l�C�e�B�u�o�C�i�����C�u�����Ƃ̘A�g���@�ɏڂ����Ȃ��̂�windows�ł��������Ă���܂���B


##Examples
###Simple
```C#
var c = new Controller();
c.Evaluate("function jsfun1(v) { return +v + 100; };");
Console.WriteLine(c.Evaluate("jsfun1(10)").ToDouble());
//-> 110
```
###CLR Type Wrapping
```C#
var c = new Controller();
c.Global["clrmath"] = Port.Util.WrapType(typeof(Math));
Console.WriteLine(c.Evaluate("clrmath.Sin(0.1)").ToDouble());
//-> 0.09983341664
```
###CLR Namespace Wrapping
```C#
var c = new Controller();
c.Global["System"] = Port.Util.WrapNamespace("System");
var js = @"
(function() {
var sb = new System.Text.StringBuilder();
sb.Append(""abc"");
sb.Append(""ABC"");
sb.Append(123);
return sb.ToString();
})()
";
Console.WriteLine(c.Evaluate(js).ToString());
//-> abcABC123
```
###JS Function Wrapping & Invoking
```C#
var c = new Controller();
c.Evaluate("function jsfun1(v) { return +v + 100; };");
Func<double, double> jsfun1 = c.Global["jsfun1"].ConvertTo<Func<double, double>>();
Console.WriteLine(jsfun1(40));
//-> 140
```
###Javascript Value Transportation
```C#
var c = new Controller();
JSValue jsval = c.Evaluate("({a:2})");
var fun = c.Evaluate("(function (v) { return v.a; })").ConvertTo<Func<JSValue, double>>();
Console.WriteLine(fun(jsval));
//-> 2
```