using BlueprintCommon;
using BlueprintCommon.Models;
using CompilerCommon;
using ILReader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using SeeSharp.Runtime.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace SeeSharpCompiler
{
    public static class SeeSharpCompiler
    {
        private const int RegisterCount = 32;
        private const int RamAddress = 16385;

        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<CompilerConfiguration>());
        }

        public static void Run(CompilerConfiguration configuration)
        {
            var inputProgramFile = configuration.InputProgram;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            var outputInstructionsFile = configuration.OutputInstructions;
            var width = configuration.Width;
            var height = configuration.Height;

            using var instructionsWriter = new StreamWriter(outputInstructionsFile);

            var compiledProgram = CompileCode(inputProgramFile);

            if (compiledProgram != null)
            {
                var blueprint = BlueprintGenerator.CreateBlueprintFromCompiledProgram(compiledProgram, width, height, instructionsWriter);
                BlueprintUtil.PopulateIndices(blueprint);

                var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

                BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
                BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            }
        }

        private static CompiledProgram CompileCode(string inputProgramFile)
        {
            var compilation = CSharpCompilation.Create("SeeSharpProgram")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(File.ReadAllText(inputProgramFile), path: inputProgramFile))
                .AddReferences(
                    MetadataReference.CreateFromFile("C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Runtime.dll"),
                    MetadataReference.CreateFromFile(typeof(InlineAttribute).Assembly.Location)
                );

            var syntaxTree = compilation.SyntaxTrees[0];
            var visitor = new ProgramVisitor(compilation, syntaxTree);
            visitor.Visit();

            var diagnostics = compilation.GetDiagnostics()
                .Concat(visitor.Diagnostics)
                .ToList();

            foreach (var diagnostic in diagnostics
                .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
                .OrderByDescending(diagnostic => diagnostic.Severity))
            {
                var position = diagnostic.Location.GetMappedLineSpan().StartLinePosition;
                Console.WriteLine($"{diagnostic.Severity} at line {position.Line + 1} column {position.Character + 1}: {diagnostic.GetMessage()}");
            }

            return diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) == 0
                ? new CompiledProgram
                {
                    Name = "See Sharp Compiler Test",
                    Instructions = visitor.Instructions
                }
                : null;
        }

        private class ProgramVisitor : CSharpSyntaxVisitor
        {
            private readonly SyntaxTree syntaxTree;
            private readonly SemanticModel semanticModel;
            private MethodContext methodContext;

            public List<Instruction> Instructions { get; } = new List<Instruction>();
            public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

            public ProgramVisitor(CSharpCompilation compilation, SyntaxTree syntaxTree)
            {
                this.syntaxTree = syntaxTree;
                semanticModel = compilation.GetSemanticModel(syntaxTree, true);
            }

            public void Visit()
            {
                // Initialize registers
                AddInstruction(Instruction.SetRegisterToImmediateValue(SpecialRegisters.StackPointer, RamAddress));
                AddInstructions(Enumerable.Range(3, RegisterCount - 2).Select(register => Instruction.SetRegisterToImmediateValue(register, 0)));

                Visit(syntaxTree.GetRoot());

                // Jump back to the beginning
                AddInstruction(Instruction.Jump(-(Instructions.Count + 1)));
            }

            public override void VisitCompilationUnit(CompilationUnitSyntax node)
            {
                Visit(node.Members);
            }

            public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
            {
                Visit(node.Members);
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                Visit(node.Members);
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var previousMethodContext = methodContext;
                methodContext = new MethodContext();
                try
                {
                    if (node.Identifier.ValueText == "Main")
                    {
                        Visit(node.Body);
                    }
                }
                finally
                {
                    methodContext = previousMethodContext;
                }
            }

            public override void VisitBlock(BlockSyntax node)
            {
                Visit(node.Statements);
            }

            public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            {
                foreach (var variable in node.Declaration.Variables)
                {
                    var type = semanticModel.GetTypeInfo(variable.Initializer.Value);

                    if (type.Type.SpecialType != SpecialType.System_Int32)
                    {
                        Diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.OnlyIntsAreSupported, node.GetLocation(), type.Type));
                    }

                    var symbol = semanticModel.GetDeclaredSymbol(variable);

                    if (symbol != null)
                    {
                        methodContext.LocalVariables[symbol] = methodContext.StackPointerOffset;
                    }

                    if (variable.Initializer != null)
                    {
                        Visit(variable.Initializer.Value);
                    }
                    else
                    {
                        AddInstruction(Instruction.AdjustStackPointer(1));
                    }
                }
            }

            public override void VisitLiteralExpression(LiteralExpressionSyntax node)
            {
                HandleConstantExpression(node);
            }

            public override void VisitIdentifierName(IdentifierNameSyntax node)
            {
                var symbol = semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol != null && symbol.Kind == SymbolKind.Local && methodContext != null && methodContext.LocalVariables.TryGetValue(symbol, out var address))
                {
                    PushStackValue(address);
                }
            }

            public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                if (HandleConstantExpression(node))
                {
                    return;
                }

                var symbol = semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol != null)
                {
                    
                }
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var symbol = semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol != null)
                {
                    var arguments = node.ArgumentList.Arguments;
                    Visit(arguments);

                    if (GetAttribute<InlineAttribute>(symbol) != null)
                    {
                        var containingType = GetTypeForSymbol(symbol.ContainingType);

                        if (containingType != null)
                        {
                            var parameterTypes = ((IMethodSymbol)symbol).Parameters.Select(parameter => GetTypeForSymbol(parameter.Type)).ToArray();
                            var method = containingType.GetMethod(symbol.Name, parameterTypes);

                            if (method != null)
                            {
                                var ilInstructions = method.GetInstructions();

                                foreach (var ilInstruction in ilInstructions)
                                {
                                    var opCodeValue = ilInstruction.Code.Value;

                                    if (opCodeValue == OpCodes.Ldc_I4.Value)
                                    {
                                        AddInstruction(Instruction.PushImmediateValue((int)ilInstruction.Operand));
                                    }
                                    else if (opCodeValue == OpCodes.Ldarg_0.Value)
                                    {
                                        PushStackValue(-arguments.Count);
                                    }
                                    else if (opCodeValue == OpCodes.Call.Value)
                                    {
                                        var operand = (MethodInfo)ilInstruction.Operand;

                                        if (operand.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                                        {
                                            if (operand.DeclaringType.Name == "Memory" && operand.Name == "ReadSignal")
                                            {
                                                AddInstruction(Instruction.Pop(3));
                                                AddInstruction(Instruction.Pop(4));
                                                AddInstructions(Instruction.NoOp(4));
                                                AddInstruction(Instruction.ReadSignal(outputRegister: 5, addressRegister: 3, signalRegister: 4));
                                                AddInstructions(Instruction.NoOp(4));
                                                AddInstruction(Instruction.PushRegister(5));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public override void VisitArgument(ArgumentSyntax node)
            {
                Visit(node.Expression);
            }

            public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            {
                if (HandleConstantExpression(node))
                {
                    return;
                }

                switch (node.OperatorToken.Kind())
                {
                    case SyntaxKind.MinusToken:
                        AddInstruction(Instruction.Pop(4));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.BinaryOperation(Operation.Subtract, outputRegister: 5, rightInputRegister: 4));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.PushRegister(5));

                        break;
                }
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                if (HandleConstantExpression(node))
                {
                    return;
                }

                Visit(node.Left);
                Visit(node.Right);

                var opCode = node.OperatorToken.Kind() switch
                {
                    SyntaxKind.AsteriskToken => Operation.Multiply,
                    SyntaxKind.SlashToken => Operation.Divide,
                    SyntaxKind.PlusToken => Operation.Add,
                    SyntaxKind.MinusToken => Operation.Subtract,
                    SyntaxKind.PercentToken => Operation.Mod,
                    SyntaxKind.LessThanLessThanToken => Operation.LeftShift,
                    SyntaxKind.GreaterThanGreaterThanToken => Operation.RightShift,
                    SyntaxKind.AmpersandToken => Operation.And,
                    SyntaxKind.BarToken => Operation.Or,
                    SyntaxKind.CaretToken => Operation.Xor,
                    _ => Operation.NoOp
                };

                if (opCode == Operation.NoOp)
                {
                    Diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedOperator, node.GetLocation(), node.OperatorToken.Kind()));
                    return;
                }

                AddInstruction(Instruction.Pop(3));
                AddInstruction(Instruction.Pop(4));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.BinaryOperation(opCode, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.PushRegister(5));
            }

            private void Visit<T>(IEnumerable<T> nodes) where T : SyntaxNode
            {
                foreach (var node in nodes)
                {
                    Visit(node);
                }
            }

            private bool HandleConstantExpression(ExpressionSyntax node)
            {
                var constantValue = semanticModel.GetConstantValue(node);
                if (constantValue.HasValue)
                {
                    if (constantValue.Value is int)
                    {
                        AddInstruction(Instruction.PushImmediateValue((int)constantValue.Value));
                    }
                }

                return constantValue.HasValue;
            }

            private void AddInstruction(Instruction instruction)
            {
                Instructions.Add(instruction);

                if (instruction.LeftInputRegister == SpecialRegisters.StackPointer)
                {
                    methodContext.StackPointerOffset += instruction.AutoIncrement;
                }

                if (instruction.LeftInputRegister == SpecialRegisters.InstructionPointer && instruction.AutoIncrement != 0)
                {
                    AddInstructions(Instruction.NoOp(4));
                }
            }

            private void PushStackValue(int offsetRelativeToBase)
            {
                AddInstruction(Instruction.ReadStackValue(offsetRelativeToBase - methodContext.StackPointerOffset, 3));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.PushRegister(3));
            }

            private void AddInstructions(IEnumerable<Instruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    AddInstruction(instruction);
                }
            }

            private AttributeData GetAttribute<T>(ISymbol symbol)
            {
                var assemblyName = typeof(T).Assembly.FullName;
                var attributeName = typeof(T).FullName;

                return symbol.GetAttributes().FirstOrDefault(attribute =>
                    attribute.AttributeClass.ContainingAssembly.ToString() == assemblyName &&
                    attribute.AttributeClass.ToString() == attributeName);
            }

            private Type GetTypeForSymbol(ITypeSymbol symbol)
            {
                return Type.GetType($"{symbol.ToDisplayString()}, {symbol.ContainingAssembly.ToDisplayString()}");
            }
        }

        private class MethodContext
        {
            public Dictionary<ISymbol, int> LocalVariables { get; set; } = new Dictionary<ISymbol, int>();
            public int StackPointerOffset { get; set; } = 0;
        }
    }

    public class CompilerConfiguration
    {
        public string InputProgram { get; set; }
        public string OutputBlueprint { get; set; }
        public string OutputJson { get; set; }
        public string OutputInstructions { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
