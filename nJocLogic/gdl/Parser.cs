using System;
using System.Collections.Generic;
using System.IO;

namespace nJocLogic.gdl
{
    public class Parser
    {
        readonly SymbolTable _symbolTable;

        public int TokRole { get; private set; }
        public int TokInit { get; private set; }
        public int TokTrue { get; private set; }
        public int TokDoes { get; private set; }
        public int TokNext { get; private set; }
        public int TokLegal { get; private set; }
        public int TokGoal { get; private set; }
        public int TokTerminal { get; private set; }
        public int TokDistinct { get; private set; }
        public int TokNil { get; private set; }

        // GDL operators
        public int TokImpliedby { get; private set; }
        public int TokOrOp { get; private set; }
        public int TokNotOp { get; private set; }

        internal Parser()
        {
            _symbolTable = new SymbolTable();
            InitSymbolTable();
        }

        public SymbolTable SymbolTable
        {
            get { return _symbolTable; }
        }

        private void InitSymbolTable()
        {
            TokRole = _symbolTable["role"];
            TokInit = _symbolTable["init"];
            TokTrue = _symbolTable["true"];
            TokDoes = _symbolTable["does"];
            TokNext = _symbolTable["next"];
            TokLegal = _symbolTable["legal"];
            TokGoal = _symbolTable["goal"];
            TokTerminal = _symbolTable["terminal"];
            TokDistinct = _symbolTable["distinct"];
            TokNil = _symbolTable["nil"];

            TokImpliedby = _symbolTable["<="];
            TokOrOp = _symbolTable["or"];
            TokNotOp = _symbolTable["not"];
        }

        public void Reset()
        {
            _symbolTable.Clear();
            InitSymbolTable();
        }

        public GdlList Parse(string input)
        {
            return Parse(new StringReader(input));            
        }

        public GdlList Parse(TextReader input)
        {            
            var lexer = new Lexer(input, _symbolTable);

            // Top-level is a list of expressions.
            var exprs = new List<GdlExpression>();

            while (true)
            {
                int t = lexer.Token();

                if (t == -1)
                    break;

                lexer.Unget(t);
                exprs.Add(ParseExpression(lexer));
            }
            return new GdlList(_symbolTable, exprs.ToArray());
        }

        private GdlExpression ParseExpression(Lexer lexer)
        {
            // Get the token:
            int t = lexer.Token();

            // If it's a (, then we must have a new expression
            if (t == '(')
                return ParseList(lexer);

            // If it's an identifier, then we have an atom
            if (t > 255)
                return new GdlAtom(_symbolTable, t);

            // If it's a question mark, then we have a variable
            if (t == '?')
                return ParseVariable(lexer);

            ReportError("Expression: can't handle token " + t + " (char: " + (char)t + ")");
            return null;
        }

        private GdlList ParseList(Lexer lexer)
        {
            var arr = new List<GdlExpression>();

            int token;

            while ((token = lexer.Token()) != ')')
            {
                if (token == Lexer.Eof)
                {
                    ReportError("Unexpected end of file in parseList");
                }

                // put this token back, and parse the expression
                lexer.Unget(token);
                arr.Add(ParseExpression(lexer));
            }

            // Convert the ArrayList to an array, and return a new GdlList object.
            return new GdlList(_symbolTable, arr.ToArray());
        }

        private GdlVariable ParseVariable(Lexer lexer)
        {
            int token = lexer.Token();

            // Make sure we actually got an identifier
            if (token <= 255)
                ReportError("? token must be followed by an identifier token in variable parsing");

            return GdlVariable.GetGdlVariable(_symbolTable, token);
        }

        private static void ReportError(String str, Lexer lexer = null)
        {
            string message = lexer != null 
                            ? string.Format("Parser error (line {0}): {1}", lexer.GetLineNumber(), str) 
                            : string.Format("Parser error: {0}", str);

            throw new ApplicationException(message);
        }
    }

}
