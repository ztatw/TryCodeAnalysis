using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GWTVisitor;

public class GWTSyntaxVisitor: CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;

    public GWTSyntaxVisitor(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax invokeExp)
    {
        var symInfo = _semanticModel.GetSymbolInfo(invokeExp);
        if (symInfo.Symbol is IMethodSymbol {Name:"Given"} or IMethodSymbol {Name: "When"} or IMethodSymbol {Name: "Then"})
        {
            var normalizedText = string.Empty;
            var name = string.Empty;
            SyntaxNode actionNode = null;
            var gwtArgs = invokeExp.ArgumentList.Arguments;
            if (gwtArgs.Count == 2)
            {
                var nameExp = (LiteralExpressionSyntax) gwtArgs.First().Expression;
                name = nameExp.GetText().ToString();
                
                var actionExp = gwtArgs.Last().Expression;
                if (actionExp is ParenthesizedLambdaExpressionSyntax ple)
                {
                    if (ple.Block == null)
                    {
                        // Given/When/Then("xx", ()=>foo.bar());
                        if (ple.Body is InvocationExpressionSyntax)
                        {
                            actionNode = ple.Body;
                            var invokeText = ple.Body.GetText().ToString().Trim();
                            normalizedText = invokeText.EndsWith(")") ? $"{invokeText};" : invokeText;
                        }
                    }
                    else
                    {
                        //Given/When/Then("xxx", ()=>{foo.bar();bar.bzz();...});
                        var blockText = ple.Block.GetText().ToString().Trim();
                        normalizedText = blockText[(blockText.IndexOf("{")+1)..blockText.LastIndexOf("}")];
                    }
                } else if (actionExp is IdentifierNameSyntax idns)
                {
                    //Given/When/Then("xxx", act);
                    var idText = idns.GetText().ToString();
                    normalizedText = $"{idText}();";
                }

                if (actionNode != null)
                {
                    return invokeExp.ReplaceNode(invokeExp, actionNode);
                }
            }
        }
        return base.VisitInvocationExpression(invokeExp);
    }
}