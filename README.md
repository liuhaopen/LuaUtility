# LuaUtility
parse lua code to a c# object, or generate lua code by a c# object

### lua code => c# object 
```
string test_code = "return {a=1.2, b=false, c=\"haha\"}";
TestClass obj = LuaUtility.FromLua<TestClass>(test_code);
//result : obj.a == 1.2 && obj.b == false && obj.c == "haha"
```

### c# object => lua code
```
TestClass obj = new TestClass(){a=1, b=true, c="aaa"};
string code = LuaUtility.ToLua(obj);
//result : code == "{a=1, b=true, c="aaa"}"
```
  
### Support Lua BNF:  
&ensp;Stat ::= **local** Name ‘=’ Exp **return** Name | **return** Exp  
&ensp;Exp ::= nil | false | true | Nunber | String | TableconStructor  
&ensp;TableconStructor ::= ‘{’ [FieldList] ‘}’  
&ensp;FieldList ::= Field {FieldSep Field} [FieldSep]  
&ensp;Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp  
&ensp;FieldSep ::= ‘,’ | ‘;’  
