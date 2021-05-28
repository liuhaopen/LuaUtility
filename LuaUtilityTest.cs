using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class LuaUtilityTest
    {
        const string TestStr = @"hah duil uuu uii123 4432.fds_dfei sdfesdfe";
        static string TestLexStateBaseDataStr = "{ int_val = 123, float_val = 4.56789, bool_val = true, str_val = '"+TestStr+"', nil_val = nil, }";
        static string TestLexStateStr1 = "return "+TestLexStateBaseDataStr;

        [Test]
        public void TestLexState()
        {
            LuaUtility.LexState ls = new LuaUtility.LexState(TestLexStateStr1);
            Assert.AreEqual((int)LuaUtility.KeyWord.Return, ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual('{', (char)ls.t.token);

            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Name, ls.t.token);
            Assert.AreEqual("int_val", ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual('=', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Integer, ls.t.token);
            Assert.AreEqual(123, ls.t.i);
            ls.MoveToNextToken();
            Assert.AreEqual(',', (char)ls.t.token);

            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Name, ls.t.token);
            Assert.AreEqual("float_val", ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual('=', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.FLOAT, ls.t.token);
            Assert.AreEqual(4.56789, ls.t.d);
            ls.MoveToNextToken();
            Assert.AreEqual(',', (char)ls.t.token);

            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Name, ls.t.token);
            Assert.AreEqual("bool_val", ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual('=', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.True, ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual(',', (char)ls.t.token);

            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Name, ls.t.token);
            Assert.AreEqual("str_val", ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual('=', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.String, ls.t.token);
            Assert.AreEqual(TestStr, ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual(',', (char)ls.t.token);

            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Name, ls.t.token);
            Assert.AreEqual("nil_val", ls.t.str);
            ls.MoveToNextToken();
            Assert.AreEqual('=', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.Nil, ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual(',', (char)ls.t.token);


            ls.MoveToNextToken();
            Assert.AreEqual('}', (char)ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual((int)LuaUtility.KeyWord.EOZ, ls.t.token);
        }

        [Test]
        public void TestFromLuaInt()
        {
            var str = @"return {i=1}";
            var obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 1);

            str = @"return {i=-1}";
            obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, -1);

            str = @"{i=0}";
            obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 0);
        }

        [Test]
        public void TestFromLuaIntExtraData()
        {
            var str = @"return {i=1, ii=3, t={}, f=1.23, b=false, tt={1,2,3, ttt={a=1}}}";
            LuaUtility.IsShowLog = true;
            var obj = LuaUtility.FromLua<TestLuaInt>(str);
            LuaUtility.IsShowLog = false;
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 1);
        }

        [Test]
        public void TestFromLuaIntLocal()
        {
            var str = @"local t = {i=1} return t";
            var obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 1);

            // CAT_TODO: return前的赋值语法
            LuaUtility.IsShowLog = true;
            str = @"local t = {i=1}  t['i']=1 t['i']=2 return t";
            obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 1);
            LuaUtility.IsShowLog = false;
        }

        [Test]
        public void TestToLuaInt()
        {
            var data = new TestLuaInt();
            data.i = 1234567890;
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("i = 1234567890,"));
        }

        [Test]
        public void TestFromLuaFloat()
        {
            var str = @"return {f=1.2345}";
            var obj = LuaUtility.FromLua<TestLuaFloat>(str);
            Assert.IsNotNull(obj);
            AssertFloat(1.2345, obj.f);
            
            str = @"return {f=-1e+11}";
            obj = LuaUtility.FromLua<TestLuaFloat>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(float.Parse("-1e+11"), obj.f);
            AssertFloat(float.Parse("-1e+11"), obj.f);

            str = @"return {f=.2345}";
            obj = LuaUtility.FromLua<TestLuaFloat>(str);
            Assert.IsNotNull(obj);
            AssertFloat(0.2345, obj.f);
        }

        [Test]
        public void TestFromLuaComment()
        {
            var str = @"--哈哈忽略注释吧
            --[[123456
                忽略多行注释吧1

                忽略多行注释吧2
            654321--]]
            return {i=33}";
            var obj = LuaUtility.FromLua<TestLuaInt>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(obj.i, 33);
        }

        [Test]
        public void TestFromLuaString()
        {
            var str = "return {str=\""+TestStr+"\"}";
            var obj = LuaUtility.FromLua<TestLuaString>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(TestStr, obj.str);

            str = @"return {str='"+TestStr+"'}";
            obj = LuaUtility.FromLua<TestLuaString>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(TestStr, obj.str);

            var str_with_line = @"abcde\
            fghijk";
            str = "return {str=\""+str_with_line+"\"}";
            Debug.LogFormat("LuaUtilityTest[191:04] str:{0}", str);
            obj = LuaUtility.FromLua<TestLuaString>(str);
            Assert.IsNotNull(obj);
            //注意expect_str是故意和str_with_line不一样的，因为lua会把\符号给忽略掉或者转义
            var expect_str = @"abcde
            fghijk";
            Assert.AreEqual(expect_str, obj.str);

            //CAT_TODO:加上\符号的转义测试
            // str_with_line = @"abcde\\a\\bfghijk";
            // str = "return {str=\""+str_with_line+"\"}";
            // Debug.LogFormat("LuaUtilityTest[191:04] str:{0}", str);
            // obj = LuaUtility.FromLua<TestLuaString>(str);
            // Assert.IsNotNull(obj);
            // //注意expect_str是故意和str_with_line不一样的，因为lua会把\符号给忽略掉或者转义
            // expect_str = @"abcdefghijk";
            // Assert.AreEqual(expect_str, obj.str);

            string test_str_mut = @"123456
                多行字符串1

                多行字符串2
            654321";
            str = @"return {str=[["+test_str_mut+"]]}";
            obj = LuaUtility.FromLua<TestLuaString>(str);
            Assert.IsNotNull(obj);
            Assert.AreEqual(test_str_mut, obj.str);
        }

        [Test]
        public void TestFromLuaBase()
        {
            var obj = LuaUtility.FromLua<TestDataBase>(TestLexStateStr1);
            TestDataBase(obj);
        }

        [Test]
        public void TestToLuaBase()
        {
            var data = new TestDataBase();
            data.int_val = 1;
            data.float_val = 2.34;
            data.str_val = TestStr;
            data.bool_val = true;
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("int_val = 1,"));
            Assert.IsTrue(str.Contains("float_val = 2.34,"));
            Assert.IsTrue(str.Contains("str_val = \""+TestStr));
            Assert.IsTrue(str.Contains("bool_val = true,"));
        }

        public void TestDataBase(TestDataBase obj)
        {
            Assert.IsNotNull(obj);
            Assert.AreEqual(123, obj.int_val);
            Assert.AreEqual(4.56789, obj.float_val);
            Assert.AreEqual(TestStr, obj.str_val);
            Assert.AreEqual(true, obj.bool_val);
        }

        [Test]
        public void TestFromLuaInlay()
        {
            string test_code = "return { id = 987, data_int = { i = 23847, }, data_base = "+TestLexStateBaseDataStr+", content = '"+TestStr+"' }";
            var obj = LuaUtility.FromLua<TestDataInlay>(test_code);
            Assert.IsNotNull(obj);
            Assert.AreEqual(987, obj.id);
            Assert.AreEqual(TestStr, obj.content);
            TestDataBase(obj.data_base);
            Assert.IsNotNull(obj.data_int);
            Assert.AreEqual(23847, obj.data_int.i);
        }

        [Test]
        public void TestToLuaInlay()
        {
            var data = new TestDataInlay();
            data.id = 1;
            data.data_int = new TestLuaInt();
            data.data_int.i = 2;
            data.content = TestStr;
            data.data_base = new TestDataBase();
            data.data_base.int_val = 3;
            data.data_base.float_val = 4.56;
            data.data_base.str_val = TestStr;
            data.data_base.bool_val = false;
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("id = 1,"));
            Assert.IsTrue(str.Contains("data_int = {"));
            Assert.IsTrue(str.Contains("i = 2,"));
            Assert.IsTrue(str.Contains("data_base = {"));
            Assert.IsTrue(str.Contains("int_val = 3,"));
            Assert.IsTrue(str.Contains("float_val = 4.56,"));
            Assert.IsTrue(str.Contains("str_val = \""+TestStr));
            Assert.IsTrue(str.Contains("bool_val = false,"));
            Assert.IsTrue(str.Contains("content = \""+TestStr));
        }

        [Test]
        public void TestFromLuaList()
        {
            string test_code = "return {nums={0,1,2,3,4,5,6,7,8,9,10}}";
            var obj = LuaUtility.FromLua<TestDataList>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.nums);
            Assert.AreEqual(11, obj.nums.Count);
            for (int i = 0; i < 11; i++)
            {
                Assert.AreEqual(i, obj.nums[i]);
            }
        }

        [Test]
        public void TestToLuaList()
        {
            var data = new TestDataList();
            data.nums = new List<int>();
            data.nums.Add(1);
            data.nums.Add(21);
            data.nums.Add(621);
            data.nums.Add(2331);
            data.nums.Add(0);
            data.nums.Add(564);
            data.nums.Add(45643);
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("nums = {"));
            int lastIndex = -1;
            for (int i = 0; i < data.nums.Count; i++)
            {
                var newIndex = str.IndexOf(data.nums[i]+",");
                Assert.IsTrue(newIndex!=-1);
                Assert.IsTrue(newIndex > lastIndex);
                lastIndex = newIndex;
            }
        }

        private void SwitchListValue(List<string> list, int a, int b)
        {
            var temp = list[a];
            list[a] = list[b];
            list[b] = temp;
        }

        [Test]
        public void TestFromLuaList2()
        {
            var test_list = new List<int>(){8,6,5,3,4,2,1,7,0,9,10};
            string test_code = "return {nums={";
            var str_list = new List<string>();
            for (int i = 0; i < test_list.Count; i++)
            {
                str_list.Add("["+(i+1)+"] = "+test_list[i]+", ");
            }
            //打乱顺序
            SwitchListValue(str_list, 0, 3);
            SwitchListValue(str_list, 1, 6);
            SwitchListValue(str_list, 2, 9);
            SwitchListValue(str_list, 4, 7);
            foreach (var str in str_list)
            {
                test_code += str;
            }
            test_code += "}}";
            var obj = LuaUtility.FromLua<TestDataList>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.nums);
            Assert.AreEqual(test_list.Count, obj.nums.Count);
            for (int i = 0; i < test_list.Count; i++)
            {
                Assert.AreEqual(test_list[i], obj.nums[i]);
            }
        }

        [Test]
        public void TestFromLuaListTable()
        {
            string test_code = "return {data={{i=1}, {i=2}, {i=3}, {i=4}, {i=5}, {i=6}, {i=7}, {i=8}, {i=9}, {i=10}, {i=11}, }, }";
            var obj = LuaUtility.FromLua<TestDataListTable>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.data);
            Assert.AreEqual(11, obj.data.Count);
            for (int i = 0; i < 11; i++)
            {
                Assert.AreEqual(i+1, obj.data[i].i);
            }
        }

        private void AssertFloat(double expected, double actual)
        {
            var isEqual = Mathf.Abs((float)(expected - actual)) < 0.0000001;
            if (!isEqual)
                Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TestFromLuaUnity()
        {
            string test_code = "return {vec3={x=1.2, y=3.45, z=6.7890}, vec2={x=1, y=2}, rota={x=1,y=2,z=3,w=4}, color={r=1,g=2,b=3,a=4}}";
            var obj = LuaUtility.FromLua<TestLuaUnity>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.vec2);
            Assert.IsNotNull(obj.vec3);
            Assert.IsNotNull(obj.color);
            Assert.IsNotNull(obj.rota);
            AssertFloat(1.2, obj.vec3.x);
            AssertFloat(3.45, obj.vec3.y);
            AssertFloat(6.7890, obj.vec3.z);

            AssertFloat(1, obj.vec2.x);
            AssertFloat(2, obj.vec2.y);

            AssertFloat(1, obj.rota.x);
            AssertFloat(2, obj.rota.y);
            AssertFloat(3, obj.rota.z);
            AssertFloat(4, obj.rota.w);

            AssertFloat(1, obj.color.r);
            AssertFloat(2, obj.color.g);
            AssertFloat(3, obj.color.b);
            AssertFloat(4, obj.color.a);
        }   

        [Test]
        public void TestToLuaUnity()
        {
            var data = new TestLuaUnity();
            data.vec2 = new Vector2(1, 2);
            data.vec3 = new Vector3(3, 4.5f, 6.7f);
            data.rota = new Quaternion(9, 8, 7, 6);
            data.color = new Color(1, 2, 3, 4);
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("vec2 = {"));
            Assert.IsTrue(str.Contains("x = 1,"));
            Assert.IsTrue(str.Contains("y = 2,"));
            Assert.IsTrue(str.Contains("vec3 = {"));
            Assert.IsTrue(str.Contains("x = 3,"));
            Assert.IsTrue(str.Contains("y = 4.5,"));
            Assert.IsTrue(str.Contains("z = 6.7,"));
            Assert.IsTrue(str.Contains("rota = {"));
            Assert.IsTrue(str.Contains("eulerAngles = {"));
            Assert.IsTrue(str.Contains("x = 359.0035,"));
            Assert.IsTrue(str.Contains("y = 105.124,"));
            Assert.IsTrue(str.Contains("z = 97.49586,"));
            Assert.IsTrue(str.Contains("color = {"));
            Assert.IsTrue(str.Contains("r = 1,"));
            Assert.IsTrue(str.Contains("g = 2,"));
            Assert.IsTrue(str.Contains("b = 3,"));
            Assert.IsTrue(str.Contains("a = 4,"));
        }
        
        [Test]
        public void TestFromLuaDic()
        {
            string test_code = "return {dic={a=1.2, b=3.45, c=6.7890}}";
            var obj = LuaUtility.FromLua<TestDataDicStrFlt>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.dic);
            AssertFloat(1.2, obj.dic["a"]);
            AssertFloat(3.45, obj.dic["b"]);
            AssertFloat(6.7890, obj.dic["c"]);
        }

        [Test]
        public void TestToLuaDic()
        {
            var data = new TestDataDicStrFlt();
            data.dic = new Dictionary<string, double>();
            data.dic.Add("a", 1.23);
            data.dic.Add("c", 4);
            data.dic.Add("d", -5.01);
            data.dic.Add("e", 12345);
            data.dic.Add("b", -0.3333);
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("dic = {"));
            Assert.IsTrue(str.Contains("a = 1.23,"));
            Assert.IsTrue(str.Contains("c = 4,"));
            Assert.IsTrue(str.Contains("d = -5.01,"));
            Assert.IsTrue(str.Contains("e = 12345,"));
            Assert.IsTrue(str.Contains("b = -0.3333,"));
        }

        [Test]
        public void TestFromLuaDic2()
        {
            string test_code = "return {dic={[\"a\"]=1.2, [\'b\']=3.45, [\"c\"]=6.7890}}";
            var obj = LuaUtility.FromLua<TestDataDicStrFlt>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.dic);
            AssertFloat(1.2, obj.dic["a"]);
            AssertFloat(3.45, obj.dic["b"]);
            AssertFloat(6.7890, obj.dic["c"]);
        }

        [Test]
        public void TestToLuaDic2()
        {
            var data = new TestDataDicIntStr();
            data.dic = new Dictionary<int, string>();
            data.dic.Add(1, "a");
            data.dic.Add(3, "c");
            data.dic.Add(111, "d");
            data.dic.Add(-1234, "e");
            data.dic.Add(0, "b");
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("dic = {"));
            Assert.IsTrue(str.Contains("[1] = \"a\","));
            Assert.IsTrue(str.Contains("[3] = \"c\","));
            Assert.IsTrue(str.Contains("[111] = \"d\","));
            Assert.IsTrue(str.Contains("[-1234] = \"e\","));
            Assert.IsTrue(str.Contains("[0] = \"b\","));
        }

        [Test]
        public void TestToLuaDic3()
        {
            var data = new TestDataDicIntStr();
            data.dic = new Dictionary<int, string>();
            data.dic.Add(1, "a");
            data.dic.Add(3, "c");
            data.dic.Add(111, "d");
            data.dic.Add(-1234, "e");
            data.dic.Add(0, "b");
            var str = LuaUtility.ToLua(data.dic);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("[1] = \"a\","));
            Assert.IsTrue(str.Contains("[3] = \"c\","));
            Assert.IsTrue(str.Contains("[111] = \"d\","));
            Assert.IsTrue(str.Contains("[-1234] = \"e\","));
            Assert.IsTrue(str.Contains("[0] = \"b\","));
        }

        [Test]
        public void TestFromLuaDic3()
        {
            string test_code = "return {dic={[3]=\"haha\", [1234]=\"abcdefg_ ieow\", [555]=\"uwioerlskdjfo:123342\\354-=|\"}}";
            var obj = LuaUtility.FromLua<TestDataDicIntStr>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.dic);
            Assert.AreEqual("haha", obj.dic[3]);
            Assert.AreEqual("abcdefg_ ieow", obj.dic[1234]);
            Assert.AreEqual("uwioerlskdjfo:123342\\354-=|", obj.dic[555]);

            test_code = "return {dic={\"haha\", \"abcdefg_ ieow\", [555]=\"uwioerlskdjfo:123342\\354-=|\"}}";
            LuaUtility.IsShowLog = true;
            obj = LuaUtility.FromLua<TestDataDicIntStr>(test_code);
            LuaUtility.IsShowLog = false;
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.dic);
            Assert.AreEqual("haha", obj.dic[1]);
            Assert.AreEqual("abcdefg_ ieow", obj.dic[2]);
            Assert.AreEqual("uwioerlskdjfo:123342\\354-=|", obj.dic[555]);
        }

        [Test]
        public void TestFromLuaEnum()
        {
            string test_code = "return {e=1, E=3}";
            var obj = LuaUtility.FromLua<TestDataEnum>(test_code);
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.e);
            Assert.AreEqual(TestEnum.Haha, obj.e);
            Assert.AreEqual(TestEnum2.D, obj.E);
        }

        [Test]
        public void TestToLuaEnum()
        {
            var data = new TestDataEnum();
            data.e = TestEnum.Haha;
            data.E = TestEnum2.D;
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            Assert.IsTrue(str.Contains("e = 1,"));
            Assert.IsTrue(str.Contains("E = 3,"));
        }

        [Test]
        public void TestToLuaPolymorphic()
        {
            var data = new List<TestAnimal>();
            data.Add(new TestDog());
            data.Add(new TestCat());
            var str = LuaUtility.ToLua(data);
            Assert.IsNotNull(str);
            var dog_type_name = typeof(TestDog).FullName+", "+typeof(TestDog).Assembly.GetName().Name;
            var cat_type_name = typeof(TestCat).FullName+", "+typeof(TestCat).Assembly.GetName().Name;
            Assert.IsTrue(str.Contains(string.Format("[\"$type\"] = \"{0}\"", dog_type_name)));
            Assert.IsTrue(str.Contains(string.Format("[\"$type\"] = \"{0}\"", cat_type_name)));
            Assert.IsFalse(str.Contains("TestAnimal"));
        }

        //测试多态
        [Test]
        public void TestFromLuaPolymorphic() 
        {
            var dog_type_name = typeof(TestDog).AssemblyQualifiedName;
            var dog_code = "{[\"$type\"] = \""+dog_type_name+"\", type=\"dog\", can_run=true}";
            var cat_type_name = typeof(TestCat).AssemblyQualifiedName;
            var cat_code = "{[\"$type\"] = \""+cat_type_name+"\", type=\"cat\", life=9}";
            string test_code = "return {"+dog_code+", "+cat_code+"}";
            Debug.LogFormat("LuaUtilityTest[574:49] test_code:{0}", test_code);
            // LuaUtility.IsShowLog = true;
            var obj = LuaUtility.FromLua<List<TestAnimal>>(test_code);
            // LuaUtility.IsShowLog = false;
            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj[0]);
            Assert.IsNotNull(obj[1]);
            Assert.AreEqual(obj[0].GetType(), typeof(TestDog));
            Assert.AreEqual(obj[1].GetType(), typeof(TestCat));
            Assert.AreEqual(obj[0].type, "dog");
            Assert.AreEqual(obj[1].type, "cat");
            Assert.AreEqual((obj[0] as TestDog).can_run, true);
            Assert.AreEqual((obj[1] as TestCat).life, 9);
        }

        [Test]
        public void TestMultiLineStr()
        {
            var test_str = @"1.哈哈哈
                2。行要 国为\n3.line33333
                4.asdfes

                5.seirewsl
                6。槈吸灯这；要
            ";
            var obj = new TestLuaString(){str=test_str};
            var code = LuaUtility.ToLua(obj);
            Debug.LogFormat("LuaUtilityTest[604:50] code:{0}", code);
            var obj_new = LuaUtility.FromLua<TestLuaString>("return "+code);
            Assert.IsNotNull(obj_new);
            Assert.AreEqual(test_str, obj_new.str);
        }

        [Test]
        public void TestToLuaStaticArray()
        {
            var obj = new TestDoubleStaticArray();
            for (int i = 0; i < obj.ds.Length; i++)
            {
                obj.ds[i] = i;
            }
            var code = LuaUtility.ToLua(obj);
            Debug.LogFormat("LuaUtilityTest[604:50 TestToLuaStaticArray] code:{0}", code);
            for (int i = 0; i < obj.ds.Length; i++)
            {
                Assert.IsTrue(code.Contains(string.Format("{0},", i)));
            }
        }

        [Test]
        public void TestFromLuaStaticArray()
        {
            var code = @"{ds={0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11}}";
            var obj_new = LuaUtility.FromLua<TestDoubleStaticArray>("return "+code);
            Assert.IsNotNull(obj_new);
            Assert.AreEqual(12, obj_new.ds.Length);
            for (int i = 0; i < obj_new.ds.Length; i++)
            {
                Assert.AreEqual(i, obj_new.ds[i]);
            }

            var code2 = @"{ds={12, 33.80, 55.123, }}";
            var obj_new2 = LuaUtility.FromLua<TestDoubleStaticArray>("return "+code2);
            Assert.IsNotNull(obj_new2);
            Assert.AreEqual(3, obj_new2.ds.Length);
            AssertFloat(12, obj_new2.ds[0]);
            AssertFloat(33.80, obj_new2.ds[1]);
            AssertFloat(55.123, obj_new2.ds[2]);

        }

    }

    public class TestLuaInt
    {
        public int i;
    }

    public class TestLuaFloat
    {
        public float f;
    }

    public class TestLuaString
    {
        public string str;
    }

    public class TestLuaUnity
    {
        public Vector2 vec2;
        public Vector3 vec3;
        public Quaternion rota;
        public Color color;
    }

    public class TestDataBase
    {
        public int int_val;
        public double float_val;
        public string str_val;
        public bool bool_val;
    }

    public class TestDataInlay
    {
        public int id;
        public TestLuaInt data_int;
        public TestDataBase data_base;
        public string content;
    }

    public class TestDataList
    {
        public List<int> nums; 
    }

    public class TestDataListTable
    {
        public List<TestLuaInt> data; 
    }

    public class TestDoubleStaticArray
    {
        public double[] ds = new double[12];
    }

    public class TestDataDicStrFlt
    {
        public Dictionary<string, double> dic; 
    }

    public class TestDataDicIntStr
    {
        public Dictionary<int, string> dic; 
    }

    public enum TestEnum
    {
        None = 0,
        Haha = 1,
        P = 3,
    }

    public enum TestEnum2
    {
        A = 0,
        B = 1,
        C = 2,
        D = 3,
    }

    public class TestDataEnum
    {
        public TestEnum e;
        private TestEnum2 e2;
        public TestEnum2 E
        {
            get {return e2;}
            set {e2 = value;}
        }
    }

    //测试多态
    public class TestAnimal
    {
        public string type = "Animal";
    }

    public class TestDog : TestAnimal
    {
        public bool can_run = true;
        public TestDog()
        {
            type = "Dog";
        }
    }

    public class TestCat : TestAnimal
    {
        public int life = 9;
        public TestCat()
        {
            type = "Cat";
        }
    }
}
