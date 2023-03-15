// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using System.Linq;
using FluentNHibernate.Cfg;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NHibernate;

var source = @"
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;

namespace TryCodeAnalysis;

public class Example
{
    public static void Main(string args)
    {
        var msSqlConfiguration = MsSqlConfiguration.MsSql2008.ConnectionString("");

        var fluentConfiguration = Fluently.Configure().Database(msSqlConfiguration);
        var sessionFactory = fluentConfiguration.BuildSessionFactory();
        ISession session = sessionFactory.OpenSession();
        session.FlushMode = FlushMode.Commit;


        var entities = GetEntities(session);


        static List<MyEntity> GetEntities(ISession session)
        {
            var ids = new List<long>();
            return session.Query<MyEntity>()
                .Select(e => e)
                .Where(e => ids.Contains(e.Id))
                .ToList();
        }
    }

    internal class MyEntity
    {
        public long Id { get; set; }
    }
}
";


var tree = CSharpSyntaxTree.ParseText(source);

var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
var fmlib = MetadataReference.CreateFromFile(typeof(Fluently).Assembly.Location);
var nhlib = MetadataReference.CreateFromFile(typeof(NHibernateLogger).Assembly.Location);
var libPath = typeof(IQueryable).Assembly.Location;
var libDir = Path.GetDirectoryName(libPath);

var compilation = CSharpCompilation.Create("example", new[] {tree}, new[]
{
    mscorlib,
    MetadataReference.CreateFromFile($"{libDir}\\System.Runtime.dll"),
    MetadataReference.CreateFromFile($"{libDir}\\System.Collections.dll"),
    fmlib, nhlib,
    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
    MetadataReference.CreateFromFile($"{libDir}\\System.Linq.dll"),
    MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Expressions.dll"),
    MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Parallel.dll"),
    MetadataReference.CreateFromFile($"{libDir}\\System.Linq.Queryable.dll")
});
var result = compilation.Emit(new MemoryStream());
if (!result.Success)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic.ToString());
    }
}

var model = compilation.GetSemanticModel(tree);

var root = tree.GetRoot();
root.DescendantNodes().OfType<InvocationExpressionSyntax>()
    .ToList()
    .ForEach(invokeExp =>
    {
        var symInfo = model.GetSymbolInfo(invokeExp);
        if (symInfo.Symbol is IMethodSymbol {Name:"Where", ReturnType.Name: "IQueryable"})
        {
            Console.WriteLine($"Found call Where on IQueryable: expr: {invokeExp.Expression.ToString()}");
            var whereArgs = invokeExp.ArgumentList.Arguments.SingleOrDefault();
            if (whereArgs != null)
            {
                whereArgs.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .ToList()
                    .ForEach(whereInvokeExp =>
                    {
                        var sym = model.GetSymbolInfo(whereInvokeExp);

                        if (sym.Symbol != null && 
                            sym.Symbol.Name == "Contains" &&
                            sym.Symbol.ContainingType.Name == "List" && sym.Symbol.ContainingType.ContainingType == null)
                        {
                            Console.WriteLine("found call List<T>.Contains");
                        }
                    });
            }
        }
    });