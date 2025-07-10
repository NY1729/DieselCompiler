using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler;

internal class Compiler
{

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
            string tk = tokens.GetToken(pc);
            if (tk == ";") continue; // セミコロンは無視)

            switch (tk)
            {
                case "if":
                    {
                        ifStatement();
                        continue;
                    }
                case "var": // システム変数宣言
                    {
                        ++pc;
                        VariableList.Add(tokens.GetToken(pc));
                        continue;
                    }
                case "env": // 変数宣言
                    {
                        ++pc;
                        EnvList.Add(tokens.GetToken(pc));
                        continue;
                    }
                case "din": // 標準入力を取得する
                    {
                        ++pc; // '>>'
                        ++pc; // 変数名
                        string variable = tokens.GetToken(pc);
                        string command = GetType(tokens.GetToken(pc));

                        string SetCommand = $"set{command};{variable};\\";
                        codeBuilder.Append(SetCommand);
                        continue;
                    }
                case "input":
                    {
                        ++pc;
                        codeBuilder.Append("\\");
                        continue;
                    }
                case "cancel":
                    {
                        codeBuilder.Append("^C^C");
                        continue;
                    }
                case ":":
                    {
                        codeBuilder.Append(";");
                        continue;
                    }

            }

            // 変数と環境変数のチェック
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

            string GetCondition() // Conditionを取得する
            {
                string cond = "";
                pc++;                    // pcは '('  を指している
                pc++;                    // pcは 'lhs or 条件' を指している
                if (tokens.GetToken(pc + 1) != ")") // 条件式がある場合
                {
                    // 1) 左辺
                    string lhs = GetValue(pc);

                    // 2) 演算子
                    pc++;   // pcは演算子を指している
                    string op = tokens.GetToken(pc);

                    // 3) 右辺
                    pc++; // pcは rhs を指している
                    string rhs = GetValue(pc);

                    pc++;   // pcは ')' を指している
                    pc++;   // pcは '{' を指している

                    cond = $"$({op},{lhs},{rhs})";
                }
                else
                {
                    cond = GetValue(pc); // 条件式がない場合はトークンをそのまま使用
                    pc++; // pcは ')' を指している
                    pc++; // pcは '{' を指している
                }
                return cond;
            }

            string GetCase() // Caseブロックを取得する
            {
                int CaseStart = pc++;                     // '{' をスキップ
                int branchDepth = 1; // ブロックの深さを追跡
                var Case = new StringBuilder();
                while (branchDepth > 0)
                {
                    if (tokens.GetToken(pc) == "{")
                    {
                        branchDepth++; // 新しいブロックの開始
                    }
                    else if (tokens.GetToken(pc) == "}")
                    {
                        branchDepth--; // ブロックの終了
                        if (branchDepth == 0) break; // 最初のブロックが終了したらループを抜ける
                    }
                    pc++;
                }

                int CaseEnd = pc; // pc は '}' を指している
                                  //
                pc++; // '}' をスキップ
                if (CaseStart + 1 < CaseEnd - 1) // Caseブロックが空でない場合
                    Case.Append(CompileBlock(tokens, CaseStart + 1, CaseEnd - 1, VariableList, EnvList));
                return Case.ToString();

            }

            void ifStatement() // if ( 条件文 ) {}
            {
                string cond = GetCondition(); // 条件を取得

                string trueCase = GetCase(); // Trueブロックを取得

                string falseCase = "";
                if (pc + 1 < tokens.Token.Count && tokens.GetToken(pc) == "else")
                {
                    pc++; // 'else' をスキップ
                    falseCase = GetCase(); // Falseブロックを取得
                }
                pc++; // '}' をスキップ
                codeBuilder.Append($"$M=$(if,{cond},{trueCase},{falseCase})");
            }

            string GetType(string variable)
            {
                if (VariableList.Contains(variable))
                    return "var";
                else if (EnvList.Contains(variable))
                    return "env";
                else
                    return "none";
            }

            string GetValue(int pc1)
            {
                string variable = tokens.GetToken(pc1); // 変数名を取得
                string command = GetType(variable);

                if (command == "none")
                    return variable; // 変数が存在しない場合はそのまま返す

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
