using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler;

internal partial class Compiler
{

    private static readonly HashSet<string> StopSymbol = new()
{
    "(", ")", "{", "}", "[", "]", ";", ":", ","
};
    private static Tokens GetSliceTokens(Tokens tokens, int start, int end)
    {
        if (start < 0 || end >= tokens.Token.Count || start > end)
            throw new ArgumentOutOfRangeException("Invalid range for slicing tokens.");

        return new Tokens(tokens.Token.GetRange(start, end - start + 1), tokens.TokenList);
    }
    private static string CompileBlock(Tokens tokens, int start, int end, List<string> vars, List<string> envs)
    {
        Tokens sliceTokens = GetSliceTokens(tokens, start, end);
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
                case "nth":
                    {
                        nthStatement();
                        continue;
                    }
                case "var": // システム変数宣言
                    {
                        pc++;
                        VariableList.Add(tokens.GetToken(pc));
                        continue;
                    }
                case "env": // 変数宣言
                    {
                        pc++;
                        EnvList.Add(tokens.GetToken(pc));
                        continue;
                    }
                case "din": // 標準入力を取得する
                    {
                        pc++; // '>>'
                        pc++; // 変数名
                        string variable = tokens.GetToken(pc);
                        string command = GetType(tokens.GetToken(pc));

                        string SetCommand = $"set{command};{variable};\\";
                        codeBuilder.Append(SetCommand);
                        continue;
                    }
                case "input":
                    {
                        pc++;
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


            if (VariableList.Contains(tk) || EnvList.Contains(tk) || double.TryParse(tk, out _)) // 変数名または環境変数名、または整数の場合
            {

                if (tokens.GetToken(pc + 1) == "=")
                {
                    string variavle = tokens.GetToken(pc);

                    string command = GetType(tokens.GetToken(pc));
                    int start = pc + 2; // '=' の次のトークンから開始
                    SkipUntil(); // ';'までスキップ
                    Tokens sliceTokens = GetSliceTokens(tokens, start, pc);

                    string Value = BuildSetCommand(sliceTokens, VariableList, EnvList);
                    string SetCommand = $"set{command};{variavle};{Value};";
                    // 変数の値をセットする
                    codeBuilder.Append(SetCommand);

                }
                else
                {
                    int start = pc; // '=' の次のトークンから開始
                    SkipUntil(); // ';'までスキップ
                    Tokens sliceTokens = GetSliceTokens(tokens, start, pc);

                    string Value = BuildSetCommand(sliceTokens, VariableList, EnvList);
                    codeBuilder.Append(Value);
                }
                pc--; //Skipしたシンボルを戻す
                continue;
            }

            codeBuilder.Append(tk);

            void SkipUntil() // StopSymbol までスキップ
            {

                while (pc < tokens.Token.Count && !StopSymbol.Contains(tokens.GetToken(pc)))
                {
                    pc++;
                }
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

            void nthStatement()
            {
                string cond = GetCondition(); // 条件を取得
                List<string> Cases = new List<string>();
                pc++; // pcは 'case' を指している
                while (pc + 1 < tokens.Token.Count && tokens.GetToken(pc) == "case")
                {
                    pc++;
                    Cases.Add(GetCase()); // Caseブロックを取得
                }
                codeBuilder.Append($"$M=$(nth,{cond}");
                for (int i = 0; i < Cases.Count; i++)
                {
                    codeBuilder.Append($",{Cases[i]}");
                }
                codeBuilder.Append($")");
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

        }

        return codeBuilder.ToString();
    }
}
