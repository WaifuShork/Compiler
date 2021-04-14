﻿using System;
using Compiler.CodeAnalysis.Lexing;
using Compiler.CodeAnalysis.Syntax.Expression;
using System.Collections.Generic;
using System.Linq;
using Compiler.CodeAnalysis.Syntax;

namespace Compiler.CodeAnalysis.Parsing
{
    internal sealed class Parser
    {
        private readonly SyntaxToken[] _tokens;
        private int _position;
        private SyntaxToken _current => Peek(0);
        private readonly List<string> _diagnostics = new();

        public Parser(string text)
        {
            var tokens = new List<SyntaxToken>();
            var lexer = new Lexer(text);
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                if (token.Kind != SyntaxKind.WhiteSpace || token.Kind != SyntaxKind.BadToken)
                {
                    tokens.Add(token);
                }
            }
            while (token.Kind != SyntaxKind.EndOfFile);
            _tokens = tokens.ToArray();
        }

        public IEnumerable<string> Diagnostics => _diagnostics;

        private SyntaxToken Peek(int offset)
        {
            var index = _position + offset;
            if (index >= _tokens.Length)
            {
                return _tokens[^1];
            }

            return _tokens[index];
        }

        private SyntaxToken NextToken()
        {
            var current = _current;
            _position++;
            return current;
        }

        private SyntaxToken MatchToken(SyntaxKind kind)
        {
            if (_current.Kind == kind)
            {
                return NextToken();
            }

            _diagnostics.Add($"Unexpected token: {_current.Kind}, expected {kind}");

            return new(kind, _current.Position, null, null);
        }

        public SyntaxTree Parse()
        {
            var expression = ParseExpression();
            var EOFToken = MatchToken(SyntaxKind.EndOfFile);

            return new(_diagnostics, expression, EOFToken);
        }

        //Allowing for proper operator precedence
        private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
        {

            ExpressionSyntax left;
            var unaryOperatorPrecedence = _current.Kind.GetUnaryOperatorPrecedence();

            //Allowing for unary operator precedence
            if(unaryOperatorPrecedence != 0 && unaryOperatorPrecedence >= parentPrecedence)
            {
                var operatorToken = NextToken();
                var operand = ParseExpression(unaryOperatorPrecedence);
                left = new UnaryExpressionSyntax(operatorToken, operand);
            }
            else
            {
                left = ParsePrimaryExpression();
            }
            
            //Keep looping until our precedence is <= parent precedence, or == 0;
            while(true)
            {
                var precedence = _current.Kind.GetBinaryOperatorPrecedence();
                
                if(precedence == 0 || precedence <= parentPrecedence)
                {
                    break;
                }
                
                //Taking the current token, and moving the index
                var operatorToken = NextToken();
                //Recursively calling the ParseExpression with the current precedence
                var right = ParseExpression(precedence);

                //Making left a New BinaryExpressionSyntax
                left = new BinaryExpressionSyntax(left, operatorToken, right);

            }
            return left;
        }


        private ExpressionSyntax ParsePrimaryExpression()
        {
            //Converted to switch before we get too many checks
            switch(_current.Kind)
            {
                //Parenthesis
                case SyntaxKind.OpenParenthesis:
                    var left = NextToken();
                    var expression = ParseExpression();
                    var right = MatchToken(SyntaxKind.CloseParenthesis);
                    return new ParenthesizedExpressionSyntax(left, expression, right);
                
                //Bools
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.FalseKeyword:
                    var keywordToken = NextToken();
                    var value = keywordToken.Kind == SyntaxKind.TrueKeyword;
                    return new LiteralExpressionSyntax(keywordToken, value);

                //Assuming default is a number token
                default:
                    var numberToken = MatchToken(SyntaxKind.NumberToken);
                    return new LiteralExpressionSyntax(numberToken);

            }       
        }

        public static void PrettyPrint(SyntaxNode node, string indent = "", bool isLast = true)
        {
            var marker = isLast ? "└───" : "├───";

            Console.Write(indent + marker + node.Kind);

            if (node is SyntaxToken t && t.Value != null)
            {
                Console.Write(" " + t.Value);
            }
            Console.WriteLine();
            indent += isLast ? "    " : "│   ";

            var lastChild = node.GetChildren().LastOrDefault();
            foreach (var child in node.GetChildren())
            {
                PrettyPrint(child, indent, child == lastChild);
            }
        }
    }
}
