// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class OperatorPrecedenceTier
    {
        public IEnumerable<OperatorResolution> Operators { get; private set; }

        public OperatorPrecedenceTier(IEnumerable<OperatorResolution> operators)
        {
            Operators = operators;
        }
    }

    internal struct OperatorResolution
    {
        public ByteStringSlice OperatorStr { get; private set; }
        public ExpressionValue.EOperator Operator { get; private set; }

        public OperatorResolution(string opStr, ExpressionValue.EOperator op)
        {
            OperatorStr = ByteString.FromAsciiString(opStr).ToSlice();
            Operator = op;
        }
    }

    internal class OperatorPrecedences
    {

        public IReadOnlyList<OperatorPrecedenceTier> Tiers { get { return _tiers; } }

        private static IEnumerable<OperatorResolution> _opResolutions = GenerateOperatorResolutions();
        public static OperatorPrecedences DParseCompatible = GenerateDParseCompatiblePrecedences();
        public static OperatorPrecedences Cpp = GenerateCppPrecedences();

        private List<OperatorPrecedenceTier> _tiers;

        public OperatorPrecedences(IEnumerable<OperatorPrecedenceTier> tiers)
        {
            _tiers = new List<OperatorPrecedenceTier>();
            _tiers.AddRange(tiers);
        }

        private static OperatorPrecedences GenerateDParseCompatiblePrecedences()
        {
            string[] ops =
            {
                "/", "*", "-", "+", "<=", ">=", "<", ">", "!=", "==", "&&", "^^", "||"
            };

            List<OperatorPrecedenceTier> tiers = new List<OperatorPrecedenceTier>();
            foreach (string op in ops)
            {
                List<OperatorResolution> tierOps = new List<OperatorResolution>();
                tierOps.Add(FindResolution(op));

                OperatorPrecedenceTier tier = new OperatorPrecedenceTier(tierOps);

                tiers.Add(tier);
            }

            return new OperatorPrecedences(tiers);
        }

        private static OperatorPrecedences GenerateCppPrecedences()
        {
            string[][] opTiers =
            {
                new string[] { "/", "*" },
                new string[] { "-", "+" },
                new string[] { "<=", ">=", "<", ">" },
                new string[] { "!=", "==" },
                new string[] { "&&" },
                new string[] { "^^" },
                new string[] { "||" },
            };

            List<OperatorPrecedenceTier> tiers = new List<OperatorPrecedenceTier>();
            foreach (string[] ops in opTiers)
            {
                List<OperatorResolution> tierOps = new List<OperatorResolution>();

                foreach (string op in ops)
                    tierOps.Add(FindResolution(op));

                OperatorPrecedenceTier tier = new OperatorPrecedenceTier(tierOps);

                tiers.Add(tier);
            }

            return new OperatorPrecedences(tiers);
        }

        private static IEnumerable<OperatorResolution> GenerateOperatorResolutions()
        {
            List<OperatorResolution> resolutions = new List<OperatorResolution>();

            // Longer matches must come first
            resolutions.Add(new OperatorResolution("||", ExpressionValue.EOperator.Or));
            resolutions.Add(new OperatorResolution("&&", ExpressionValue.EOperator.And));
            resolutions.Add(new OperatorResolution("^^", ExpressionValue.EOperator.Xor));

            resolutions.Add(new OperatorResolution(">=", ExpressionValue.EOperator.Ge));
            resolutions.Add(new OperatorResolution(">", ExpressionValue.EOperator.Gt));
            resolutions.Add(new OperatorResolution("<=", ExpressionValue.EOperator.Le));
            resolutions.Add(new OperatorResolution("<", ExpressionValue.EOperator.Lt));

            resolutions.Add(new OperatorResolution("==", ExpressionValue.EOperator.Eq));
            resolutions.Add(new OperatorResolution("!=", ExpressionValue.EOperator.Neq));

            resolutions.Add(new OperatorResolution("+", ExpressionValue.EOperator.Add));
            resolutions.Add(new OperatorResolution("-", ExpressionValue.EOperator.Sub));
            resolutions.Add(new OperatorResolution("/", ExpressionValue.EOperator.Div));
            resolutions.Add(new OperatorResolution("*", ExpressionValue.EOperator.Mul));

            return resolutions;
        }

        private static OperatorResolution FindResolution(string str)
        {
            ByteString opStr = ByteString.FromAsciiString(str);

            foreach (OperatorResolution resolution in _opResolutions)
            {
                if (opStr.Equals(resolution.OperatorStr))
                    return resolution;
            }

            throw new Exception($"Internal error: Unresolved operator resolution {str}");
        }
    }
}
