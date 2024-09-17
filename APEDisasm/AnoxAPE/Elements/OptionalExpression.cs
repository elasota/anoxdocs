namespace AnoxAPE.Elements
{
    public class OptionalExpression
    {
        public ExpressionValue? Expression { get; private set; }

        public OptionalExpression()
        {
            Expression = null;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "OptionalExpression");

            ulong exprFlag = inStream.ReadUInt64();
            if (exprFlag == 0)
                return;
            else if (exprFlag == 1)
            {
                Expression = new ExpressionValue();

                Expression.Load(inStream, indent + 1, disasmStream);

                inStream.ExpectUInt32(0);
                inStream.ExpectUInt32(0);
            }
            else
                inStream.ReportError($"Unexpected flag {exprFlag} in OptionalExpression");
        }

        public void Write(OutputStream outStream)
        {
            if (Expression == null)
            {
                outStream.WriteUInt64(0);
                return;
            }

            outStream.WriteUInt64(1);
            Expression.Write(outStream);
            outStream.WriteUInt64(0);
        }
    }
}
