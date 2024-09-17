namespace AnoxAPE.Elements
{
    public interface IExpressionOperand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
        public void Write(OutputStream outStream);
    }
}
