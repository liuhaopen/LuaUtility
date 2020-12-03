using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

public class LuaUtility
{
    private static bool isShowLog = false;
    static string TabStr = "\t";

    public static bool IsShowLog { get => isShowLog; set => isShowLog = value; }

    public static string ToLua(object obj, int level=1)
    {
        Type property_type = obj.GetType();
        Type list_type = typeof(IList);
        Type dic_type = typeof(IDictionary);
        StringBuilder content = new StringBuilder();
        string tab_str = GetStrMutiple(TabStr, level);
        if (property_type.IsPrimitive)
        {
            if (obj is bool)
            {
                content.Append(((bool)obj)?"true":"false");
            }
            else
                content.Append(obj.ToString());
        }
        else if (property_type == typeof(string))
        {
            content.Append("\"" + obj.ToString() + "\"");
        }
        else if (property_type.IsEnum)
        {
            content.Append((int)obj);
        }
        else if (list_type.IsAssignableFrom(property_type))
        {
            content.Append("{\n");
            IEnumerable list_info = obj as IEnumerable;
            foreach (var list_item in list_info)
            {
                content.Append(tab_str);
                content.Append(ToLua(list_item, level+1));
                content.Append(",\n");
            }
            content.Append(GetStrMutiple(TabStr, level-1));
            content.Append("}");
        }
        else if (dic_type.IsAssignableFrom(property_type))
        {
            content.Append("{\n");
            IDictionary dic_info = obj as IDictionary;
            foreach (var item in dic_info)
            {
                var itemKey = item.GetType().GetProperty("Key").GetValue(item, null);
                var itemKeyType = dic_info.GetType().GetGenericArguments()[0];
                var itemValue = item.GetType().GetProperty("Value").GetValue(item, null);
                string itemKeyStr;
                if (itemKeyType == typeof(string))
                    itemKeyStr = itemKey.ToString();
                else
                    itemKeyStr = "["+itemKey.ToString()+"]";
                content.Append(tab_str);
                content.Append(itemKeyStr + " = ");
                content.Append(ToLua(itemValue, level+1));
                content.Append(",\n");
            }
            content.Append(GetStrMutiple(TabStr, level-1));
            content.Append("}");
        }
        else
        {
            content.Append("{\n");
            Type type = obj.GetType();
            var contractAttr = type.GetCustomAttribute(typeof(DataContractAttribute));
            //如果类有 DataContract 特性的话，就只导出其带有 DataMember 特性的字段，否则导出所有 public 字段
            bool isNeedAttr = contractAttr != null;
            MemberInfo[] members = type.GetMembers();
            // UnityEngine.Debug.Log("members.Length : "+members.Length.ToString());
            if (members != null && members.Length > 0)
            {
                foreach (MemberInfo p in members)
                {
                    var isNeedExport = true;
                    if (isNeedAttr)
                    {
                        object[] objAttrs = p.GetCustomAttributes(typeof(DataMemberAttribute), true);
                        isNeedExport = objAttrs != null && objAttrs.Length > 0;
                    }
                    // UnityEngine.Debug.Log("objAttrs.Length : "+objAttrs.Length.ToString()+ " field!=null:"+(field!=null).ToString());
                    if (isNeedExport)
                    {
                        object obj_value = null;
                        FieldInfo field = p as FieldInfo;
                        if(field!=null && !field.IsStatic)
                        {
                            obj_value = field.GetValue(obj);
                        }
                        else
                        {
                            PropertyInfo pro = p as PropertyInfo;
                            if (pro != null && pro.CanRead && pro.CanWrite)
                            {
                                // Debug.Log("pro name : "+pro.Name+" "+type.Name+" mtype:"+pro.MemberType);
                                try {
                                    obj_value = pro.GetValue(obj);
                                }
                                catch{}
                            }
                        }
                        if (obj_value != null)
                        {
                            content.Append(tab_str + p.Name + " = ");
                            content.Append(ToLua(obj_value, level+1));
                            content.Append(",\n");
                        }
                    }
                };
            }
            content.Append(GetStrMutiple(TabStr, level-1));
            content.Append("}");
        }
        return content.ToString();
    }

    public static string GetStrMutiple(string str, int num)
    {
        string result = "";
        for (int i = 0; i < num; i++)
        {
            result += str;
        }
        return result;
    }

    //---------------------------Lua->C#---------------------------------
    public static void ThrowError(string err_str)
    {
        Debug.LogError(err_str);
        throw new System.InvalidOperationException(err_str); 
    }

    public class Token 
    {
        public int token;
        public string str;
        public int i;
        public double d;
    }
    public enum KeyWord
    {
        Local,
        Nil,
        Return,
        True,
        False,
        Dots,
        Equal,
        String,
        Name,
        Integer,
        FLOAT,
        Concat,
        EOZ
    }

    public class LexState
    {
        public static int EOZ = -1;
        public int current;
        private int code_i;
        public Token t;  /* current token */
        public Token lookahead;  /* look ahead token */
        public string code;
        private Dictionary<string,KeyWord> reserved_words;
        static int[] LuaI_CType = new int[]{
            0x00,  /* EOZ */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* 0. */
            0x00,  0x08,  0x08,  0x08,  0x08,  0x08,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* 1. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x0c,  0x04,  0x04,  0x04,  0x04,  0x04,  0x04,  0x04,	/* 2. */
            0x04,  0x04,  0x04,  0x04,  0x04,  0x04,  0x04,  0x04,
            0x16,  0x16,  0x16,  0x16,  0x16,  0x16,  0x16,  0x16,	/* 3. */
            0x16,  0x16,  0x04,  0x04,  0x04,  0x04,  0x04,  0x04,
            0x04,  0x15,  0x15,  0x15,  0x15,  0x15,  0x15,  0x05,	/* 4. */
            0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,
            0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,	/* 5. */
            0x05,  0x05,  0x05,  0x04,  0x04,  0x04,  0x04,  0x05,
            0x04,  0x15,  0x15,  0x15,  0x15,  0x15,  0x15,  0x05,	/* 6. */
            0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,
            0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,  0x05,	/* 7. */
            0x05,  0x05,  0x05,  0x04,  0x04,  0x04,  0x04,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* 8. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* 9. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* a. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* b. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* c. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* d. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* e. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,	/* f. */
            0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,  0x00,
        };

        private static int ALPHABIT = 0;
        private static int DIGITBIT = 1;
        private static int PRINTBIT = 2;
        private static int SPACEBIT = 3;
        private static int XDIGITBIT = 4;

    
        public LexState(string _code)
        {
            InitReserved();
            code = _code;
            code_i = -1;
            NextChar();
            MoveToNextToken();
        }

        public string Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("current : "+current+" token:"+t.token+" str:"+t.str+" code_i:"+code_i);
            return sb.ToString();
        }

        private void InitReserved()
        {
            reserved_words = new Dictionary<string, KeyWord>();
            reserved_words.Add("local", KeyWord.Local);
            reserved_words.Add("return", KeyWord.Return);
            reserved_words.Add("false", KeyWord.False);
            reserved_words.Add("true", KeyWord.True);
            reserved_words.Add("nil", KeyWord.Nil);
        }

        private void NextChar()
        {
            code_i++;
            if (code_i < code.Length)
                current = code[code_i];
            else
                current = EOZ;
        }
        public Token MoveToNextToken()
        {
            if (lookahead != null)
            {
                t = lookahead;
                lookahead = null;
            }
            else 
            {
                t = new Token();
                t.token = ReadToken(ref t);
            }
            return null;
        }

        public Token ReadAheadToken()
        {
            lookahead = new Token();
            lookahead.token = ReadToken(ref lookahead);
            return lookahead;
        }

        private KeyWord TryGetReserved(string str)
        {
            if (reserved_words.ContainsKey(str))
                return reserved_words[str];
            else
                return KeyWord.Name;
        }

        private bool CheckNext1(char set)
        {
            if (current == set)
            {
                NextChar();
                return true;
            }
            return false;
        }

        private bool CheckNext2(string set)
        {
            if (current == set[0] || current == set[1])
            {
                NextChar();
                return true;
            }
            return false;
        }

        public bool TestNext(int c)
        {
            if (t.token == c)
            {
                NextChar();
                return true;
            }
            return false;
        }

        private bool TestProp(int c, int p)
        {
            var ret = (LuaI_CType[(int)c+1] & p);
            return ret != 0;
        }

        private int GetMask(int i)
        {
            return 1<<i;
        }

        public bool IsAlpha(int c)
        {
            return TestProp(c, GetMask(ALPHABIT));
        }
        public bool IsAlphaNum(int c)
        {
            return TestProp(c, GetMask(ALPHABIT) | GetMask(DIGITBIT));
        }

        public bool IsDigit(int c)
        {
            return TestProp(c, GetMask(DIGITBIT));
        }

        private void ReadString(ref Token token, int del)
        {
            NextChar();
            int start_i = code_i;
            while (current != del)
            {
                if (current == EOZ || current == '\n' || current == '\r')
                {
                    LuaUtility.ThrowError("unfinished string");
                    break;
                }
                // else if ()//TODO:处理\换行符号
                else
                {
                    NextChar();
                }
            }
            token.str = code.Substring(start_i, code_i-start_i);
            // Debug.Log("read string : "+token.str+" start_i:"+start_i+" end_i:"+code_i);
            NextChar();
        }

        private int ReadNumeral(ref Token token, bool is_decimal)
        {
            string expo = "Ee";
            int start_i = code_i;
            var first = current;
            NextChar();
            if (first == '0' && CheckNext2("xX"))
                expo = "Pp";
            while (true)
            {
                if (CheckNext2(expo))
                    CheckNext2("-+");
                if (IsDigit(current))
                    NextChar();
                else if (current == '.')
                    NextChar();
                else
                    break;
            }
            var num_str = code.Substring(start_i, code_i-start_i);
            if (is_decimal)
                num_str = "0." + num_str;
            int ret_i;
            var isInt = int.TryParse(num_str, out ret_i);
            if (isInt)
            {
                token.i = ret_i;
                return (int)KeyWord.Integer;
            }
            else
            {
                double ret_d;
                num_str = num_str.Replace("e", "E");
                var isDouble = double.TryParse(num_str, out ret_d);
                if (isDouble)
                {
                    token.d = ret_d;
                }
                else
                {
                    var err_str = string.Format("malformed number {0} in code {1}~{2}", num_str, start_i, code_i);
                    Debug.LogError(err_str);
                }
                return (int)KeyWord.FLOAT;
            }
        }

        private int SkipSep()
        {
            int count = 0;
            int s = current;
            NextChar();
            while (current == '=')
            {
                NextChar();
                count++;
            }
            return (current == s) ? count : (-count)-1;
        }

        private void IncLineNumber()
        {
            int old = current;
            NextChar();
            if (CurrentIsNewLine() && current != old)
                NextChar();
        }

        private bool CurrentIsNewLine()
        {
            return current == '\n' || current == '\r';
        }

        private string ReadLongString(ref Token token, int sep)
        {
            bool isString = token != null;
            NextChar();
            int start_i = code_i;
            int end_i = code_i;
            if (CurrentIsNewLine())
                IncLineNumber();
            while (true)
            {
                if (current == EOZ)
                {
                    ThrowError("unfinished long "+(isString?"string":"comment")+", ls:"+Dump());
                    break;
                }
                else if (current == ']')
                {
                    end_i = code_i;
                    if (SkipSep() == sep)
                    {
                        NextChar();
                        break;
                    }
                }
                else
                {
                    NextChar();
                }
            }
            return code.Substring(start_i, end_i-start_i-(isString?0:2));
        }

        public int ReadToken(ref Token token)
        {
            int loop_max = 5000;
            while (loop_max > 0)
            {
                loop_max--;
                if (current == '\n' || current == '\r')
                {
                    //换行符不管    
                    NextChar();
                }
                else if (current == ' ' || current == '\f' || current == '\t' || current == '\v')
                {
                    NextChar();
                }
                else if (current == '-')
                {
                    NextChar();
                    if (current != '-') 
                        return '-';
                    NextChar();
                    if (current == '[') 
                    {  /* long comment? */
                        int sep = SkipSep();
                        if (sep >= 0)
                        {
                            Token emptyToken = null;
                            var longStr = ReadLongString(ref emptyToken, sep);
                            // Debug.Log("ReadToken comment longStr : "+longStr);
                        }
                    }
                    while (!CurrentIsNewLine() && current != EOZ)
                        NextChar();
                }
                else if (current == '[')
                {
                    int sep = SkipSep();
                    if (sep >= 0)
                    {
                        var longStr = ReadLongString(ref token, sep);
                        // Debug.Log("ReadToken string longStr : "+longStr);
                        token.str = longStr;
                        return (int)KeyWord.String;
                    }
                    return '[';
                }
                else if (current == '=')
                {
                    NextChar();
                    if (current == '=')    
                        return (int)KeyWord.Equal;
                    else 
                        return '=';
                }
                else if (current == '"' || current == '\'')
                {
                    ReadString(ref token, current);
                    return (int)KeyWord.String;
                }
                else if (current == '.')
                {
                    NextChar();
                    if (CheckNext1('.'))
                    {
                        if (CheckNext1('.'))
                            return (int)KeyWord.Dots;
                        else
                            return (int)KeyWord.Concat;
                    }
                    else if (!IsDigit(current))
                    {
                        return '.';
                    }
                    else
                    {
                        return ReadNumeral(ref token, true);
                    }
                }
                else if (current >= (int)'0' && current <= (int)'9')
                {
                    return ReadNumeral(ref token, false);
                }
                else if (current == EOZ)
                {
                    return (int)KeyWord.EOZ;
                }
                else
                {
                    if (IsAlpha(current))
                    {
                        var start_i = code_i;
                        do 
                        {
                            NextChar();
                        } while(IsAlphaNum(current));
                        token.str = code.Substring(start_i, code_i-start_i);
                        return (int)TryGetReserved(token.str);
                    }
                    else
                    {
                        /* single-char tokens (+ - / ...) */
                        var c = current;
                        NextChar();
                        return c;
                    }
                }
            } 
            return 0;
        }

    }

    private static object ParseExp(LexState ls, Type type)
    {
        Token t = ls.t;
        switch (t.token)
        {
            case (int)KeyWord.String:
            {
                ls.MoveToNextToken();
                return t.str;
            }
            case (int)KeyWord.Name:
            {
                ls.MoveToNextToken();
                return t.str;
            }   
            case (int)KeyWord.FLOAT:
            {
                ls.MoveToNextToken();
                return t.d;
            }
            case (int)KeyWord.Integer:
            {
                ls.MoveToNextToken();
                return t.i;
            }
            case (int)KeyWord.Nil:
            {
                ls.MoveToNextToken();
                return null;
            }
            case (int)KeyWord.False:
            {
                ls.MoveToNextToken();
                return false;
            }
            case (int)KeyWord.True:
            {
                ls.MoveToNextToken();
                return true;
            }
            case (int)'{':
            {
                return ParseTableConstructor(ls, type);
            }
            case (int)'-':
            {
                ls.MoveToNextToken();
                t = ls.t;
                ls.MoveToNextToken();
                if (t.token == (int)KeyWord.FLOAT)
                    return -t.d;
                else if (t.token == (int)KeyWord.Integer)
                    return -t.i;
                else
                    ThrowError("wrong : '-' must before a number! ls:"+ls.Dump());
                return null;
            }
            default:
            {
            }
            break;
        }
        return null;
    }

    private static object ParseTableConstructor(LexState ls, Type type)
    {
        Token t = ls.t;
        Token ahead = ls.lookahead;
        if (t.token == (int)'{')
        {
            ls.MoveToNextToken();
            object ret = null;
            if (type != null)
            {
                Log("start table constructor for type : "+type.Name);
                ret = System.Activator.CreateInstance(type);
                Assert.IsNotNull(ret, "cannot create instance for type : "+type.Name);
            }
            var isOk = ParseFieldList(ls, ref ret, type);
            Log("end table constructor for type : "+(type!=null?type.Name:"unknow type")+" isOk:"+isOk+" token is }"+(ls.t.token == (int)'}')+" token:"+ls.t.token);
            if (isOk && ls.t.token == (int)'}')
            {
                ls.MoveToNextToken();
                return ret;
            }
        }
        else
        {
            Debug.LogError("wrong table constructor!"+ls.Dump());
        }
        return null;
    }

    //FieldList ::= Field {FieldSep Field} [FieldSep]
    private static bool ParseFieldList(LexState ls, ref object obj, Type type)
    {
        int index = 0;
        var isSep = false;
        do 
        {
            if (ls.t.token == (int)'}')
                break;
            ParseField(ls, ref obj, type, index);
            index++;
            isSep = IsFieldSep(ls.t);
            if (isSep)
                ls.MoveToNextToken();
        } while (isSep);
        return true;
    }

    private static bool IsFieldSep(Token t)
    {
        return (t.token == (int)',') || (t.token == (int)';');
    }

    private static Type GetTypeByMemberInfo(MemberInfo mem)
    {
        FieldInfo field = mem as FieldInfo;
        if(field!=null)
        {
            return field.FieldType;
        }
        else
        {
            PropertyInfo pro = mem as PropertyInfo;
            if (pro != null)
                return pro.PropertyType;
        }
        return mem.GetType();
    }

    private static Type GetTypeByKeyName(object obj, Type type, string keyName)
    {
        var mems = type.GetMember(keyName);
        if (mems.Length == 1)
        {
            var mem = mems[0];
            return GetTypeByMemberInfo(mem);
        }
        return null;
    }

    private static Type GetFieldTypeFromList(Type t)
    {
        if (t.IsGenericType)
        {
            var listType = t.GetGenericArguments()[0];
            return listType;
        }
        return typeof(Nullable);
    }

    private static Type GetFieldTypeFromDic(Type t)
    {
        if (t.IsGenericType)
        {
            var listType = t.GetGenericArguments()[1];
            return listType;
        }
        return typeof(Nullable);
    }

    private static object ConvertToRealType(object val, Type fieldType)
    {
        if (fieldType.IsEnum)
            return Enum.ToObject(fieldType, val);
        else
            return Convert.ChangeType(val, fieldType);
    }

    private static void AssignValue(LexState ls, ref object obj, Type type, int index, object keyNameObj)
    {
        string logStr = string.Format("AssignValue keyNameObj:{0} type:{1} obj:{2} ls:{3}", keyNameObj, (type!=null?type.Name:"unknow type"), obj!=null?obj.ToString():"null", ls.Dump());
        Log(logStr);
        if (type != null && type.IsGenericType)
        {
            if (obj is IDictionary)
            {
                var dic = obj as IDictionary;
                Type fieldType = GetFieldTypeFromDic(type);
                var val = ParseExp(ls, fieldType);
                var valObj = ConvertToRealType(val, fieldType);
                Assert.IsNotNull(valObj, "value nil! code dump:"+ls.Dump());
                if (keyNameObj == null)
                    keyNameObj = index + 1;
                dic.Add(keyNameObj, valObj);
            }
            else if (obj is IList)
            {
                Type fieldType = GetFieldTypeFromList(type);
                if (fieldType == typeof(Nullable))
                {
                    LuaUtility.ThrowError("wrong file type from list : "+fieldType?.Name+" "+ls.Dump());
                    return;
                }
                var val = ParseExp(ls, fieldType);
                var list = obj as IList;

                if (keyNameObj != null && keyNameObj is int)
                    index = (int)keyNameObj-1;
                Debug.Log("ParseField type : "+type.Name+" index:"+index+" list.Count:"+list.Count);
                if (list.Count <= index)
                {
                    var new_count = index-list.Count+1;
                    for (int i = 0; i < new_count; i++)
                    {
                        list.Add(System.Activator.CreateInstance(fieldType));
                    }
                }
                list[index] = ConvertToRealType(val, fieldType);
            }
            else
                ThrowError("wrong type of obj : "+type.Name+" ls:"+ls.Dump());
        }
        else
        {
            var keyName = keyNameObj as string;
            var fieldType = type != null ? GetTypeByKeyName(obj, type, keyName) : null;
            var val = ParseExp(ls, fieldType);
            var mems = type != null ? type.GetMember(keyName) : null;
            // Debug.Log("ParseField keyName : "+keyName+" fieldType:"+fieldType?.Name);
            if (mems != null && mems.Length == 1 && val != null)
            {
                var mem = mems[0];
                FieldInfo field = mem as FieldInfo;
                if(field != null)
                {
                    field.SetValue(obj, ConvertToRealType(val, fieldType));
                }
                else
                {
                    PropertyInfo pro = mem as PropertyInfo;
                    if (pro != null)
                    {
                        pro.SetValue(obj, ConvertToRealType(val, fieldType));
                    }
                }
            }
            else
            {
                //如果 lua 代码里的字段在 c# 的结构体里没有，应该要支持这种用法，所以就不打印了
                // Log("members num error : "+(mems != null ? mems.Length : 0)+" by key name:"+keyName);
            }
        }
    }
    
    //Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp
    private static void ParseField(LexState ls, ref object obj, Type type, int index)
    {
        Token t = ls.t;
        if (t.token == (int)'[')
        {
            ls.MoveToNextToken();
            var keyName = ParseExp(ls, type);
            Assert.IsTrue(ls.t.token == (int)']');
            ls.MoveToNextToken();
            Assert.IsTrue(ls.t.token == (int)'=');
            ls.MoveToNextToken();
            AssignValue(ls, ref obj, type, index, keyName);
        }
        else if (t.token == (int)KeyWord.Name)
        {
            var keyName = t.str;
            ls.MoveToNextToken();
            Assert.IsTrue(ls.t.token == (int)'=');
            ls.MoveToNextToken();
            AssignValue(ls, ref obj, type, index, keyName);
        }
        else
        {
            AssignValue(ls, ref obj, type, index, null);
        }
    }

    private static void Log(string str)
    {
        if (IsShowLog)
            Debug.Log(str);
    }

    private static object FromLuaInternal(string code, Type type)
    {
        Log("FromLuaInternal code:"+code);
        LexState ls = new LexState(code);
        ls.ReadAheadToken();
        object ret = null;
        Log("FromLuaInternal ls.t.token : "+ls.t.token+" "+ls.lookahead.token);
        if (ls.t.token == (int)KeyWord.Return && ls.lookahead.token == (int)'{')
        {
            ls.MoveToNextToken();
            ret = ParseExp(ls, type);
        }
        else if (ls.t.token == (int)KeyWord.Local)
        {
            ls.MoveToNextToken();
            Assert.AreEqual((int)KeyWord.Name, ls.t.token);
            ls.MoveToNextToken();
            Assert.AreEqual('=', ls.t.token);
            ls.ReadAheadToken();
            if (ls.lookahead.token == (int)'{')
            {
                ls.MoveToNextToken();
                ret = ParseExp(ls, type);
            }
        }
        return ret;
    }

    /*  
    支持所解析的 lua BNF:
        Exp ::= nil | false | true | Nunber | String | TableconStructor
        TableconStructor ::= ‘{’ [FieldList] ‘}’
        FieldList ::= Field {FieldSep Field} [FieldSep]
        Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp
        FieldSep ::= ‘,’ | ‘;’
    */
    public static T FromLua<T>(string code) 
    { 
        return (T)FromLua(code, typeof(T)); 
    }

    public static object FromLua(string code, Type type)
    {
        if (string.IsNullOrEmpty(code))
            return null;
        if (type == null)
            throw new ArgumentNullException("type");

        if (type.IsAbstract || type.IsSubclassOf(typeof(MonoBehaviour)))
            throw new ArgumentException("Cannot deserialize Lua to new instances of type '" + type.Name + ".'");

        return FromLuaInternal(code, type);
    }
}