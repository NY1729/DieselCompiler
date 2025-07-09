using System;
using System.IO;
using System.Text;
using DieselCompiler;

string path = "./code.txt";

using (StreamReader file = new StreamReader(path, Encoding.GetEncoding("utf-8")))
{
    // ファイルを読み込む
    string Codes = file.ReadToEnd();
    // トークン化
    Tokens tokens = Tokenizer.ParseCode(Codes);
    // コンパイル
    string compiledCode = Compiler.OutCompiledCode(tokens);
    // 表示
    Console.WriteLine(compiledCode);
}