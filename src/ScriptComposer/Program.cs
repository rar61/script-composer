using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Buildalyzer;
using Buildalyzer.Workspaces;


namespace SEScriptComposer
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Argument<string> { Name = "namespace", Description = "Root namespace" },
                new Option<FileInfo>(new[] { "--project", "-p" }, "Path to project file")
            };

            rootCommand.Handler = CommandHandler.Create<string, FileInfo>(ComposeScript);

            rootCommand.Invoke(args);
        }

        static void ComposeScript(string @namespace, FileInfo projectPath)
        {
            if (projectPath == null)
            {
                var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
                projectPath = cwd.EnumerateFiles().FirstOrDefault(info => info.Extension == ".csproj");
                if (projectPath == null)
                {
                    Console.WriteLine("project file not found in current directory");
                    return;
                }
            }
            else if (!projectPath.Exists)
            {
                Console.WriteLine("specified project does not exists");
                return;
            }

            Workspace workspace = new AnalyzerManager().GetProject(projectPath.FullName).GetWorkspace();
            Project project = workspace.CurrentSolution.Projects.First();

            Compilation compilation = project.GetCompilationAsync().Result;

            var declaredNamespaces = new HashSet<INamespaceSymbol>(SymbolEqualityComparer.Default);
            INamedTypeSymbol rootClass = null;

            foreach (Document document in project.Documents)
            {
                var syntaxTree = document.GetSyntaxTreeAsync().Result;
                var syntaxRoot = syntaxTree.GetCompilationUnitRoot();
                SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);

                var documentNamespaces = syntaxRoot
                    .ChildNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .Select(node => semanticModel.GetDeclaredSymbol(node));

                if (rootClass == null)
                {
                    foreach (INamespaceSymbol namespaceSymbol in documentNamespaces)
                    {
                        if (namespaceSymbol.ToDisplayString() == @namespace)
                        {
                            rootClass = namespaceSymbol.GetTypeMembers().FirstOrDefault();
                            if (rootClass?.BaseType is INamedTypeSymbol symbol)
                            {
                                if (symbol.Name != "MyGridProgram")
                                {
                                    Console.WriteLine("base class not inhereting from MyGridProgram");
                                    return;
                                }
                                break;
                            }
                        }
                    }
                }

                declaredNamespaces.UnionWith(documentNamespaces);
            }

            if (rootClass == null)
            {
                Console.WriteLine("base class not found");
                return;
            }

            var requiredNamespaces = new HashSet<INamespaceSymbol>
            (
                rootClass.DeclaringSyntaxReferences.SelectMany
                (
                    reference => reference.SyntaxTree.GetCompilationUnitRoot().Usings.Select
                    (
                        node => (INamespaceSymbol)compilation
                            .GetSemanticModel(reference.SyntaxTree)
                            .GetSymbolInfo(node.Name).Symbol
                    )
                ),
                SymbolEqualityComparer.Default
            );

            requiredNamespaces.IntersectWith(declaredNamespaces);

            var requiredNodes = GetRequiredNamespaces(requiredNamespaces, declaredNamespaces, compilation).SelectMany
            (
                ns => ns.DeclaringSyntaxReferences.Where
                (
                    reference => ns.Equals
                    (
                        compilation.GetSemanticModel(reference.SyntaxTree).GetDeclaredSymbol(reference.GetSyntax()),
                        SymbolEqualityComparer.Default
                    )
                )
                .SelectMany(reference => ((NamespaceDeclarationSyntax)reference.GetSyntax()).Members)
            );

            var nodes = rootClass.DeclaringSyntaxReferences
                .SelectMany(reference => ((ClassDeclarationSyntax)reference.GetSyntax()).Members)
                .Concat(requiredNodes);


            var compilationUnit = SyntaxFactory.CompilationUnit
            (
                new SyntaxList<ExternAliasDirectiveSyntax>(),
                new SyntaxList<UsingDirectiveSyntax>(),
                new SyntaxList<AttributeListSyntax>(),
                new SyntaxList<MemberDeclarationSyntax>(nodes)
            );

            TextCopy.Clipboard.SetText(Formatter.Format(compilationUnit, workspace).ToFullString());
            Console.WriteLine("result copied to clipboard");
            Console.Beep();
        }

        static IEnumerable<INamespaceSymbol> GetRequiredNamespaces(
            IEnumerable<INamespaceSymbol> namespaces,
            IEnumerable<INamespaceSymbol> declaredNamespaces,
            Compilation compilation
        )
        {
            var childNamspaces = new HashSet<INamespaceSymbol>
            (
                namespaces.SelectMany
                (
                    ns => ns.DeclaringSyntaxReferences.Where
                    (
                        reference => ns.Equals
                        (
                            compilation.GetSemanticModel(reference.SyntaxTree).GetDeclaredSymbol(reference.GetSyntax()),
                            SymbolEqualityComparer.Default
                        )
                    )
                    .SelectMany
                    (
                        reference => reference.SyntaxTree.GetCompilationUnitRoot().Usings.Select
                        (
                            node => (INamespaceSymbol)compilation
                                    .GetSemanticModel(reference.SyntaxTree)
                                    .GetSymbolInfo(node.Name).Symbol
                        )
                    )
                ),
                SymbolEqualityComparer.Default
            );

            childNamspaces.IntersectWith(declaredNamespaces);
            if (childNamspaces.Count == 0)
                return Enumerable.Empty<INamespaceSymbol>();

            return namespaces.Concat(GetRequiredNamespaces(childNamspaces, declaredNamespaces, compilation));
        }
    }
}