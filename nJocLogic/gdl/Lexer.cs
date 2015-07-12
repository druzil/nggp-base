using NLog;
using System.Collections.Generic;
using System.Text;
using System;
using System.IO;

namespace nJocLogic.gdl
{
    public class Lexer
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.gdl");

        private readonly TextReader _in;
        private int _lineNumber;
        private readonly SymbolTable _symbolTable;
        private readonly Stack<int> _bufferedTokens;

        int _markedCharacter;
        private const int NoMark = -2;

        StringBuilder _identBuf;

        public const int Eof = -1;

        public Lexer(TextReader input, SymbolTable symbolTable)
        {
            _in = input;

            _symbolTable = symbolTable;
            _lineNumber = 0;

            _bufferedTokens = new Stack<int>(2);

            _markedCharacter = NoMark;
        }

        public void Unget(int token)
        {
            _bufferedTokens.Push(token);
        }

        /**
         * Get the next token. Returns -1 for EOF. A character literal
         * between 0 and 255 is returned as is. Numbers 256 and above
         * are for reserved tokens.
         * 
         * @return The value of the next token, or -1 for EOF.
         */
        public int Token()
        {
            int token = GetNextToken();

            if (Logger.IsDebugEnabled)
            {
                if (token == Eof)
                {
                    Logger.Debug("Lexer got EOF");
                }
                else if (token < 256)
                {
                    Logger.Debug("Lexer got character '" + (char)token + "' (ascii " + token + ")");
                }
                else
                {
                    Logger.Debug("Lexer got token '" + _symbolTable[token] + "' (token #" + token + ")");
                }
            }

            return token;
        }

        private int GetNextToken()
        {
            // If we have a buffered token, return it
            if (_bufferedTokens.Count > 0)
                return _bufferedTokens.Pop();

            try
            {
                while (true)
                {
                    int c;
                    if ((c = _markedCharacter) == NoMark)
                        c = _in.Read();
                    else
                        _markedCharacter = NoMark;

                    if (c == -1)
                        return Eof;

                    if (c == '\n')
                        _lineNumber++;
                    else if (c == ';')  // Comments
                    {
                        while (!IsNewline((char)(c = _in.Read())))
                        {
                        }

                        if (c != Eof)
                            _markedCharacter = c;
                    }

                    // Block comments:
                    else if (c == '#')
                    {
                        c = _in.Read();

                        if (c != '|')
                            ReportError("# must be followed by a |, not " + c + " (char: " + (char)c + ")");

                        // read until we get a "|#"                    
                        bool gotBar = false;
                        while (true)
                        {
                            c = _in.Read();

                            if (c == Eof)
                                ReportError("EOF in block comment");

                            if (gotBar && c == '#')
                                break;

                            gotBar = c == '|';
                        }
                    }


                    else if (char.IsWhiteSpace((char)c)) // Ignore white-space.
                    { }

                    // Return parentheses and question marks as-is
                    else if (c == '(' || c == ')' || c == '?')
                        return c;

                    // Identifiers
                    else if (IsIdentifierChar((char)c))
                    {
                        // make a buffer of size 32
                        _identBuf = new StringBuilder(32);
                        _identBuf.Append(char.ToLower((char)c));

                        while (IsIdentifierChar((char)(c = _in.Read())))
                            _identBuf.Append(char.ToLower((char)c));

                        // If we didn't read EOF, then be sure to return to that character
                        if (c != Eof)
                            _markedCharacter = c;

                        // Look up the identifier in our symbol table
                        int token = _symbolTable[_identBuf.ToString()];

                        return token;
                    }
                    else
                        ReportError("Cannot handle character: " + c + " (char: " + (char)c + ")");
                }
            }
            catch (IOException e)
            {
                Logger.Fatal("I/O error: " + e.Message);
                return Eof;
            }
        }

        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '<' || c == '>' || c == '=' || c == '_' || c == '-' || c == '.' || c == '+'; 
        }

        private static bool IsNewline(char c)
        {
            return c == '\n' || c == '\r';
        }

        public int GetLineNumber()
        {
            return _lineNumber;
        }

        private void ReportError(string str)
        {
            string message = "Lexer error (line " + _lineNumber + "): " + str;
            throw new ApplicationException(message);
        }
    }

}
