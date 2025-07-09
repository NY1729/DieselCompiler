using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler;

internal class Compiler
{
    enum VarType
    {
        Variable,
        Environment
    }

    private static string CompileBlock(Tokens tokens, int start, int end, List<string> vars, List<string> envs)
    {
        Tokens sliceTokens = new Tokens(tokens.Token.GetRange(start, end - start + 1), tokens.TokenList);
        return OutCompiledCode(sliceTokens, vars, envs);
    }



    public static string OutCompiledCode(Tokens tokens, List<string>? inheritaedVariables = null, List<string>? inheritedEnvironments = null)
    {
        List<string> VariableList = inheritaedVariables ?? new List<string>();
        List<string> EnvList = inheritedEnvironments ?? new List<string>();
        var codeBuilder = new StringBuilder();

        for (int pc = 0; pc < tokens.Token.Count; pc++)
        {


            /* セミコロンは無視 */
            if (tokens.GetToken(pc) == ";") continue;

            string tk = tokens.GetToken(pc);


            /* ---------- if 文 ---------- */
            if (tk == "if")
            {
                string cond = "";
                pc++;                    // '(' 
                pc++;                    // lhs or 条件
                if (tokens.GetToken(pc + 1) != ")") // 条件式がある場合
                {
                    // 1) 左辺
                    string lhs = GetValue(pc);

                    // 2) 演算子
                    pc++;
                    string op = tokens.GetToken(pc);

                    // 3) 右辺
                    pc++;
                    string rhs = GetValue(pc);

                    pc++;
                    pc++;   // ')' 自体も飛ばす

                    cond = $"$({op},{lhs},{rhs})";
                }
                else
                {
                    string condRaw = tokens.GetToken(pc); // 条件式がない場合はトークンをそのまま使用
                    cond = GetValue(pc);
                    pc++; // ')'
                    pc++; // '{' 
                }
                // 2) true ブロック
                int trueStart = pc++;                     // '{' をスキップ
                int branchDepth = 1; // ブロックの深さを追跡
                var trueCase = new StringBuilder();
                while (branchDepth > 0)
                {
                    pc++;
                    if (tokens.GetToken(pc) == "{")
                    {
                        branchDepth++; // 新しいブロックの開始
                    }
                    else if (tokens.GetToken(pc) == "}")
                    {
                        branchDepth--; // ブロックの終了
                        if (branchDepth == 0) break; // 最初のブロックが終了したらループを抜ける
                    }
                }

                int trueEnd = pc;

                ++pc; // '}' をスキップ
                trueCase.Append(CompileBlock(tokens, trueStart + 1, trueEnd - 1, VariableList, EnvList));
                // 3) else があるか？
                var falseCase = new StringBuilder();
                if (pc + 1 < tokens.Token.Count && tokens.GetToken(pc) == "else")
                {
                    pc++; // 'else' をスキップ
                    int falseStart = pc++;                     // '{' をスキップ
                    branchDepth = 1; // ブロックの深さを追跡
                    while (branchDepth > 0)
                    {
                        pc++;
                        if (tokens.GetToken(pc) == "{")
                        {
                            branchDepth++; // 新しいブロックの開始
                        }
                        else if (tokens.GetToken(pc) == "}")
                        {
                            branchDepth--; // ブロックの終了
                            if (branchDepth == 0) break; // 最初のブロックが終了したらループを抜ける
                        }
                    }       // '}'
                    int falseEnd = pc;
                    falseCase.Append(CompileBlock(tokens, falseStart + 1, falseEnd - 1, VariableList, EnvList));
                }
                ++pc; // '}' をスキップ
                codeBuilder.Append($"$M=$(if,{cond},{trueCase},{falseCase})");
                continue;                 // 余計な追加を防止
            }
            else if (tk == "var")
            {
                ++pc;
                VariableList.Add(tokens.GetToken(pc));
                continue;
            }
            else if (tk == "env")
            {
                ++pc;
                EnvList.Add(tokens.GetToken(pc));
                continue;
            }
            if (VariableList.Contains(tk) || EnvList.Contains(tk))
            {
                if (tokens.GetToken(pc + 1) == "=")
                {
                    // 変数の値をセットする
                    codeBuilder.Append(SetValue(pc));
                    pc += 2; // '=' と値をスキップ
                }
                else if (tokens.GetToken(pc + 1) == ";")
                {
                    // 変数の値を取得する
                    codeBuilder.Append(GetValue(pc));
                }
                continue;
            }

            string GetType(string variable)
            {
                if (VariableList.Contains(variable))
                    return "var";
                else if (EnvList.Contains(variable))
                    return "env";
                else
                    return "";
            }

            string GetValue(int pc1)
            {
                string variable = tokens.GetToken(pc1); // 変数名を取得
                string command = GetType(variable);

                string GetCommand = $"$(get{command},{variable})";
                return GetCommand;
            }
            string SetValue(int pc1)
            {
                string variable = tokens.GetToken(pc1); // 変数名を取得
                string command = GetType(variable);

                string value = tokens.GetToken(pc1 + 2); // 値を取得
                string SetCommand = $"set{command};{variable};{value};";
                return SetCommand;
            }

            codeBuilder.Append(tk);
        }

        return codeBuilder.ToString();
    }
}
