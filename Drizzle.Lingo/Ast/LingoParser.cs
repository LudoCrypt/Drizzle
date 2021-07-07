﻿using System;
using System.Collections.Generic;
using System.Linq;
using Pidgin;
using Pidgin.Expression;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace Drizzle.Lingo.Ast
{
    public static class LingoParser
    {
        private static readonly HashSet<string> Keywords = new()
        {
            "end",
            "on",
            "case",
            "if",
            "global",
            "menu",
            "repeat",
            "next",
            "else",
            "otherwise"
        };

        private static string PrintPos(SourcePos pos) => $"{pos.Line}:{pos.Col}";

        public static readonly Parser<char, Unit> Nop = Return(Unit.Value);

#if true
        private static Parser<char, T> TraceBegin<T>(this Parser<char, T> parser, string msg) =>
            CurrentPos.Trace(p => $"[{PrintPos(p)}] {msg}").Then(parser);

        private static Parser<char, T> TracePos<T>(this Parser<char, T> parser, string msg) =>
            parser.Before(CurrentPos.Trace(p => $"[{PrintPos(p)}] {msg}"));

#else
        private static Parser<char, T> TraceBegin<T>(this Parser<char, T> parser, string msg) => parser;

        private static Parser<char, T> TracePos<T>(this Parser<char, T> parser, string msg) => parser;
#endif

        private static readonly Parser<char, Unit> Comment =
            String("--")
                .Then(AnyCharExcept('\n').SkipMany())
                .Labelled("comment");

        private static readonly Parser<char, char> NnlWhiteSpace =
            OneOf(' ', '\t', '\r').Labelled("non-newline white space");

        public static readonly Parser<char, Unit> SkipNnlWhiteSpace =
            NnlWhiteSpace.SkipMany();

        private static readonly Parser<char, Unit> EndLine =
            SkipNnlWhiteSpace
                .Then(Comment.Optional())
                .Then(Char('\n'))
                .IgnoreResult();

        private static readonly Parser<char, Unit> WrapLine =
            Char('\\')
                .Then(SkipNnlWhiteSpace)
                .Then(
                    Char('\n').Then(SkipNnlWhiteSpace).Optional())
                .IgnoreResult()
                .Labelled("line wrap");

        private static readonly Parser<char, Unit> WhiteSpaceOrWrap =
            SkipNnlWhiteSpace
                .Then(WrapLine.Optional())
                //.Then(Rec(() => WhiteSpaceOrWrap).Optional())
                .IgnoreResult()
                .Labelled("white space or line wrap");

        private static Parser<char, T> Tok<T>(Parser<char, T> token)
            => Try(token).Before(WhiteSpaceOrWrap);

        private static Parser<char, string> Tok(string token)
            => Tok(String(token));

        private static Parser<char, char> Tok(char token)
            => Tok(Char(token));

        private static readonly Parser<char, string> Identifier =
            Tok(OneOf(Letter, Char('_')).Then(OneOf(LetterOrDigit, Char('_')).ManyString(), (a, b) => a + b))
                .Assert(val => !Keywords.Contains(val), v => $"Expected identifier, found keyword {v}")
                .Labelled("identifier");

        private static readonly Parser<char, AstNode.Global> KeywordGlobal =
            Tok("global")
                .Then(Identifier.SeparatedAtLeastOnce(Tok(',')))
                .Select(s => new AstNode.Global(s.ToArray()))
                .Labelled("global keyword");

        private static readonly Parser<char, Unit> EmptyLine =
            SkipNnlWhiteSpace
                .Then(Char('\n'))
                .IgnoreResult()
                .Labelled("empty line");

        private static readonly Parser<char, Unit> SkipEmptyOrComments =
            Try(EmptyLine)
                .Or(Try(SkipNnlWhiteSpace.Then(Comment)))
                .SkipMany();

        private static readonly Parser<char, Unit> SkipWhiteSpaceAndLines =
            SkipEmptyOrComments.Then(SkipNnlWhiteSpace);

        private static readonly Parser<char, AstNode.Base> Decimal =
            Tok(
                // Sign
                Char('-').Optional()
                    // Digits before the decimal point
                    .Then(Digit.AtLeastOnceString(), (a, b) => a.HasValue ? a.Value + b : b)
                    // Decimal point and after.
                    .Then(Char('.').Then(Digit.AtLeastOnceString(), (a, b) => a + b),
                        (a, b) => a + b)
                    .Select(s => (AstNode.Base) new AstNode.Decimal(LingoDecimal.Parse(s))));

        private static readonly Parser<char, AstNode.Base> Integer =
            Tok(Num.Select(i => (AstNode.Base) new AstNode.Integer(i)));

        private static readonly Parser<char, AstNode.Base> Symbol =
            Tok(Char('#')
                .Then(Identifier)
                .Select(i => (AstNode.Base) new AstNode.Symbol(i.ToLowerInvariant())));

        private static readonly Parser<char, AstNode.Base> String =
            Tok(AnyCharExcept('"')
                .ManyString()
                .Between(Char('"'))
                .Select(s => (AstNode.Base) new AstNode.String(s)));

        private static readonly string[] AllConstants =
        {
            "TRUE",
            "FALSE",
            "VOID",
            "EMPTY",
            "BACKSPACE",
            "ENTER",
            "QUOTE",
            "RETURN",
            "SPACE",
            "TAB",
            "PI"
        };

        private static readonly Parser<char, AstNode.Base> Constant =
            Tok(OneOf(AllConstants.Select(c => String(c)))
                .Select(c => (AstNode.Base) new AstNode.Constant(c)));

        private static readonly Parser<char, AstNode.Base> Literal =
            OneOf(Try(Decimal), Integer, Symbol, String, Try(Constant));

        private static readonly Parser<char, AstNode.Base> VariableName =
            Identifier.Select(i => (AstNode.Base) new AstNode.VariableName(i));

        private static Parser<char, T> Parenthesized<T>(Parser<char, T> parser) =>
            parser.Between(Tok('('), Tok(')'));

        private static Parser<char, AstNode.Base> GlobalCall(Parser<char, AstNode.Base> subExpr) =>
            Identifier
                .Then(Parenthesized(subExpr/*.Trace(arg => $"arg: {arg}")*/.Separated(Tok(','))),
                    (ident, args) => (AstNode.Base) new AstNode.GlobalCall(ident, args.ToArray()));

        private static Parser<char, AstNode.Base> List(Parser<char, AstNode.Base> subExpr) =>
            subExpr
                .Separated(Tok(','))
                .Between(Tok('['), Tok(']'))
                .Select(v => (AstNode.Base) new AstNode.List(v.ToArray()));

        private static Parser<char, AstNode.Base> PropertyList(Parser<char, AstNode.Base> subExpr) =>
            subExpr.Before(Tok(':'))
                .Then(subExpr, KeyValuePair.Create)
                .Separated(Tok(','))
                .Select(v => (AstNode.Base) new AstNode.PropertyList(v.ToArray()))
                // Empty list clause.
                .Or(Tok(':')
                    .ThenReturn(
                        (AstNode.Base) new AstNode.PropertyList(
                            Array.Empty<KeyValuePair<AstNode.Base, AstNode.Base>>())))
                .Between(Tok('['), Tok(']'));

        private static Parser<char, AstNode.Base> ParameterList(Parser<char, AstNode.Base> subExpr) =>
            subExpr
                .Before(Tok(':'))
                .Then(subExpr, KeyValuePair.Create)
                .Separated(Tok(','))
                .Between(Tok('{'), Tok('}'))
                .Select(pairs => (AstNode.Base) new AstNode.ParameterList(pairs.ToArray()));

        private static Parser<char, Func<AstNode.Base, AstNode.Base>> MemberCall(Parser<char, AstNode.Base> subExpr) =>
            Tok('.').Then(Identifier)
                .Then<IEnumerable<AstNode.Base>, Func<AstNode.Base, AstNode.Base>>(
                    Parenthesized(subExpr.Separated(Tok(','))),
                    (ident, args) => func => new AstNode.MemberCall(func, ident, args.ToArray()));

        private static Parser<char, Func<AstNode.Base, AstNode.Base>> Index(Parser<char, AstNode.Base> subExpr) =>
            subExpr.Between(Tok('['), Tok(']'))
                .Select<Func<AstNode.Base, AstNode.Base>>(
                    expr => func => new AstNode.MemberIndex(func, expr));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base>> MemberProp =
            Tok('.').Then(Identifier)
                /*.Before(Lookahead(AnyCharExcept('(')))*/
                //.Trace(i => $"member: .{i}")
                .Select<Func<AstNode.Base, AstNode.Base>>(i => expr => new AstNode.Prop(expr, i));

        private static Parser<char, Func<AstNode.Base, AstNode.Base>>
            Unary(Parser<char, AstNode.UnaryOperatorType> op) =>
            op.Select<Func<AstNode.Base, AstNode.Base>>(type => expr => new AstNode.UnaryOperator(type, expr));

        private static Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>>
            Binary(Parser<char, AstNode.BinaryOperatorType> op) =>
            op.Select<Func<AstNode.Base, AstNode.Base, AstNode.Base>>(type =>
                (left, right) => new AstNode.BinaryOperator(type, left, right));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base>> Negate =
            Unary(Tok('-').ThenReturn(AstNode.UnaryOperatorType.Negate));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base>> Not =
            Unary(Tok("not").ThenReturn(AstNode.UnaryOperatorType.Not));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> LessThan =
            Binary(Tok('<').ThenReturn(AstNode.BinaryOperatorType.LessThan));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> LessThanOrEqual =
            Binary(Try(Tok("<=")).ThenReturn(AstNode.BinaryOperatorType.LessThanOrEqual));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> NotEqual =
            Binary(Try(Tok("<>")).ThenReturn(AstNode.BinaryOperatorType.NotEqual));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Equal =
            Binary(Tok("=").ThenReturn(AstNode.BinaryOperatorType.Equal));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> GreaterThan =
            Binary(Tok('>').ThenReturn(AstNode.BinaryOperatorType.GreaterThan));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> GreaterThanOrEqual =
            Binary(Try(Tok(">=")).ThenReturn(AstNode.BinaryOperatorType.GreaterThanOrEqual));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Contains =
            Binary(Tok("contains").ThenReturn(AstNode.BinaryOperatorType.Contains));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Starts =
            Binary(Tok("starts").ThenReturn(AstNode.BinaryOperatorType.Starts));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> ConcatSpace =
            Binary(Try(Tok("&&")).ThenReturn(AstNode.BinaryOperatorType.ConcatSpace));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Concat =
            Binary(Tok('&').ThenReturn(AstNode.BinaryOperatorType.Concat));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Add =
            Binary(Tok('+').ThenReturn(AstNode.BinaryOperatorType.Add));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Multiply =
            Binary(Tok('*').ThenReturn(AstNode.BinaryOperatorType.Multiply));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Divide =
            Binary(Tok('/').ThenReturn(AstNode.BinaryOperatorType.Divide));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> And =
            Binary(Tok("and").TraceBegin("trying binary and").ThenReturn(AstNode.BinaryOperatorType.And));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Or =
            Binary(Tok("or").ThenReturn(AstNode.BinaryOperatorType.Or));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Mod =
            Binary(Tok("mod").ThenReturn(AstNode.BinaryOperatorType.Mod));

        private static readonly Parser<char, Func<AstNode.Base, AstNode.Base, AstNode.Base>> Subtract =
            Binary(Try(Tok('-').Then(Lookahead(AnyCharExcept('-')))).ThenReturn(AstNode.BinaryOperatorType.Subtract));

        private static readonly string[] KeywordFunctionNames =
        {
            "put"
        };

        private static Parser<char, AstNode.Base> KeywordFunction(Parser<char, AstNode.Base> expr) =>
            OneOf(KeywordFunctionNames.Select(Tok))
                //.Trace(n => $"Trying keyword: {n}")
                .Then(
                    expr,
                    (name, arg) => (AstNode.Base) new AstNode.GlobalCall(name, new[] {arg}));

        private static readonly Parser<char, AstNode.Base> The =
            Tok("the")
                .Then(Identifier)
                .Select(i => (AstNode.Base) new AstNode.The(i));

        public static readonly Parser<char, AstNode.Base> Expression = ExpressionFunc(true);
        private static readonly Parser<char, AstNode.Base> ExpressionNoEquals = ExpressionFunc(false);

        private static Parser<char, AstNode.Base> ExpressionFunc(bool allowEquals) =>
            ExpressionParser.Build<char, AstNode.Base>(
                _ =>
                {
                    var expr = Rec(() => Expression);
                    var precedence1 = Operator.InfixL(LessThanOrEqual)
                        .And(Operator.InfixL(NotEqual))
                        .And(Operator.InfixL(LessThan))
                        .And(Operator.InfixL(GreaterThanOrEqual))
                        .And(Operator.InfixL(GreaterThan))
                        .And(Operator.InfixL(Contains))
                        .And(Operator.InfixL(Starts));

                    if (allowEquals)
                        precedence1 = precedence1.And(Operator.InfixL(Equal));

                    var operatorTable = new[]
                    {
                        // Unspecified operators.
                        Operator.PostfixChainable(
                            Try(MemberCall(expr).TraceBegin("member call postfix")),
                            Try(MemberProp).TraceBegin("member prop postfix"),
                            Index(expr).TraceBegin("member index")),

                        // TODO: are these associativity rules correct?
                        // Precedence 5
                        Operator.Prefix(Negate)
                            .And(Operator.Prefix(Not)),
                        // Precedence 4
                        Operator.InfixL(Add)
                            .And(Operator.InfixL(Multiply.TraceBegin("Multiply")))
                            .And(Operator.InfixL(Divide.TraceBegin("Divide")))
                            .And(Operator.InfixL(And.TraceBegin("And")))
                            .And(Operator.InfixL(Or.TraceBegin("Or")))
                            .And(Operator.InfixL(Mod.TraceBegin("Mod"))),
                        // Precedence 3
                        Operator.InfixL(Subtract),
                        // Precedence 2
                        Operator.InfixL(ConcatSpace.TraceBegin("Concat Space"))
                            .And(Operator.InfixL(Concat.TraceBegin("Concat"))),
                        // Precedence 1
                        precedence1,
                    };
                    return (
                        OneOf(
                            Try(KeywordFunction(expr)).TraceBegin("TRYING KEYWORD")
                            /*.Trace(g => $"found keyword: {DebugPrint.PrintAstNode(g)}")*/,
                            Try(GlobalCall(expr)).TraceBegin("TRYING GLOBAL CALL")
                            /*.Trace(g => $"Global call: {DebugPrint.PrintAstNode(g)}")*/,
                            Literal.TraceBegin("TRYING LITERAL"),
                            Try(The).TraceBegin("TRYING THE"),
                            VariableName.TraceBegin("TRYING VAR"),
                            Parenthesized(expr).TraceBegin("TRYING PARENS"),
                            Try(PropertyList(expr)).TraceBegin("TRYING PROPERTY LIST"),
                            List(expr).TraceBegin("TRYING LIST"),
                            ParameterList(expr).TraceBegin("TRYING PARAMETER LIST")
                        ).TraceBegin("TRYING TERM") /*.Trace(n => $"term: {DebugPrint.PrintAstNode(n)}")*/,
                        operatorTable
                    );
                }).TraceBegin("Trying expr") /*.Trace(expr => $"expr: {expr}")*/;

        private static readonly Parser<char, AstNode.StatementBlock> StatementBlock =
                Try(SkipWhiteSpaceAndLines
                        .Then(
                            Rec(() => Statement).TraceBegin("Statementblock -> statement")
                                //.Trace(DebugPrint.PrintAstNode)
                                .Before(EndLine)))
                    .Many()
                    .Before(SkipWhiteSpaceAndLines)
                    .Select(b => new AstNode.StatementBlock(b.ToArray()))
            /*.Trace(b => $"Statement block: {DebugPrint.PrintAstNode(b)}")*/;

        private static readonly Parser<char, AstNode.Base> RepeatWhile =
            Try(Tok("while"))
                .Then(Expression)
                .Then(StatementBlock, (cond, block) => (AstNode.Base) new AstNode.RepeatWhile(cond, block))
                .Before(Tok("end").Before(Tok("repeat")));

        private static readonly Parser<char, AstNode.Base> RepeatWith =
            Try(Tok("with")).Then(
                OneOf(
                    // repeat with .. in ..
                    Map(
                        (varName, listExpr, block) =>
                            (AstNode.Base) new AstNode.RepeatWithList(varName, listExpr, block),
                        Try(Identifier.Before(Tok("in"))),
                        Expression,
                        StatementBlock),

                    // repeat with .. = .. to ..
                    Map((varName, start, end, block) =>
                            (AstNode.Base) new AstNode.RepeatWithCounter(varName, start, end, block),
                        Identifier.Before(Tok('=')),
                        Expression.Before(Tok("to")),
                        Expression,
                        StatementBlock
                    ).TraceBegin("repeat with =")
                )).Before(Tok("end")).Before(Tok("repeat"));

        private static readonly Parser<char, AstNode.Base> Repeat =
            Try(Tok("repeat"))
                .Then(OneOf(RepeatWhile, RepeatWith));

        private static readonly Parser<char, AstNode.Base> If =
            Try(Tok("if"))
                .Then(
                    Map((condition, body, elseIfs, @else) =>
                            (AstNode.Base) new AstNode.If(
                                condition,
                                body,
                                // Fold else ifs into nested if else statements.
                                elseIfs.Reverse().Aggregate(
                                    @else.GetValueOrDefault(),
                                    (aggElse, tuple) => new AstNode.StatementBlock(new AstNode.Base[]
                                    {
                                        new AstNode.If(tuple.cond, tuple.block, aggElse)
                                    }))),
                        // Condition
                        Expression.Before(Tok("then")),
                        // Code block
                        StatementBlock.TraceBegin("Entering if block"),
                        // Else if clauses.
                        Try(Tok("else").Then(Tok("if"))) /*.Trace("Entering else if")*/
                            .Then(Expression)
                            .Before(Tok("then"))
                            .Then(StatementBlock, (expr, block) => (cond: expr, block))
                            .Many(),
                        // Else clause.
                        Try(Tok("else")
                                .Then(StatementBlock))
                            .Optional()
                            .Before(Tok("end").Before(Tok("if"))))
                ).TraceBegin("Trying if") /*.Trace(i => $"end if: {i}")*/;

        private static readonly Parser<char, IEnumerable<AstNode.Base>> CaseExpr =
            SkipWhiteSpaceAndLines
                .Then(Expression.Separated(Tok(',')))
                /*.Trace(e => $"case expr candidate: {e}")*/
                .Before(Tok(':'));

        private static readonly Parser<char, Unit> CaseOtherwise =
            SkipWhiteSpaceAndLines
                .Then(Tok("otherwise"))
                .Then(Tok(':'))
                .IgnoreResult();

        private static readonly Parser<char, Unit> CaseEnd =
            SkipWhiteSpaceAndLines.Then(Tok("end")).IgnoreResult();

        private static readonly Parser<char, IEnumerable<(AstNode.Base[] exprs, AstNode.StatementBlock)>> CaseBody =
            Try(CaseExpr)
                .TraceBegin("trying new case body")
                .Then(
                    Try(SkipWhiteSpaceAndLines
                            .Then(Rec(() => Statement))
                            .Before(EndLine))
                        .Until(Try(Lookahead(Try(CaseEnd).Or(Try(CaseOtherwise)).Or(CaseExpr.IgnoreResult())
                            .TraceBegin("Checking end"))))
                    /*.Trace("Done until")*/,
                    (expr, block) => (expr.ToArray(), new AstNode.StatementBlock(block.ToArray())))
                .Many() /*.Trace("case body end")*/;

        private static readonly Parser<char, AstNode.Base> Case =
            Try(Tok("case"))
                .Then(Map((expr, cases, otherwise) =>
                        (AstNode.Base) new AstNode.Case(expr, cases.ToArray(), otherwise.GetValueOrDefault()),
                    Expression.Before(Tok("of")),
                    CaseBody.Before(Nop.TraceBegin("case body ended here")),
                    Try(CaseOtherwise).TraceBegin("foo")
                        .Then(StatementBlock)
                        .Optional()
                ))
                .Before(SkipWhiteSpaceAndLines).Before(Tok("end")).Before(Tok("case"));

        private static readonly Parser<char, AstNode.Assignment> Assignment =
            Map(
                (var, expr) => new AstNode.Assignment(var, expr),
                Tok(ExpressionNoEquals).Before(Tok('=')),
                Expression
            );

        private static readonly Parser<char, AstNode.Base> Return =
            Try(Tok("return"))
                .Then(Expression,
                    (_, expr) => (AstNode.Base) new AstNode.Return(expr));

        private static readonly Parser<char, AstNode.Base> Exit =
            Try(Tok("exit"))
                .Then(Tok("repeat").Optional())
                .Select(r => r.HasValue ? (AstNode.Base) new AstNode.ExitRepeat() : new AstNode.Exit());

        public static readonly Parser<char, AstNode.Base> Statement =
                OneOf(
                    Return.TraceBegin("Statement -> return"),
                    Exit.TraceBegin("Statement -> exit"),
                    Case.TraceBegin("Statement -> case"),
                    Repeat.TraceBegin("Statement -> repeat"),
                    If.TraceBegin("Statement -> if"),
                    Try(Assignment.Cast<AstNode.Base>()).TraceBegin("Statement -> assignment"),
                    Try(KeywordGlobal.Cast<AstNode.Base>()).TraceBegin("Statement -> global"),
                    Expression.TraceBegin("Statement -> expr"))
            /*.Trace(s => $"STATEMENT: {DebugPrint.PrintAstNode(s)}")*/;

        private static readonly Parser<char, IEnumerable<string>> HandlerParameter =
            Identifier
                .Separated(Tok(','));

        private static readonly Parser<char, IEnumerable<string>> HandleParenthesesParameter =
            OneOf(
                Parenthesized(HandlerParameter),
                HandlerParameter);

        private static readonly Parser<char, AstNode.Handler> Handler =
            Tok("on")
                //.Trace("HANDLER")
                .Then(Map(
                    (name, parameters, body) => new AstNode.Handler(name, parameters.ToArray(), body),
                    Identifier,
                    HandleParenthesesParameter.Before(EndLine),
                    StatementBlock
                ))
                .Before(Tok("end"))
                //.Trace(h => $"HANDLER: {DebugPrint.PrintAstNode(h)}")
                .Bind(h => Tok(h.Name).Optional().ThenReturn(h))
                .Labelled("handler definition");

        private static readonly Parser<char, AstNode.Base> TopKeyword =
            OneOf(
                    KeywordGlobal.Cast<AstNode.Base>(),
                    Handler.Cast<AstNode.Base>())
                .Labelled("top-level keyword");

        public static readonly Parser<char, AstNode.Script> Script =
            Try(SkipEmptyOrComments
                    .Then(TopKeyword)
                    .Before(EndLine))
                .Many()
                .TracePos("B")
                .Before(SkipWhiteSpaceAndLines)
                .Select(n => new AstNode.Script(n.ToArray()))
                .TracePos("A")
                .Before(End)
                .Labelled("script");
    }
}