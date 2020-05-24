using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using MemoryInitializer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SeeSharpCompiler
{
    public static class SeeSharpCompiler
    {
        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<CompilerConfiguration>());
        }

        public static void Run(CompilerConfiguration configuration)
        {
            var inputProgramFile = configuration.InputProgram;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            //var outputInstructionsFile = configuration.OutputInstructions;
            var width = configuration.Width;
            var height = configuration.Height;

            var compiledProgram = CompileCode(inputProgramFile);

            if (compiledProgram != null)
            {
                var blueprint = CreateBlueprintFromCompiledProgram(compiledProgram, width, height);
                var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

                BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
                BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
                //WriteOutInstructions(outputInstructionsFile, blueprint);
            }
        }

        private static CompiledProgram CompileCode(string inputProgramFile)
        {
            var compilation = CSharpCompilation.Create("SeeSharpProgram")
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(File.ReadAllText(inputProgramFile), path: inputProgramFile))
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile("../../../../See Sharp Runtime/bin/Debug/netcoreapp3.1/SeeSharpRuntime.dll")
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
                Visit(syntaxTree.GetRoot());
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
                        AddInstruction(Instruction.SetRegisterToImmediateValue(SpecialRegisters.StackPointer, 1));
                        AddInstructions(Instruction.NoOp(4));

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
                    Instructions.Add(new Instruction
                    {
                        OpCode = OpCode.Read,
                        OutputRegister = 3,
                        LeftInputRegister = SpecialRegisters.StackPointer,
                        LeftImmediateValue = address - methodContext.StackPointerOffset
                    });

                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.PushRegister(3));
                }
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
                        AddInstruction(Instruction.PopRegister(4));
                        AddInstructions(Instruction.NoOp(4));

                        AddInstruction(new Instruction
                        {
                            OpCode = OpCode.Subtract,
                            OutputRegister = 5,
                            RightInputRegister = 4
                        });

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
                    SyntaxKind.AsteriskToken => OpCode.Multiply,
                    SyntaxKind.SlashToken => OpCode.Divide,
                    SyntaxKind.PlusToken => OpCode.Add,
                    SyntaxKind.MinusToken => OpCode.Subtract,
                    SyntaxKind.PercentToken => OpCode.Mod,
                    SyntaxKind.LessThanLessThanToken => OpCode.LeftShift,
                    SyntaxKind.GreaterThanGreaterThanToken => OpCode.RightShift,
                    SyntaxKind.AmpersandToken => OpCode.And,
                    SyntaxKind.BarToken => OpCode.Or,
                    SyntaxKind.CaretToken => OpCode.Xor,
                    _ => OpCode.NoOp
                };

                if (opCode == OpCode.NoOp)
                {
                    Diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedOperator, node.GetLocation(), node.OperatorToken.Kind()));
                    return;
                }

                AddInstruction(Instruction.PopRegister(3));
                AddInstruction(Instruction.PopRegister(4));
                AddInstructions(Instruction.NoOp(4));

                AddInstruction(new Instruction
                {
                    OpCode = opCode,
                    OutputRegister = 5,
                    LeftInputRegister = 3,
                    RightInputRegister = 4
                });

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
            }

            private void AddInstructions(IEnumerable<Instruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    AddInstruction(instruction);
                }
            }
        }

        private static Blueprint CreateBlueprintFromCompiledProgram(CompiledProgram compiledProgram, int? width, int? height)
        {
            var program = new List<MemoryCell>();
            var data = new List<MemoryCell>();

            var address = 1;
            foreach (var instruction in compiledProgram.Instructions)
            {
                if (instruction.OpCode != OpCode.NoOp || instruction.AutoIncrement != 0)
                {
                    Console.WriteLine($"{address}: {instruction.OpCode} [{instruction.OutputRegister}] + {instruction.AutoIncrement} = [{instruction.LeftInputRegister}] + {instruction.LeftImmediateValue}, [{instruction.RightInputRegister}] + {instruction.RightImmediateValue} if [{instruction.ConditionRegister}] + {instruction.ConditionImmediateValue} {instruction.ConditionOperator}");
                    program.Add(new MemoryCell { Address = address, Filters = ConvertInstructionToFilters(instruction) });
                }

                address++;
            }

            return RomGenerator.Generate(new RomConfiguration { Width = width, Height = height }, program, data);
        }

        private static List<Filter> ConvertInstructionToFilters(Instruction instruction)
        {
            return new List<Filter>
            {
                CreateFilter('0', (int)instruction.OpCode),
                CreateFilter('1', instruction.OutputRegister),
                CreateFilter('A', instruction.AutoIncrement),
                CreateFilter('2', instruction.LeftInputRegister),
                CreateFilter('B', instruction.LeftImmediateValue),
                CreateFilter('3', instruction.RightInputRegister),
                CreateFilter('C', instruction.RightImmediateValue),
                CreateFilter('4', instruction.ConditionRegister),
                CreateFilter('D', instruction.ConditionImmediateValue),
                CreateFilter('5', (int)instruction.ConditionOperator)
            }
                .Where(filter => filter.Count != 0)
                .ToList();
        }

        private static Filter CreateFilter(char signal, int count)
        {
            return new Filter { Signal = new SignalID { Name = VirtualSignalNames.LetterOrDigit(signal), Type = SignalTypes.Virtual }, Count = count };
        }

        private class MethodContext
        {
            public Dictionary<ISymbol, int> LocalVariables { get; set; } = new Dictionary<ISymbol, int>();
            public int StackPointerOffset { get; set; } = 0;
        }

        private class CompiledProgram
        {
            public List<Instruction> Instructions { get; set; }
        }

        private class Instruction
        {
            public OpCode OpCode { get; set; }
            public int OutputRegister { get; set; }
            public int AutoIncrement { get; set; }
            public int LeftInputRegister { get; set; }
            public int LeftImmediateValue { get; set; }
            public int RightInputRegister { get; set; }
            public int RightImmediateValue { get; set; }
            public int ConditionRegister { get; set; }
            public int ConditionImmediateValue { get; set; }
            public ConditionOperator ConditionOperator { get; set; }

            public static IEnumerable<Instruction> NoOp(int cycles)
            {
                for (int index = 0; index < cycles; index++)
                {
                    yield return new Instruction
                    {
                        OpCode = OpCode.NoOp
                    };
                }
            }

            public static Instruction SetRegisterToImmediateValue(int outputRegister, int value)
            {
                return new Instruction
                {
                    OpCode = OpCode.Add,
                    OutputRegister = outputRegister,
                    LeftImmediateValue = value
                };
            }

            public static Instruction PushImmediateValue(int value)
            {
                return new Instruction
                {
                    OpCode = OpCode.Write,
                    AutoIncrement = 1,
                    LeftInputRegister = SpecialRegisters.StackPointer,
                    RightImmediateValue = value
                };
            }

            public static Instruction PushRegister(int register)
            {
                return new Instruction
                {
                    OpCode = OpCode.Write,
                    AutoIncrement = 1,
                    LeftInputRegister = SpecialRegisters.StackPointer,
                    RightInputRegister = register
                };
            }

            public static Instruction PopRegister(int register)
            {
                return new Instruction
                {
                    OpCode = OpCode.Read,
                    OutputRegister = register,
                    AutoIncrement = -1,
                    LeftInputRegister = SpecialRegisters.StackPointer
                };
            }

            public static Instruction AdjustStackPointer(int value)
            {
                return new Instruction
                {
                    OpCode = OpCode.NoOp,
                    AutoIncrement = value,
                    LeftInputRegister = SpecialRegisters.StackPointer
                };
            }
        }

        private enum OpCode
        {
            NoOp,
            Multiply,
            Divide,
            Add,
            Subtract,
            Mod,
            Power,
            LeftShift,
            RightShift,
            And,
            Or,
            Xor,
            Note,
            Chord,
            Read,
            Write
        }

        private enum ConditionOperator
        {
            IsEqual,
            IsNotEqual,
            GreaterThan,
            LessThan,
            GreaterThanOrEqual,
            LessThanOrEqual
        }

        private class SpecialRegisters
        {
            public const int InstructionPointer = 1;
            public const int StackPointer = 2;
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
