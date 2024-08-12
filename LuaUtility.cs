using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;

public class LuaUtility
{
    public class ToLuaSetting
    {
        public bool SkipProperty = false;
        public List<string> propertyWhiteList;
        public int MaxLevel = -1;
        public bool ExportRootType = false;//是否导出传入的类类型信息
    }
    private static bool isShowLog = false;
    static string TabStr = "\t";
    static LexState cur_ls_in_parse_for_debug = null;

    public static bool IsShowLog { get => isShowLog; set => isShowLog = value; }

    public static string ToLua(object obj, ToLuaSetting setting=null)
    {
        return ToLua(obj, 1, null, setting);
    }

    static readonly Type list_type = typeof(IList);
    static readonly Type dic_type = typeof(IDictionary);
    private static string ToLua(object obj, int level, Type obj_member_type, ToLuaSetting setting)
    {
        if (setting != null && setting.MaxLevel != -1 && level >= setting.MaxLevel)
            return "";
        if (obj == null)
            return "nil";
        Type type = obj.GetType();
        StringBuilder content = new StringBuilder();
        string tab_str = GetStrMutiple(TabStr, level);
        if (type.IsPrimitive)
        {
            if (obj is bool)
            {
                content.Append(((bool)obj)?"true":"false");
            }
            else
                content.Append(obj.ToString());
        }
        else if (type == typeof(string))
        {
            // var str = obj.ToString().Replace("\n", "\\\n");
            var str = obj.ToString();
            var symbol = "\"";
            var symbol_end = symbol;
            if (str.IndexOf("\n") != -1)
            {
                symbol = "[[";
                symbol_end = "]]";
            }
            content.Append(symbol + str + symbol_end); 
        }
        else if (type.IsEnum)
        {
            content.Append((int)obj);
        }
        else if (list_type.IsAssignableFrom(type))
        {
            content.Append("{\n");
            IEnumerable list_info = obj as IEnumerable;
            Type member_type = null;
            var gneric_args = type.GetGenericArguments();
            if (gneric_args != null && gneric_args.Length > 0)
                member_type = gneric_args[0];
            else
                member_type = type.GetElementType();
            
            foreach (var list_item in list_info)
            {
                content.Append(tab_str);
                content.Append(ToLua(list_item, level+1, member_type, setting));
                content.Append(",\n");
            }
            content.Append(GetStrMutiple(TabStr, level-1));
            content.Append("}");
        }
        else if (dic_type.IsAssignableFrom(type))
        {
            content.Append("{\n");
            IDictionary dic_info = obj as IDictionary;
            foreach (var item in dic_info)
            {
                var itemKey = item.GetType().GetProperty("Key").GetValue(item, null);
                var itemKeyType = dic_info.GetType().GetGenericArguments()[0];
                var itemValue = item.GetType().GetProperty("Value").GetValue(item, null);
                var itemValueType = dic_info.GetType().GetGenericArguments()[1];
                string itemKeyStr;
                if (itemKeyType == typeof(string))
                    itemKeyStr = "[\""+itemKey.ToString()+"\"]";
                else
                    itemKeyStr = "["+itemKey.ToString()+"]";
                content.Append(tab_str);
                content.Append(itemKeyStr + " = ");
                content.Append(ToLua(itemValue, level+1, itemValueType, setting));
                content.Append(",\n");
            }
            content.Append(GetStrMutiple(TabStr, level-1));
            content.Append("}");
        }
        else
        {
            content.Append("{\n");
            // Type type = obj.GetType();
            var contractAttr = type.GetCustomAttribute(typeof(DataContractAttribute));
            //如果类有 DataContract 特性的话，就只导出其带有 DataMember 特性的字段，否则导出所有 public 字段
            bool isNeedAttr = contractAttr != null;
            MemberInfo[] members = type.GetMembers();
            // UnityEngine.Debug.Log("members.Length : "+members.Length.ToString());
            if (members != null && members.Length > 0)
            {
                //只有实际类型和定义的类型不一样才需要加上类型信息，即多态时才加
                if ((obj_member_type != type && level > 1) || (level == 1 && setting != null && setting.ExportRootType))
                    content.Append(tab_str + string.Format("[\"$type\"] = \"{0}\",\n", type.FullName+", "+type.Assembly.GetName().Name));
                foreach (MemberInfo p in members)
                {
                    var isNeedExport = true;
                    if (isNeedAttr)
                    {
                        object[] objAttrs = p.GetCustomAttributes(typeof(DataMemberAttribute), true);
                        isNeedExport = objAttrs != null && objAttrs.Length > 0;
                    }
                    if (isNeedExport)
                    {
                        object[] objAttrs = p.GetCustomAttributes(typeof(HideInInspector), true);
                        isNeedExport = objAttrs == null || objAttrs.Length <= 0;
                    }
                    if (isNeedExport)
                    {
                        // Debug.LogFormat("LuaUtility[107:06] p.ToString():{0}", p.ToString());
                        object obj_value = null;
                        FieldInfo field = p as FieldInfo;
                        Type member_type = null;
                        if(field!=null && !field.IsStatic)
                        {
                            obj_value = field.GetValue(obj);
                            member_type = field.FieldType;
                        }
                        else if (setting == null || !setting.SkipProperty || (setting.propertyWhiteList != null && setting.propertyWhiteList.Contains(p.Name)))
                        {
                            PropertyInfo pro = p as PropertyInfo;
                            if (pro != null && pro.CanRead && pro.CanWrite)
                            {
                                member_type = pro.PropertyType;
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
                            content.Append(ToLua(obj_value, level+1, member_type, setting));
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
        public long i;
        public double d;
        public override string ToString()
        {
            return string.Format("Token:{0} str:{1} i:{2} d:{3}", token, str, i, d);
        }
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
        Function,
        End,
        IF,
        While,
        For,
        EOZ
    }

    public class LexState : ICloneable
    {
        public static int EOZ = -1;
        public int current;
        public int code_i;
        public Token t;  /* current token */
        public Token lookahead;  /* look ahead token */
        public string code;
        private int linenumber;
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
            linenumber = 1;
            NextChar();
            MoveToNextToken();
        }

        public void UpdateData(LexState ls)
        {
            this.code = ls.code;
            this.code_i = ls.code_i;
            this.t = ls.t;
            this.lookahead = ls.lookahead;
            this.current = ls.current;
        }

        public string Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("line : "+linenumber+" current : "+current+" token:"+t.ToString()+" code_i:"+code_i+" lookahead:"+(lookahead!=null?lookahead.ToString():"null"));
            return sb.ToString();
        }

        private void InitReserved()
        {
            reserved_words = new Dictionary<string, KeyWord>();
            reserved_words.Add("local", KeyWord.Local);
            reserved_words.Add("return", KeyWord.Return);
            reserved_words.Add("false", KeyWord.False);
            reserved_words.Add("true", KeyWord.True);
            reserved_words.Add("function", KeyWord.Function);
            reserved_words.Add("end", KeyWord.End);
            reserved_words.Add("if", KeyWord.IF);
            reserved_words.Add("while", KeyWord.While);
            reserved_words.Add("for", KeyWord.For);
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
            void OnlySave(ref Token _token, string str)
            {
                _token.str = _token.str.Substring(0, _token.str.Length-1);
                _token.str += str;
            }
            void ReadSave(ref Token _token, string str)
            {
                OnlySave(ref _token, str);
                NextChar();
            }
            NextChar();
            int start_i = code_i;
            token.str = "";
            while (current != del)
            {
                if (current == EOZ || current == '\n' || current == '\r')
                {
                    LuaUtility.ThrowError("unfinished string");
                    break;
                }
                else if (current == '\\')//CAT_TODO:处理\\符号的\x:十六进制 \u八进制 \z
                {
                    token.str += code.Substring(start_i, code_i-start_i+1);
                    // Debug.LogFormat("LuaUtility[391:54] token.str:{0}", token.str+" start_i:"+start_i);
                    NextChar();
                    start_i = code_i;
                    // Debug.LogFormat("LuaUtility[420:05] current:{0} start_i:{1}", current, start_i);
                    if (current == 'a' || current == 'b' || current == 'f' || current == 'n' || current == 'r' || current == 't' || current == 'v' )
                    {
                        ReadSave(ref token, "\\"+current);
                    }
                    else if (current == '\n' || current == '\r')
                    {
                        IncLineNumber();
                        OnlySave(ref token, "\n");
                    }
                    else if (current == '\\' || current == '\"' || current == '\'')
                    {
                        ReadSave(ref token, ((char)current).ToString());
                    }
                    start_i = code_i;
                }
                else
                {
                    NextChar();
                }
            }
            token.str += code.Substring(start_i, code_i-start_i);
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
            long ret_i;
            var isInt = long.TryParse(num_str, out ret_i);
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
            Assert.IsTrue(CurrentIsNewLine());
            NextChar();
            if (CurrentIsNewLine() && current != old)
                NextChar();
            if (++linenumber >= int.MaxValue)
                Assert.IsTrue(false, "line number too much");
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
                else if (current == '\n' || current == '\r')
                {
                   IncLineNumber();
                   end_i = code_i;
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
                    IncLineNumber();
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

        public object Clone()
        {
            return this.MemberwiseClone();
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

    private static Type TryGetRealType(LexState ls)
    {
        Type result = null;
        if (ls.t.token == (int)'[')
        {
            ls.ReadAheadToken();
            if (ls.lookahead.token == (int)KeyWord.String && ls.lookahead.str == "$type")
            {
                ls.ReadAheadToken();
                ls.ReadAheadToken();
                ls.ReadAheadToken(); 
                var type_full_name = ls.lookahead.str;
                if (settings != null && settings.CustomTypeDic != null && settings.CustomTypeDic.ContainsKey(type_full_name))
                    result = settings.CustomTypeDic[type_full_name];
                else
                    result = Type.GetType(type_full_name);
                Assert.IsNotNull(result, "cannot find type by name : "+(type_full_name));
                // SceneEditorNS.PathPointInfoData
            }
        }
        return result;
    }

    private static object CreateInstance(Type type)
    {
        if (type == typeof(string))
        {
            return "";
        }
        else if (type.IsArray)
        {
            var e_type = type.GetElementType();
            return Array.CreateInstance(e_type, 1);
        }
        else
        {
            return System.Activator.CreateInstance(type);
        }
    }

    private static object ParseTableConstructor(LexState ls, Type type)
    {
        Token t = ls.t;
        if (t.token == (int)'{')
        {
            ls.MoveToNextToken();
            object ret = null;
            if (type != null)
            {
                Log("start table constructor for type : "+type.Name);
                var old_ls_dump = ls.Dump();
                var backup = ls.Clone();
                var realType = TryGetRealType(ls);
                // ls = backup as LexState;
                ls.UpdateData(backup as LexState);
                if (realType != null)
                    type = realType;
                
                ret = CreateInstance(type);
                Assert.IsNotNull(ret, "cannot create instance for type : "+type.Name);
            }
            var isOk = ParseFieldList(ls, ref ret, type);
            Log("end table constructor for type : "+(type!=null?type.Name:"unknow type")+" isOk:"+isOk+" token is }"+(ls.t.token == (int)'}')+" token:"+ls.t);
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
        Log(string.Format("obj:{0} type:{1} ls:{2}", obj, type, ls.Dump()));
        int index = 0;
        var isSep = false;
        do 
        {
            if (ls.t.token == (int)'}')
                break;
            ParseField(ls, ref obj, type, ref index);
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
        if (null == keyName)
            return null;
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

    private static Type GetKeyTypeFromDic(Type t)
    {
        if (t.IsGenericType)
        {
            var listType = t.GetGenericArguments()[0];
            return listType;
        }
        return typeof(Nullable);
    }

    private static object ConvertToRealType(object val, Type fieldType)
    {
        if (val == null)
        {
            return null;
        }

        Type valType = val.GetType();

        // 如果val的类型和fieldType一致，则无需转换
        if (valType == fieldType)
        {
            return val;
        }

        if (fieldType.IsEnum)
            return Enum.ToObject(fieldType, val);
        else if (IsFloatNum(fieldType))
        {
            //先尝试转成更高位数的浮点型，判断实际数字是否超出范围
            double doubleVal;
            if (double.TryParse(val.ToString(), out doubleVal))
            {
                double minValue = Convert.ToDouble(fieldType.GetField("MinValue").GetValue(null));
                double maxValue = Convert.ToDouble(fieldType.GetField("MaxValue").GetValue(null));

                if (doubleVal < minValue || doubleVal > maxValue)
                {
                    LogErrorStrict("Value out of range for type "+fieldType.Name+" real value:"+doubleVal);
                }
                return Convert.ChangeType(doubleVal, fieldType);
            }
            else
            {
                LogErrorStrict("Invalid value for type "+fieldType.Name);
                return null;
            }
        }
        else if (IsIntNumType(fieldType))
        {
            long longVal;
            if (long.TryParse(val.ToString(), out longVal))
            {
                long minValue = Convert.ToInt64(fieldType.GetField("MinValue").GetValue(null));
                long maxValue = Convert.ToInt64(fieldType.GetField("MaxValue").GetValue(null));

                if (longVal < minValue || longVal > maxValue)
                {
                    LogErrorStrict("Value out of range for type "+fieldType.Name+" real value:"+longVal);
                }
                return Convert.ChangeType(longVal, fieldType);
            }
            else
            {
                LogErrorStrict("Invalid value for type "+fieldType.Name+" real value:"+val.ToString());
                return null;
            }
        }
        else if (val.GetType() != fieldType && !val.GetType().IsSubclassOf(fieldType))
        {
            try {
                return Convert.ChangeType(val, fieldType);
            }
            catch (Exception e)
            {
                Debug.LogError("ConvertToRealType error! val:"+val+" fieldType:"+fieldType+" e:"+e);
            }
            return null;
        }
        return val;
    }

    private static bool IsIntNumType(Type type)
    {
        return type == typeof(byte) ||
            type == typeof(sbyte) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong);
            // type == typeof(float) ||
            // type == typeof(double) ||
            // type == typeof(decimal);
    }

    private static bool IsFloatNum(Type type)
    {
        return type == typeof(float) ||
            type == typeof(double);
    }

    private static void AssignValue(LexState ls, ref object obj, Type type, ref int index, object keyNameObj)
    {
        string logStr = string.Format("AssignValue keyNameObj:{0} type:{1} obj:{2} index:{3} ls:{4}", keyNameObj, (type!=null?type.Name:"unknow type"), obj!=null?obj.ToString():"null", index, ls.Dump());
        Log(logStr);
        if (type != null && type.IsGenericType)
        {
            if (obj is IDictionary)
            {
                var dic = obj as IDictionary;
                Type fieldType = GetFieldTypeFromDic(type);
                var val = ParseExp(ls, fieldType);
                var valObj = ConvertToRealType(val, fieldType);
                // Assert.IsNotNull(valObj, "value nil! code dump:"+ls.Dump());
                if (keyNameObj == null)
                    keyNameObj = index + 1;
                else
                    index--;//为了处理先有[num]然后后面正常数组的情况，比如{[0]=0, 1, [2]=2, 3, 4}
                try
                {
                    Type keyType = GetKeyTypeFromDic(type);
                    var keyObj = ConvertToRealType(keyNameObj, keyType);
                    if (keyObj != null)
                        dic.Add(keyObj, valObj);
                    else
                        LogErrorStrict("wrong key type : "+keyNameObj?.GetType().Name+" key:"+ keyNameObj.ToString() +" "+ls.Dump());
                }
                catch(Exception e) {
                    Debug.LogWarning(e.Message);
                };
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

                if (keyNameObj != null && IsIntNumType(keyNameObj.GetType()))
                {
                    var keyNameObjNum = Convert.ToInt32(keyNameObj);
                    index = keyNameObjNum-1;
                }
                else if(keyNameObj != null)
                {
                    LogErrorStrict("wrong key type : "+keyNameObj?.GetType().Name+" "+ls.Dump());
                }
                // Debug.Log("ParseField type : "+type.Name+" index:"+index+" list.Count:"+list.Count);
                if (list.Count <= index)
                {
                    var new_count = index-list.Count+1;
                    for (int i = 0; i < new_count; i++)
                    {
                        list.Add(CreateInstance(fieldType));
                    }
                }
                list[index] = ConvertToRealType(val, fieldType);
            }
            else
                LogErrorStrict("wrong type of obj : "+type.Name+" ls:"+ls.Dump());
        }
        else if (type != null && type.IsArray)
        {
            Type fieldType = type.GetElementType();
            var val = ParseExp(ls, fieldType);
            var valObj = ConvertToRealType(val, fieldType);

            if (keyNameObj != null && IsIntNumType(keyNameObj.GetType()))
            {
                var keyNameObjNum = Convert.ToInt32(keyNameObj);
                index = keyNameObjNum-1;
            }
            else if(keyNameObj != null)
            {
                LogErrorStrict("wrong key type : "+keyNameObj?.GetType().Name+" "+ls.Dump());
            }
            var arr = obj as Array;
            if (arr.Length <= index)
            {
                //CAT_TODO:应该要做到只需一次创建,现在的方式太不科学了,遇到大点的数组就超级慢了
                var new_arr = Array.CreateInstance(fieldType, index+1);
                Array.Copy(arr, new_arr, arr.Length);
                arr = new_arr;
                obj = arr;
            }
            arr.SetValue(valObj, index);
        }
        else
        {
            var keyName = keyNameObj as string;
            var fieldType = type != null ? GetTypeByKeyName(obj, type, keyName) : null;
            var val = ParseExp(ls, fieldType);
            MemberInfo[] mems = null;
            if (null != keyName)
                mems = type != null ? type.GetMember(keyName) : null;
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
    private static void ParseField(LexState ls, ref object obj, Type type, ref int index)
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
            AssignValue(ls, ref obj, type, ref index, keyName);
        }
        else if (t.token == (int)KeyWord.Name)
        {
            var keyName = t.str;
            ls.MoveToNextToken();
            Assert.IsTrue(ls.t.token == (int)'=');
            ls.MoveToNextToken();
            AssignValue(ls, ref obj, type, ref index, keyName);
        }
        else
        {
            AssignValue(ls, ref obj, type, ref index, null);
        }
    }

    private static void Log(string str)
    {
        if (IsShowLog)
            Debug.Log(str);
    }

    private static void LogErrorStrict(string str)
    {
        if (IsStrictMode())
        {
            Debug.LogError(str);
            throw new Exception(str);
        }
    }

    private static bool IsStrictMode()
    {
        return LuaUtility.settings != null && LuaUtility.settings.StrictMode;
    }

    private static object FromLuaInternal(string code, Type type)
    {
        Log("FromLuaInternal code:"+code);
        LexState ls = new LexState(code);
        cur_ls_in_parse_for_debug = ls;
        ls.ReadAheadToken();
        object ret = null;
        Log("FromLuaInternal ls.t.token : "+ls.t.token+" "+ls.lookahead.token);
        if (ls.t.token == (int)KeyWord.Return && ls.lookahead.token == (int)'{')
        {
            ls.MoveToNextToken();
            ret = ParseExp(ls, type);
        }
        else if (ls.t.token == (int)'{')
        {
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

    public class Settings
    {
        public Dictionary<string, Type> CustomTypeDic;
        public bool StrictMode = false;//严格模式,如果为 true,则 lua 代码里的字段必须在 c# 的结构体里有对应的字段
    }

    /*  
    支持所解析的 lua BNF:
        Exp ::= nil | false | true | Nunber | String | TableconStructor
        TableconStructor ::= ‘{’ [FieldList] ‘}’
        FieldList ::= Field {FieldSep Field} [FieldSep]
        Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp
        FieldSep ::= ‘,’ | ‘;’
    */
    public static T FromLua<T>(string code, Settings settings=null) 
    { 
        return (T)FromLua(code, typeof(T), settings); 
    }

    private static Settings settings;
    public static object FromLua(string code, Type type, Settings settings=null)
    {
        if (string.IsNullOrEmpty(code))
            return null;
        if (type == null)
            throw new ArgumentNullException("type");

        if (type.IsAbstract || type.IsSubclassOf(typeof(MonoBehaviour)))
            throw new ArgumentException("Cannot deserialize Lua to new instances of type '" + type.Name + ".'");

        LuaUtility.settings = settings;
        object ret = null;
        try
        {
            ret = FromLuaInternal(code, type);
        }
        catch (System.Exception)
        {
            Debug.LogFormat("LuaUtility[parse lua code error] :{0}", cur_ls_in_parse_for_debug.Dump());
            throw;
        }
        return ret;
    }

    public class LuaValue
    {
        public object data;
        public string type;
        // public int line;

        public virtual string DumpCode()
        {
            return string.Empty;
        }
        
        public static bool operator ==(LuaValue obj1, LuaValue obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;

            if (ReferenceEquals(obj1, null) || ReferenceEquals(obj2, null))
                return false;

            return obj1.type == obj2.type && obj1.data.Equals(obj2.data);
        }

        public static bool operator !=(LuaValue obj1, LuaValue obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            LuaValue other = (LuaValue)obj;
            return this.type == other.type && this.data.Equals(other.data);
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }

        public long ToInt()
        {
            if (type == "number")
                return (long)data;
            else if (type == "string")
                return long.Parse((string)data);
            else
                throw new Exception("LuaValue ToInt error, type is not number or string, type:"+type);
        }

        public double ToDouble()
        {
            if (type == "number")
                return (double)data;
            else if (type == "string")
                return double.Parse((string)data);
            else
                throw new Exception("LuaValue ToDouble error, type is not number or string, type:"+type);
        }

        public bool ToBool()
        {
            if (type == "bool")
                return (bool)data;
            else if (type == "string")
                return bool.Parse((string)data);
            else if (type == "nil")
                return false;
            else
                throw new Exception("LuaValue ToBool error, type is not boolean or string, type:"+type);
        }

        // public override string ToString()
        // {
        //     if (type == "string")
        //         return (string)data;
        //     else if (type == "number")
        //         return data.ToString();
        //     else
        //         throw new Exception("LuaValue ToString error, type is not number or string, type:"+type);
        // }
    }

    public class LuaTable : LuaValue
    {
        public class Key : LuaValue
        {
            //匿名数组索引
            // public bool is_anonym_key = false;
            public bool has_brackets = false;//key是否有[]符号包起来
            public Key(string key_name, bool has_brackets)
            {
                this.data = key_name;
                this.has_brackets = has_brackets;
                this.type = "string";
            }

            public Key(int index, bool has_brackets)
            {
                this.data = index;
                this.has_brackets = has_brackets;
                this.type = "number";
            }

            public Key(LuaValue value, bool has_brackets)
            {
                this.data = value.data;
                this.type = value.type;
                this.has_brackets = has_brackets;
            }

            public override string ToString()
            {
                if (data != null)
                    return data.ToString();
                return string.Empty;
            }
        }
        public class KeyValue
        {
            public Key key;
            public LuaValue value;
            public KeyValue(Key key, LuaValue value)
            {
                this.key = key;
                this.value = value;
            }
        }

        public LuaTable()
        {
            type = "table";
            //我们要保留lua代码中字段出现的顺序信息，所以用list
            data = new List<KeyValue>();
        }

        public LuaValue this[string key]
        {
            get
            {
                return GetValue(key);
            }
            set
            {
                var last_val = GetValue(key);
                if (last_val != null)
                {
                    last_val.data = value.data;
                    last_val.type = value.type;
                }
                else
                {
                    var key_obj = new Key(key, true);
                    AddCode(key_obj, value);
                }
            }
        }

        //lua的语法：当key是整型时，会优先取数组部分，所以local sss = {[0]=0, [1]=112, 1, 2, [2]=22, 3, [3]=33}
        //实际上：sss[1]==1，sss[2]==2, sss[3]==3，而[1][2][3]这三个是字典部分就没了，目前暂没这个需求，有了再实现吧
        public LuaValue this[int index]
        {
            get
            {
                return GetValueIn(index);
            }
            set
            {
                var last_val = GetValueIn(index);
                if (last_val != null)
                {
                    last_val.data = value.data;
                    last_val.type = value.type;
                }
                else
                {
                    var key_obj = new Key(index, false);
                    AddCode(key_obj, value);
                }
            }
        }

        public int FieldCount
        {
            get
            {
                var rdata = data as List<KeyValue>;
                return rdata.Count;
            }
        }

        public override string DumpCode()
        {
            StringBuilder content = new StringBuilder();
            var rdata = data as List<KeyValue>;
            content.Append("{");
            for (int i = 0; i < rdata.Count; i++)
            {
                string key_str;
                string key_name = rdata[i].key.data as string;
                if (rdata[i].key.type == "number")
                {
                    if (rdata[i].key.has_brackets)
                        key_str = "[" + key_name + "] = ";
                    else
                        key_str = "";
                }
                else
                {
                    if (rdata[i].key.has_brackets)
                        key_str = "[\"" + key_name + "\"] = ";
                    else
                        key_str = key_name + " = ";
                }
                var value_str = rdata[i].value.DumpCode();
                content.Append(key_str + value_str);
                if (i < rdata.Count - 1)
                    content.Append(",\n");
            }
            content.Append("}");
            return content.ToString();
        }

        public void AddCode(Key key, LuaValue value)
        {
            var val = new KeyValue(key, value);
            var rdata = data as List<KeyValue>;
            rdata.Add( val );
        }

        public LuaValue GetValue(string key)
        {
            var rdata = data as List<KeyValue>;
            foreach(var kv in rdata)
            {
                if (kv.key.type == "string" && kv.key.data as string == key)
                    return kv.value;
            }
            return null;
        }

        public LuaValue GetValueIn(int index)
        {
            index += 1;//lua的数组索引是从1开始的
            var rdata = data as List<KeyValue>;
            LuaValue ret = null;
            foreach(var kv in rdata)
            {
                if (kv.key.type == "number" && kv.key.data != null && kv.key.data.Equals(index))
                {
                    //lua的语法：当key是整型时，会优先取数组部分，所以local sss = {[0]=0, [1]=112, 1, 2, [2]=22, 3, [3]=33}
                    //实际上：sss[1]==1，sss[2]==2, sss[3]==3，而[1][2][3]这三个是字典部分就没了
                    if (kv.key.has_brackets)
                        ret = kv.value;
                    else
                        return kv.value;
                }
            }
            return ret;
        }

        public KeyValue GetCodeIn(int index)
        {
            var rdata = data as List<KeyValue>;
            if (index < 0 || index >= rdata.Count)
                return null;
            return rdata[index];
        }

        public LuaTable GetTable(string key)
        {
            return GetValue(key) as LuaTable;
        }

        public LuaTable GetTableIn(int index)
        {
            return GetValueIn(index) as LuaTable;
        }
    }

    public class LuaString : LuaValue
    {
        public LuaString(string str)
        {
            type = "string";
            data = str;
        }

        public static implicit operator LuaString(string value)
        {
            return new LuaString(value);
        }

        public override string DumpCode()
        {
            var str = data.ToString();
            var symbol = "\"";
            var symbol_end = symbol;
            if (str.IndexOf("\n") != -1)
            {
                symbol = "[[";
                symbol_end = "]]";
            }
            return symbol + str + symbol_end;
        }

        public override string ToString()
        {
            if (data != null)
                return data.ToString();
            return string.Empty;
        }

        public static bool operator ==(LuaString tbl, string value)
        {
            if (ReferenceEquals(tbl, null))
                return false;

            return tbl.ToString() == value;
        }

        public static bool operator !=(LuaString tbl, string value)
        {
            return !(tbl == value);
        }

        // public override bool Equals(object obj)
        // {
        //     if (obj == null || GetType() != obj.GetType())
        //         return false;

        //     LuaString other = (LuaString)obj;
        //     return Name.Equals(other.Name);
        // }

        // public override int GetHashCode()
        // {
        //     return base.GetHashCode();
        // }
    }

    public class LuaInteger : LuaValue
    {
        public LuaInteger(long num)
        {
            type = "number";
            data = num;
        }

        public static implicit operator LuaInteger(long value)
        {
            return new LuaInteger(value);
        }

        public static bool operator ==(LuaInteger num, long value)
        {
            if (ReferenceEquals(num, null))
                return false;

            return num.ToInt() == value;
        }

        public static bool operator !=(LuaInteger num, long value)
        {
            return !(num == value);
        }

        public override string DumpCode()
        {
            return data.ToString();
        }
    }

    public class LuaFloat : LuaValue
    {
        public LuaFloat(double num)
        {
            type = "number";
            data = num;
        }

        public static implicit operator LuaFloat(double value)
        {
            return new LuaFloat(value);
        }

        public static bool operator ==(LuaFloat num, double value)
        {
            if (ReferenceEquals(num, null))
                return false;

            return num.ToDouble() == value;
        }

        public static bool operator !=(LuaFloat num, double value)
        {
            return !(num == value);
        }

        public override string DumpCode()
        {
            return data.ToString();
        }
    }

    // public class LuaNil : LuaValue
    // {
    //     public LuaNil()
    //     {
    //         type = "nil";
    //         data = null;
    //     }

    //     public override string DumpCode()
    //     {
    //         return "nil";
    //     }
    // }

    public class LuaBool : LuaValue
    {
        public LuaBool(bool b)
        {
            type = "bool";
            data = b;
        }

        public static implicit operator LuaBool(bool value)
        {
            return new LuaBool(value);
        }

        public override string DumpCode()
        {
            return data.Equals(true) ? "true" : "false";
        }
    }

    public class LuaFunction : LuaString
    {
        public LuaFunction(string str) : base(str)
        {
            type = "function";
        }

        public override string DumpCode()
        {
            return data.ToString();
        }
    }

    /*  
    支持所解析的 lua BNF, function只解析出其函数体作字符串:
        Exp ::= nil | false | true | Nunber | String | TableconStructor | FunctionDef
        TableconStructor ::= ‘{’ [FieldList] ‘}’
        FieldList ::= Field {FieldSep Field} [FieldSep]
        Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp
        FieldSep ::= ‘,’ | ‘;’
        FunctionDef ::= function * end
    */
    public static LuaValue FromLua(string code, Settings settings=null)
    {
        if (string.IsNullOrEmpty(code))
            return null;
       
        LuaUtility.settings = settings;
        LuaValue ret;
        try
        {
            ret = FromLuaInternalCode(code);
        }
        catch (System.Exception)
        {
            Debug.LogFormat("LuaUtility[parse lua code error] :{0}", cur_ls_in_parse_for_debug.Dump());
            throw;
        }
        return ret;
    }

    private static LuaValue FromLuaInternalCode(string code)
    {
        Log("FromLuaValueInternal code:"+code);
        LexState ls = new LexState(code);
        cur_ls_in_parse_for_debug = ls;
        ls.ReadAheadToken();
        LuaValue ret = null;
        Log("FromLuaInternal ls.t.token : "+ls.t.token+" "+ls.lookahead.token);
        if (ls.t.token == (int)KeyWord.Return && ls.lookahead.token == (int)'{')
        {
            ls.MoveToNextToken();
            ret = ParseExpCode(ls);
        }
        else if (ls.t.token == (int)'{')
        {
            ret = ParseExpCode(ls);
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
                ret = ParseExpCode(ls);
            }
        }
        return ret;
    }

    private static LuaValue ParseExpCode(LexState ls)
    {
        Token t = ls.t;
        switch (t.token)
        {
            case (int)KeyWord.String:
            {
                ls.MoveToNextToken();
                return new LuaString(t.str);
            }
            case (int)KeyWord.Name:
            {
                ls.MoveToNextToken();
                return new LuaString(t.str);
            }   
            case (int)KeyWord.FLOAT:
            {
                ls.MoveToNextToken();
                return new LuaFloat(t.d);
            }
            case (int)KeyWord.Integer:
            {
                ls.MoveToNextToken();
                return new LuaInteger(t.i);
            }
            case (int)KeyWord.Nil:
            {
                ls.MoveToNextToken();
                // return new LuaNil();
                return null;
            }
            case (int)KeyWord.False:
            {
                ls.MoveToNextToken();
                return new LuaBool(false);
            }
            case (int)KeyWord.True:
            {
                ls.MoveToNextToken();
                return new LuaBool(true);
            }
            case (int)'{':
            {
                return ParseTableConstructorCode(ls);
            }
            case (int)KeyWord.Function:
            {
                return ParseFunctionDefCode(ls);
            }
            case (int)'-':
            {
                ls.MoveToNextToken();
                t = ls.t;
                ls.MoveToNextToken();
                if (t.token == (int)KeyWord.FLOAT)
                    return new LuaFloat(-t.d);
                else if (t.token == (int)KeyWord.Integer)
                    return new LuaInteger(-t.i);
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

    private static LuaTable ParseTableConstructorCode(LexState ls)
    {
        Token t = ls.t;
        if (t.token == (int)'{')
        {
            ls.MoveToNextToken();
            LuaTable ret = new LuaTable();
            var isOk = ParseFieldListCode(ls, ref ret);
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

    private static LuaFunction ParseFunctionDefCode(LexState ls)
    {
        var start_i = ls.code_i;
        var end_i = start_i;
        ls.MoveToNextToken();
        if (ls.t.token != (int)'(')
        {
            ThrowError("wrong function def! wait for '(' ls:" + ls.Dump());
            return null;
        }
        ls.MoveToNextToken();
        var leftEndNum = 1;
        while (true)
        {
            Token t = ls.t;
            var token = t.token;
            if (token == (int)KeyWord.End)
            {
                leftEndNum--;
                if (leftEndNum <= 0)
                {
                    end_i = ls.code_i;
                    ls.MoveToNextToken();
                    break;
                }
            }
            else if (token == (int)KeyWord.Function || 
                token == (int)KeyWord.IF || 
                token == (int)KeyWord.While ||
                token == (int)KeyWord.For)
            {
                leftEndNum++;//每加一个这种关键字就需要多一个end
            }
            else if (token == (int)KeyWord.EOZ)
            {
                ThrowError("wrong function def! wait for keyword ‘end’ ls:" + ls.Dump());
                return null;
            }
            ls.MoveToNextToken();
        }
        start_i = start_i - "function".Length;
        var funcStr = ls.code.Substring(start_i, end_i-start_i);
        var func = new LuaFunction(funcStr);
        return func;
    }

    private static bool ParseFieldListCode(LexState ls, ref LuaTable obj)
    {
        Log(string.Format("ParseFieldListValue obj:{0} ls:{1}", obj, ls.Dump()));
        int index = 0;
        var isSep = false;
        do 
        {
            if (ls.t.token == (int)'}')
                break;
            ParseFieldCode(ls, ref obj, ref index);
            // index++;
            isSep = IsFieldSep(ls.t);
            if (isSep)
                ls.MoveToNextToken();
        } while (isSep);
        return true;
    }

    //Field ::= ‘[’ Exp ‘]’ ‘=’ Exp | Name ‘=’ Exp | Exp
    private static void ParseFieldCode(LexState ls, ref LuaTable obj, ref int index)
    {
        Token t = ls.t;
        if (t.token == (int)'[')
        {
            ls.MoveToNextToken();
            var keyName = ParseExpCode(ls);
            Assert.IsTrue(ls.t.token == (int)']');
            ls.MoveToNextToken();
            Assert.IsTrue(ls.t.token == (int)'=');
            ls.MoveToNextToken();
            var value = ParseExpCode(ls);
            var keyObj = new LuaTable.Key(keyName, true);
            obj.AddCode(keyObj, value);

        }
        else if (t.token == (int)KeyWord.Name)
        {
            var keyName = new LuaString(t.str);
            ls.MoveToNextToken();
            Assert.IsTrue(ls.t.token == (int)'=');
            ls.MoveToNextToken();
            var value = ParseExpCode(ls);
            var keyObj = new LuaTable.Key(keyName, false);
            obj.AddCode(keyObj, value);
        }
        else
        {
            index += 1;
            var keyName = new LuaTable.Key(index, false);
            var value = ParseExpCode(ls);
            obj.AddCode(keyName, value);
        }
    }

}
