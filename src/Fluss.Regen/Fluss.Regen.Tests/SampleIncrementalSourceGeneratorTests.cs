using Fluss.Regen.Tests.Utils.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Fluss.Regen.Tests;

public class SampleIncrementalSourceGeneratorTests
{
    [Fact]
    public void GeneratesForNonAsyncSelector()
    {
        var generator = new AutoLoadGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(SampleIncrementalSourceGeneratorTests),
            [CSharpSyntaxTree.ParseText(
                @"
using Fluss.Regen;

namespace TestNamespace;

public class Test
{
    [Selector]
    public static int Add(int a, int b) {
        return a + b;
    }
}")],
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            });

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        
        runResult.MatchMarkdownSnapshot();
    }
    
    [Fact]
    public void GeneratesForAsyncSelector()
    {
        var generator = new AutoLoadGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(SampleIncrementalSourceGeneratorTests),
            [CSharpSyntaxTree.ParseText(
                @"
using Fluss.Regen;
using System.Threading.Tasks;

namespace TestNamespace;

public class Test
{
    [Selector]
    public static async ValueTask<int> Add(int a, int b) {
        return a + b;
    }

    [Selector]
    public static async ValueTask<int> Add2(int a, int b) {
        return a + b;
    }
}")],
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            });

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        
        runResult.MatchMarkdownSnapshot();
    }
}