using AnoxAPE.Elements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        internal IExpressionOperand ConvertValueToOperand(ulong treeLocation, IExprValue exprValue, ILogger.LocationTag locTag)
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

        internal ExpressionValue ConvertExpression(ulong treeLocation, ExpressionExprValue expr, ILogger.LocationTag locTag)
        {
            if ((treeLocation >> 62) != 0)
                throw new CompilerException(locTag, "Condition was too complex");

            ulong leftLocation = (treeLocation << 2) + 1;
            ulong rightLocation = (treeLocation << 2) + 2;

            IExpressionOperand left = ConvertValueToOperand(leftLocation, expr.Left, locTag);
            IExpressionOperand right = ConvertValueToOperand(leftLocation, expr.Right, locTag);

            return new ExpressionValue(expr.Operator, leftLocation, expr.Left.OperandType, left, rightLocation, expr.Right.OperandType, right);
        }

        internal OptionalExpression ConvertValueToOptionalExpression(IExprValue? exprValue, ILogger.LocationTag locTag)
        {
            if (exprValue == null)
                return new OptionalExpression();

            return new OptionalExpression(ConvertValueToExpression(exprValue, locTag));
        }

        internal ExpressionValue ConvertValueToExpression(IExprValue exprValue, ILogger.LocationTag locTag)
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

        // Returns true if the expression should be emitted (e.g. not constant false)
        internal bool CheckAndConvertCondition(IExprValue condition, ILogger.LocationTag locTag, out OptionalExpression expr)
        {
            if (_optimize)
                throw new NotImplementedException();

            expr = new OptionalExpression(ConvertValueToExpression(condition, locTag));
            return true;
        }
    }
}
