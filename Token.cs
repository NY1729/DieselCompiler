using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DieselCompiler;

internal class Tokens
{
    public string GetToken(int pc)
    {
        return this.TokenList[Token[pc]];
    }

    public Tokens(List<int> Token, List<string> TokenList)
    {
        this.Token = Token;
        this.TokenList = TokenList;
    }
    public Tokens()
    {
        this.Token = new List<int>();
        this.TokenList = new List<string>();
    }
    public List<string> TokenList;
    public List<int> Token;
}
