using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler
{

    internal partial class Compiler
    {
        int base_delay = 0; // 基本ディレイ
        int delay = 0;
        private static readonly HashSet<string> StopSymbol = new HashSet<string>()
{
    "(", ")", "{", "}", "[", "]", ";", ":", ",","==",">=",">","<=","<","!="
};
        private Tokens GetSliceTokens(Tokens tokens, int start, int end)
        {
            if (start < 0 || end >= tokens.Token.Count || start > end)
                throw new ArgumentOutOfRangeException("Invalid range for slicing tokens.");

            return new Tokens(tokens.Token.GetRange(start, end - start + 1), tokens.TokenList);
        }
        private string CompileBlock(Tokens tokens, int start, int end, List<string> vars, List<string> envs)
        {
            Tokens sliceTokens = GetSliceTokens(tokens, start, end);
            return OutCompiledCode(sliceTokens, vars, envs);
        }



        Dictionary<string, int> GetDelay = new Dictionary<string, int>()
        {
            {"cmdactive", 0}, // コマンドがアクティブな状態かどうか
        }; // 変数がどれぐらい遅延しているのかを保持する

        public string OutCompiledCode(Tokens tokens, List<string>? inheritaedVariables = null, List<string>? inheritedEnvironments = null)
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
                            string variable = tokens.GetToken(pc);
                            GetDelay[variable] = 0; // 初期ディレイを設定
                            EnvList.Add(variable);
                            continue;
                        }
                    case "dout":
                        {
                            pc++; // pcは '<<' を指している
                            pc++; // pcは 出力内容 を指している
                            for (; pc < tokens.Token.Count && tokens.GetToken(pc) != ";"; pc++)
                            {
                                codeBuilder.Append(tokens.GetToken(pc));
                            }
                            codeBuilder.Append("^X"); // 出力の終端を示す
                            continue;
                        }
                    case "din": // 標準入力を取得する
                        {
                            if (delay > 0) delay++;
                            pc++; // pcは '>>' を指している
                            pc++; // pcは 変数名 を指している
                            string variable = tokens.GetToken(pc);
                            string command = GetType(tokens.GetToken(pc));
                            GetDelay[variable] += 1 + base_delay; // 入力のディレイを追加
                            string SetCommand = $"set{command};{variable};\\";
                            codeBuilder.Append(SetCommand);
                            continue;
                        }
                    case "input":
                        {
                            if (delay > 0) delay++;
                            GetDelay["cmdactive"] += 1; // 入力のディレイを追加
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
                    case "Loop":
                        {
                            codeBuilder.Append("*^C^C");
                            continue;
                        }
                }



                if (VariableList.Contains(tk) || EnvList.Contains(tk) || double.TryParse(tk, out _)) // 変数名または環境変数名、または整数の場合
                {

                    if (tokens.GetToken(pc + 1) == "=")
                    {
                        string variavle = tokens.GetToken(pc);
                        GetDelay[variavle] += 1 + base_delay; // 変数のディレイを追加
                        string command = GetType(tokens.GetToken(pc));
                        pc += 2; // '=' の次のトークンから開始
                        string Value = GetValue(); // 値を取得
                        string SetCommand = $"'set{command};{variavle};{Value};";
                        // 変数の値をセットする
                        codeBuilder.Append(SetCommand);

                    }
                    else
                    {
                        string Value = GetValue(); // 値を取得
                        string command = WrapDelay(Value, delay);
                        codeBuilder.Append(command);
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
                    string lhs = GetValue();
                    if (tokens.GetToken(pc) != ")") // 条件式がある場合
                    {
                        // 2) 演算子
                        string op = tokens.GetToken(pc);

                        // 3) 右辺
                        pc++; // pcは rhs を指している
                        string rhs = GetValue();

                        pc++;   // pcは '{' を指している

                        cond = $"$({op},{lhs},{rhs})";
                    }
                    else
                    {
                        cond = lhs; // 条件式がない場合はトークンをそのまま使用
                        pc++;   // pcは '{' を指している
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
                    pc++; // '}' をスキップ
                    if (CaseStart + 1 < CaseEnd - 1) // Caseブロックが空でない場合
                        Case.Append(CompileBlock(tokens, CaseStart + 1, CaseEnd - 1, VariableList, EnvList));
                    return Case.ToString();

                }

                void ifStatement() // if ( 条件文 ) {}
                {
                    string cond = GetCondition(); // 条件を取得
                    int cond_delay = delay;
                    string trueCase = GetCase(); // Trueブロックを取得
                    string falseCase = "";
                    if (pc + 1 < tokens.Token.Count && tokens.GetToken(pc) == "else")
                    {
                        pc++; // 'else' をスキップ
                        falseCase = GetCase(); // Falseブロックを取得
                    }
                    --pc; // pcは '}' を指している
                    codeBuilder.Append(WrapDelay($"$(if,{cond},{trueCase},{falseCase})", cond_delay));

                }

                void nthStatement()
                {
                    int current_delay = delay;
                    var nthCommand = new StringBuilder();
                    string cond = GetCondition(); // 条件を取得
                    List<string> Cases = new List<string>();
                    pc++; // pcは 'case' を指している
                    int stacked_delay = delay;
                    while (pc + 1 < tokens.Token.Count && tokens.GetToken(pc) == "case")
                    {
                        pc++;
                        Cases.Add(GetCase()); // Caseブロックを取得
                        stacked_delay = Math.Max(delay, stacked_delay);
                        delay = current_delay;
                    }
                    delay = current_delay;
                    nthCommand.Append($"$(nth,{cond}");
                    for (int i = 0; i < Cases.Count; i++)
                    {
                        nthCommand.Append($",{Cases[i]}");
                    }
                    nthCommand.Append($")");
                    codeBuilder.Append(WrapDelay(nthCommand.ToString(), delay));
                    delay = stacked_delay;
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

                string GetValue()
                {
                    int start = pc;
                    SkipUntil(); // StopSymbolまでスキップ
                    if (start == pc)
                    {
                        return "";
                    }
                    Tokens sliceTokens = GetSliceTokens(tokens, start, pc - 1);

                    string Value = BuildSetCommand(sliceTokens, VariableList, EnvList);
                    return Value;
                }

            }

            return codeBuilder.ToString();
        }
    }
}