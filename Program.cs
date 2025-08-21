using System;
using System.IO;
using System.Text;
using DieselCompiler;

string path = "C:\\work\\C\\DieselCompiler\\DieselCompiler\\DieselCompiler\\code.txt";

using (StreamReader file = new StreamReader(path, Encoding.GetEncoding("utf-8")))
{
    // ファイルを読み込む
    string Codes = file.ReadToEnd();
    // トークン化
    Tokens tokens = Tokenizer.ParseCode(Codes);
    // コンパイル
    Compiler compiler = new Compiler();
    string compiledCode = compiler.OutCompiledCode(tokens);
    // 表示
    Console.WriteLine(compiledCode);
}