using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

var source = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace GWTRemover;

public class SourcePlayground
{
    private static void Given(string name, Action establish)
    {
        establish();
    }

    private static void ExampleAction(Expression<Func<int, int>> expression) {}

    public static void Main(string[] args)
    {
        Given(""xx"", () => {                         });
    }
}
";
// var tree = CSharpSyntaxTree.ParseText(source);
// var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
// var libPath = typeof(IQueryable).Assembly.Location;
// var libDir = Path.GetDirectoryName(libPath);
// var compilation = CSharpCompilation.Create("example", new[] {tree}, new[]
// {
//     mscorlib,
//     MetadataReference.CreateFromFile($"{libDir}\\System.Runtime.dll"),
//     MetadataReference.CreateFromFile($"{libDir}\\System.Collections.dll"),
//     MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
//     MetadataReference.CreateFromFile($"{libDir}\\System.Linq.dll"),
//     MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Expressions.dll"),
//     MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Parallel.dll"),
//     MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Queryable.dll")
// });
// var result = compilation.Emit(new MemoryStream());
// if (!result.Success)
// {
//     foreach (var diagnostic in result.Diagnostics)
//     {
//         Console.WriteLine(diagnostic.ToString());
//     }
//
//     Environment.Exit(-1);
// }

var workspace = new AdhocWorkspace();
var projectId = ProjectId.CreateNewId();
var versionStamp = VersionStamp.Create();
var projectInfo = ProjectInfo.Create(projectId, versionStamp, "NewProject", "projName", LanguageNames.CSharp);
var newProject = workspace.AddProject(projectInfo);
var document = workspace.AddDocument(newProject.Id, "NewFile.cs", SourceText.From(source));
var syntaxRoot = await document.GetSyntaxRootAsync();
var editor = await DocumentEditor.CreateAsync(document);

syntaxRoot.DescendantNodes().OfType<ExpressionStatementSyntax>()
    .ToList()
    .ForEach(exp =>
    {
        if (exp.Expression is not InvocationExpressionSyntax) return;
        var invokeExp = (InvocationExpressionSyntax) exp.Expression;
        if (invokeExp.Expression is not IdentifierNameSyntax) return;
        var id = (IdentifierNameSyntax) invokeExp.Expression;
        var expName = id.Identifier.Text;
        if (expName == "Given" || expName == "When" || expName == "Then")
        {
            var gwtArgs = invokeExp.ArgumentList.Arguments;
            if (gwtArgs.Count == 2)
            {
                var nameExp = (LiteralExpressionSyntax) gwtArgs.First().Expression;
                var nameComment = SyntaxFactory.Comment($"//{nameExp.ChildTokens().Single().Value}\n");
                var actionExp = gwtArgs.Last().Expression;
                if (actionExp is ParenthesizedLambdaExpressionSyntax ple)
                {
                    if (ple.Block == null)
                    {
                        // Given/When/Then("xx", ()=>foo.bar());
                        if (ple.Body is InvocationExpressionSyntax)
                        {
                            editor.InsertBefore(exp, SyntaxFactory
                                .ExpressionStatement((InvocationExpressionSyntax) ple.Body)
                                .WithTrailingTrivia(exp.GetTrailingTrivia())
                                .WithLeadingTrivia(nameComment)
                            );
                            editor.RemoveNode(exp);
                        }
                    }
                    else
                    {
                        //Given/When/Then("xxx", ()=>{foo.bar();bar.bzz();...});
                        var stats = ple.Block.Statements.ToList();
                        if (stats.Any())
                        {
                            stats[0] = stats[0].WithLeadingTrivia(exp.GetLeadingTrivia()).WithLeadingTrivia(nameComment);
                            stats[stats.Count - 1] = stats.Last().WithTrailingTrivia(exp.GetTrailingTrivia());
                            foreach (var stat in stats)
                            {
                                editor.InsertBefore(exp, stat);
                            }
                        }
                        editor.RemoveNode(exp);
                    }
                }
                else if (actionExp is IdentifierNameSyntax idns)
                {
                    //Given/When/Then("xxx", act);
                    var expressionStatementSyntax = SyntaxFactory
                        .ExpressionStatement(SyntaxFactory.InvocationExpression(idns))
                        .WithTriviaFrom(exp);
                    expressionStatementSyntax = expressionStatementSyntax.InsertTriviaBefore(expressionStatementSyntax.GetLeadingTrivia().First(),
                        exp.GetLeadingTrivia().Add(nameComment));
                    editor.InsertBefore(exp, expressionStatementSyntax
                    );
                    editor.RemoveNode(exp);
                }
                else if (actionExp is MemberAccessExpressionSyntax maes)
                {
                    //Given/When/Then("xxx", cls.act);
                    var expressionStatementSyntax = SyntaxFactory
                        .ExpressionStatement(SyntaxFactory.InvocationExpression(maes))
                        .WithTriviaFrom(exp);
                    expressionStatementSyntax = expressionStatementSyntax.InsertTriviaBefore(
                        expressionStatementSyntax.GetLeadingTrivia().First(),
                        exp.GetLeadingTrivia().Add(nameComment));
                    editor.InsertBefore(exp, expressionStatementSyntax
                    );
                    editor.RemoveNode(exp);
                }
                else
                {
                    throw new ApplicationException($"do not support: {actionExp}");
                }
            }
        }
    });
Console.WriteLine(await editor.GetChangedDocument().GetTextAsync());