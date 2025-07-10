using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler;

internal class Tokenizer
{
    private static readonly HashSet<char> Delims = new("(){}[];:,@");
    private static readonly HashSet<char> OpChars = new("=+-*/!%&~|<>?.#");

    private static readonly HashSet<char> EmptyChars = new()
    {
        ' ', '\t', '\n', '\r'
    };

    static public Tokens ParseCode(string Code)
    {
        Tokens tokens = new Tokens();

        int GetToken(string code)
        {
            if (!tokens.TokenList.Contains(code))
            {
                tokens.TokenList.Add(code);
            }
            return tokens.TokenList.IndexOf(code);
        }


        string stack = "";

        for (int i = 0; i < Code.Length; ++i)
        {
            if (EmptyChars.Contains(Code[i]))
            {   // スペース、タブ、改行をスキップ
                continue;
            }
            stack = "";
            if (Delims.Contains(Code[i]))
            {
                stack += Code[i];
                tokens.Token.Add(GetToken(stack));
                continue;
            }
            else if (char.IsLetterOrDigit(Code[i]) || Code[i] == '_')
            {
                // 文字列を一つのトークンとして扱う
                while (i < Code.Length && (char.IsLetterOrDigit(Code[i]) || Code[i] == '_' || Code[i] == '.'))
                {
                    stack += Code[i];
                    ++i;
                }
                --i;
                tokens.Token.Add(GetToken(stack));
                continue;
            }
            else if (Code[i] == '/' && Code[i + 1] == '/')
            {
                // コメントをスキップ
                while (i < Code.Length && Code[i] != '\n')
                {
                    ++i;
                }
                continue;
            }
            else if (OpChars.Contains(Code[i]))
            {
                // 演算子を一つのトークンとして扱う
                while (i < Code.Length && OpChars.Contains(Code[i]))
                {
                    stack += Code[i];
                    ++i;
                }
                --i;
                tokens.Token.Add(GetToken(stack));
                continue;
            }


        }
        return tokens;
    }
}
