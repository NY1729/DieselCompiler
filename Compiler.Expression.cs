using System;
using System.Text;
using System.Collections.Generic;

namespace DieselCompiler
{
    internal partial class Compiler
    {
        internal string BuildSetCommand(
                                               Tokens tokens,
                                               List<string> vars,
                                               List<string> envs
                                               )
        {
            delay = 0; // 初期ディレイを設定
            // 変数名を取得
            int exprStart = 0;
            int exprEnd = tokens.Token.Count - 1;
            string dieselExpr = "";
            if (exprStart == exprEnd)
            {
                dieselExpr = ParseFactor(tokens, exprStart, exprEnd, vars, envs);
                return dieselExpr; // 変数名がない場合はそのまま返す
            }
            // 最高位から解析 (OR)
            dieselExpr = ParseOr(tokens, exprStart, exprEnd, vars, envs);
            return dieselExpr;

        }

        private string WrapDelay(string command, int delay)
        {
            int d = delay + base_delay;
            var commandBuilder = new StringBuilder();
            if (d >= 0)
            {
                commandBuilder.Append($"$M="); // ディレイ付きコマンド
                commandBuilder.Append(GetQuote(d));
                commandBuilder.Append(command);
                commandBuilder.Append(GetQuote(d));
            }
            else
            {
                commandBuilder.Append(command); // ディレイなし
            }


            return commandBuilder.ToString(); // ディレイなし
        }

        private static string GetQuote(int delay)
        {
            if (delay <= 0) return "";
            int count = (1 << (delay - 1)) - 1;
            return new string('"', count);
        }

        private static readonly Dictionary<string, string> OpMap = new() //トークン → Diesel 関数名
        {
            {"|",  "or" },
            {"^",  "xor"},
            {"&",  "and"},
            {"+",  "+"  },
            {"-",  "-"  },
            {"*",  "*"  },
            {"/",  "/"}
        };

        private string ParseOr(Tokens t, int lo, int hi,
                                       List<string> vars, List<string> envs) // 再帰下降パーサ (低→高)
        {
            int idx = FindTop(t, lo, hi, "|");
            return idx >= 0
                ? $"$({OpMap["|"]},{ParseOr(t, lo, idx - 1, vars, envs)},{ParseXor(t, idx + 1, hi, vars, envs)})"
                : ParseXor(t, lo, hi, vars, envs);
        }

        private string ParseXor(Tokens t, int lo, int hi,
                                        List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "^");
            return idx >= 0
                ? $"$({OpMap["^"]},{ParseXor(t, lo, idx - 1, vars, envs)},{ParseAnd(t, idx + 1, hi, vars, envs)})"
                : ParseAnd(t, lo, hi, vars, envs);
        }

        private string ParseAnd(Tokens t, int lo, int hi,
                                        List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "&");
            return idx >= 0
                ? $"$({OpMap["&"]},{ParseAnd(t, lo, idx - 1, vars, envs)},{ParseAddSub(t, idx + 1, hi, vars, envs)})"
                : ParseAddSub(t, lo, hi, vars, envs);
        }

        private string ParseAddSub(Tokens t, int lo, int hi,
                                           List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "+", "-");
            return idx >= 0
                ? $"$({OpMap[t.GetToken(idx)]},{ParseAddSub(t, lo, idx - 1, vars, envs)},{ParseMulDiv(t, idx + 1, hi, vars, envs)})"
                : ParseMulDiv(t, lo, hi, vars, envs);
        }

        private string ParseMulDiv(Tokens t, int lo, int hi,
                                           List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "*", "/");
            return idx >= 0
                ? $"$({OpMap[t.GetToken(idx)]},{ParseMulDiv(t, lo, idx - 1, vars, envs)},{ParseFactor(t, idx + 1, hi, vars, envs)})"
                : ParseFactor(t, lo, hi, vars, envs);
        }

        private string ParseFactor(Tokens t, int lo, int hi,
                                          List<string> vars, List<string> envs)
        {
            // 括弧
            if (t.GetToken(lo) == "(" && t.GetToken(hi) == ")")
                return ParseOr(t, lo + 1, hi - 1, vars, envs);

            string s = t.GetToken(lo);
            if (GetDelay.ContainsKey(s)) // 変数名がディレイを持つ場合
            {
                GetDelay[s] += 1; // ディレイを追加
                if (delay >= 0)          // 条件ではないとき
                    delay = Math.Max(GetDelay[s], delay);

            }
            if (vars.Contains(s)) return $"$(getvar,{s})";
            if (envs.Contains(s)) return $"$(getenv,{s})";
            return s; // 数値・シンボル等
        }

        private static int FindTop(Tokens t, int lo, int hi, params string[] ops)
        {
            int depth = 0;
            for (int i = hi; i >= lo; --i)
            {
                string tok = t.GetToken(i);
                switch (tok)
                {
                    case ")": depth++; break;
                    case "(": depth--; break;
                    default:
                        if (depth == 0 && Array.IndexOf(ops, tok) >= 0)
                            return i;
                        break;
                }
            }
            return -1;
        }

    }
}