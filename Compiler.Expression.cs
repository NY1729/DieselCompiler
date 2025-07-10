using System;
using System.Collections.Generic;

namespace DieselCompiler
{
    internal partial class Compiler
    {
        internal static string BuildSetCommand(
                                               Tokens tokens,
                                               List<string> vars,
                                               List<string> envs)
        {
            // 変数名を取得
            int exprStart = 0;
            int exprEnd = tokens.Token.Count - 1;

            // 最高位から解析 (OR)
            string dieselExpr = ParseOr(tokens, exprStart, exprEnd, vars, envs);
            return $"$M={dieselExpr}";
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

        private static string ParseOr(Tokens t, int lo, int hi,
                                       List<string> vars, List<string> envs) // 再帰下降パーサ (低→高)
        {
            int idx = FindTop(t, lo, hi, "|");
            return idx >= 0
                ? $"$({OpMap["|"]},{ParseOr(t, lo, idx - 1, vars, envs)},{ParseXor(t, idx + 1, hi, vars, envs)})"
                : ParseXor(t, lo, hi, vars, envs);
        }

        private static string ParseXor(Tokens t, int lo, int hi,
                                        List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "^");
            return idx >= 0
                ? $"$({OpMap["^"]},{ParseXor(t, lo, idx - 1, vars, envs)},{ParseAnd(t, idx + 1, hi, vars, envs)})"
                : ParseAnd(t, lo, hi, vars, envs);
        }

        private static string ParseAnd(Tokens t, int lo, int hi,
                                        List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "&");
            return idx >= 0
                ? $"$({OpMap["&"]},{ParseAnd(t, lo, idx - 1, vars, envs)},{ParseAddSub(t, idx + 1, hi, vars, envs)})"
                : ParseAddSub(t, lo, hi, vars, envs);
        }

        private static string ParseAddSub(Tokens t, int lo, int hi,
                                           List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "+", "-");
            return idx >= 0
                ? $"$({OpMap[t.GetToken(idx)]},{ParseAddSub(t, lo, idx - 1, vars, envs)},{ParseMulDiv(t, idx + 1, hi, vars, envs)})"
                : ParseMulDiv(t, lo, hi, vars, envs);
        }

        private static string ParseMulDiv(Tokens t, int lo, int hi,
                                           List<string> vars, List<string> envs)
        {
            int idx = FindTop(t, lo, hi, "*", "/");
            return idx >= 0
                ? $"$({OpMap[t.GetToken(idx)]},{ParseMulDiv(t, lo, idx - 1, vars, envs)},{ParseFactor(t, idx + 1, hi, vars, envs)})"
                : ParseFactor(t, lo, hi, vars, envs);
        }

        private static string ParseFactor(Tokens t, int lo, int hi,
                                          List<string> vars, List<string> envs)
        {
            // 括弧
            if (t.GetToken(lo) == "(" && t.GetToken(hi) == ")")
                return ParseOr(t, lo + 1, hi - 1, vars, envs);

            string s = t.GetToken(lo);
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

        private static int FindExpressionEnd(Tokens t, int start)
        {
            int depth = 0;
            for (int i = start; i < t.Token.Count; i++)
            {
                string tok = t.GetToken(i);
                switch (tok)
                {
                    case "(": depth++; break;
                    case ")": depth--; break;
                    case ";":
                    case "}":
                    case ":":
                        if (depth == 0) return i - 1;
                        break;
                }
            }
            throw new InvalidOperationException("unterminated expression"); // セミコロンなし行末まで
        }
    }
}