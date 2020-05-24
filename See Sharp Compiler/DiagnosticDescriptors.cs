using Microsoft.CodeAnalysis;

namespace SeeSharpCompiler
{
    public static class DiagnosticDescriptors
    {
        private static class Categories
        {
            public const string SeeSharpCompiler = "SeeSharpCompiler";
        }

        public static readonly DiagnosticDescriptor OnlyIntsAreSupported = new DiagnosticDescriptor("SS1001", "Only ints are supported", "Unsupported variable type {0}", Categories.SeeSharpCompiler, DiagnosticSeverity.Error, true);
        public static readonly DiagnosticDescriptor UnsupportedOperator = new DiagnosticDescriptor("SS1002", "Unsupported operator", "Unsupported operator {0}", Categories.SeeSharpCompiler, DiagnosticSeverity.Error, true);
    }
}
