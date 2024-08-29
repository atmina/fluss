using Fluss.Regen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Fluss.UnitTest.Regen;

public class SelectorGeneratorTests
{
    [Fact]
    public Task GeneratesForAsyncSelector()
    {
        var generator = new SelectorGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(SelectorGeneratorTests),
            [
                CSharpSyntaxTree.ParseText(
                    """

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
                    }
                    """)
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForNonAsyncSelector()
    {
        var generator = new SelectorGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(SelectorGeneratorTests),
            [
                CSharpSyntaxTree.ParseText(
                    """

                    using Fluss.Regen;

                    namespace TestNamespace;

                    public class Test
                    {
                        [Selector]
                        public static int Add(int a, int b) {
                            return a + b;
                        }
                    }
                    """)
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ]);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForUnitOfWorkSelector()
    {
        var generator = new SelectorGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(SelectorGeneratorTests),
            [
                CSharpSyntaxTree.ParseText(
                    """

                    using Fluss;
                    using Fluss.Regen;

                    namespace TestNamespace;

                    public class Test
                    {
                        [Selector]
                        public static int Add(IUnitOfWork unitOfWork, int a, int b) {
                            return a + b;
                        }
                    }
                    """)
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(UnitOfWork).Assembly.Location)
            ]);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        return Verify(runResult);
    }
}