﻿// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class ExprConverter
    {
        private bool _allowMalformedExprs;
        private bool _optimize;
        private ILogger? _logger;

        public ExprConverter(bool allowMalformedExprs, bool optimize, ILogger? logger)
        {
            _allowMalformedExprs = allowMalformedExprs;
            _optimize = optimize;
            _logger = logger;
        }

        private IExpressionOperand ConvertValueToOperand(ulong treeLocation, IExprValue exprValue, ILogger.LocationTag locTag)
        {
            switch (exprValue.ExprType)
            {
                case ExprType.Expr:
                    return new ExpressionValueOperand(ConvertExpression(treeLocation, (ExpressionExprValue)exprValue, locTag));
                case ExprType.FloatConst:
                    return new FloatOperand(((FloatConstExprValue)exprValue).Value);
                case ExprType.FloatVar:
                    return new StringOperand(((FloatVarExprValue)exprValue).VarName.ToByteString());
                case ExprType.StringConst:
                    return new QuotedStringOperand(Utils.EscapeSlice(((StringConstExprValue)exprValue).UnescapedValue, locTag, true, true).ToByteString());
                case ExprType.StringVar:
                    return new StringOperand(((StringVarExprValue)exprValue).VarName.ToByteString());
                case ExprType.Invalid:
                    throw new CompilerException(locTag, "Expresion was invalid and unemittable");
                default:
                    throw new Exception("Internal error: Bad expr type");
            }
        }

        private ExpressionValue ConvertExpression(ulong treeLocation, ExpressionExprValue expr, ILogger.LocationTag locTag)
        {
            if ((treeLocation >> 62) != 0)
                throw new CompilerException(locTag, "Condition was too complex");

            ulong leftLocation = (treeLocation << 2) + 1;
            ulong rightLocation = (treeLocation << 2) + 2;

            IExpressionOperand left = ConvertValueToOperand(leftLocation, expr.Left, locTag);
            IExpressionOperand right = ConvertValueToOperand(rightLocation, expr.Right, locTag);

            return new ExpressionValue(expr.Operator, leftLocation, expr.Left.OperandType, left, rightLocation, expr.Right.OperandType, right);
        }

        public OptionalExpression ConvertValueToOptionalExpression(IExprValue? exprValue, ILogger.LocationTag locTag)
        {
            if (exprValue == null)
                return new OptionalExpression();

            return new OptionalExpression(ConvertValueToExpression(exprValue, locTag));
        }

        private ExpressionValue ConvertValueToExpression(IExprValue exprValue, ILogger.LocationTag locTag)
        {
            if (exprValue.ResultType != ExprResultType.Float)
            {
                if (exprValue.ExprType != ExprType.Expr || !_allowMalformedExprs)
                    throw new Exception("Internal error: Expression to convert to condition was not float type");
                else
                {
                    if (_logger != null)
                        _logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, locTag), "Expression is invalid (type mismatch, probably)");
                }
            }

            if (exprValue.ExprType == ExprType.Expr)
                return ConvertExpression(1, (ExpressionExprValue)exprValue, locTag);

            return ConvertExpression(1, new ExpressionExprValue(new FloatConstExprValue(0), exprValue, ExpressionValue.EOperator.Add), locTag);
        }

        // Returns true if the command or block using this condition should be emitted (i.e. condition is not constant false)
        public bool CheckAndConvertCondition(IExprValue condition, ILogger.LocationTag locTag, out OptionalExpression expr)
        {
            if (_optimize)
            {
                if (condition.ExprType == ExprType.FloatConst && ((FloatConstExprValue)condition).Value == 0.0f)
                {
                    expr = ConvertValueToOptionalExpression(condition, locTag);
                    return false;
                }
            }

            expr = new OptionalExpression(ConvertValueToExpression(condition, locTag));
            return true;
        }
    }
}
