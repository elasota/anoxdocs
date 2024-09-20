namespace AnoxAPECompiler.HLCompiler
{
    interface IInlineSwitchIDGenerator
    {
        public bool TryGenerateNextID(out uint id);
    }
}
