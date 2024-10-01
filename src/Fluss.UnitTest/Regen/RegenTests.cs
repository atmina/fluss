using Fluss.Regen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;

namespace Fluss.UnitTest.Regen;

public class RegenTests
{
    [Fact]
    public Task GeneratesForAsyncSelector()
    {
        var runResult = GenerateFor(
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
            """);

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForNonAsyncSelector()
    {
        var runResult = GenerateFor(
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
            """);

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForUnitOfWorkSelector()
    {
        var runResult = GenerateFor(
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
            """);

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForAggregateValidator()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.Validation;

            namespace TestNamespace;
            
            public record TestAggregate : AggregateRoot
            {
                public TestAggregate When(EventEnvelope envelope) {
                    return this;
                }
            }

            public class TestAggregateValidator : AggregateValidator<TestAggregate>
            {
                public ValueTask ValidateAsync(TestAggregate aggregateAfterEvent, IUnitOfWork unitOfWorkBeforeEvent) {
                    return ValueTask.CompletedTask;
                }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GenerateForEventValidator()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.Validation;

            namespace TestNamespace;
            
            public record TestEvent : Event;

            public class TestEventValidator : EventValidator<TestEvent>
            {
                public ValueTask Validate(TestEvent @event, IUnitOfWork unitOfWorkBeforeEvent) {
                    return ValueTask.CompletedTask;
                }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GenerateForPolicy()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.Authentication;

            namespace TestNamespace;

            public class TestPolicy : Policy;
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GenerateForSideEffect()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.SideEffects;

            namespace TestNamespace;
            
            public record TestEvent : Event;

            public class TestSideEffect : SideEffect<TestEvent> {
                public Task<IEnumerable<Event>> HandleAsync(T @event, UnitOfWork unitOfWork) {
                    return Task.FromResult<IEnumerable<Event>>(new List<Event>());
                }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GenerateForUpcaster()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.Upcasting;
            using Newtonsoft.Json.Linq;

            namespace TestNamespace;

            public class TestUpcaster : IUpcaster {
                public IEnumerable<JObject>? Upcast(JObject eventJson) {
                    return null;
                }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesNothingForUninterestingClasses()
    {
        var runResult = GenerateFor(
            """
            using Fluss;
            using Fluss.Upcasting;
            
            namespace TestNamespace;

            public class Uninteresting {
            
            }
            """
        );

        return Verify(runResult);
    }

    public static GeneratorDriverRunResult GenerateFor(string source)
    {
        var generator = new RepetitiveEventsourcingCodeGenerator();

        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(RegenTests),
            [
                CSharpSyntaxTree.ParseText(source)
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(UnitOfWork).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(JObject).Assembly.Location),
            ]);

        var runResult = driver.RunGenerators(compilation).GetRunResult();

        return runResult;
    }
}