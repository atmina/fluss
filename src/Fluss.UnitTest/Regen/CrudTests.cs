namespace Fluss.UnitTest.Regen;

public class CrudTests
{
    [Fact]
    public Task GeneratesForSimpleCase()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public int Test { get; set; }
                public int Test2 { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesForSimpleCaseWithId()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public int Id { get; set; }
                public int Test { get; set; } = 23;
                public int Test2 { get; set; } = 42;
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task GeneratesCallToGivenAggregateExtendWhen()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;
            
            [Crud]
            public partial class TestCrud
            {
                public global::System.Guid Id { get; set; }
                
                public partial record Aggregate : AggregateRoot
                {
                    public Aggregate ExtendWhen(EventEnvelope envelope)
                    {
                        return this;
                    }
                }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenClassIsNotPartial()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public class TestCrud {
                public int Id { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenIdPropertyTypeIsNotSupported()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public string Id { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenDuplicatePropertyName()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public int Test { get; set; }
                public string Test { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenReservedPropertyName()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public bool Exists { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenNamespaceMissing()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            [Crud]
            public partial class TestCrud {
                public int Id { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenUnsupportedPropertyType()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;
            using System.Collections.Generic;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public List<int> Test { get; set; }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenNamingConflict()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            [Crud]
            public partial class TestCrud {
                public int Id { get; set; }
                public void TestCrudCreate() { }
            }
            """
        );

        return Verify(runResult);
    }

    [Fact]
    public Task DiagnosticWhenInvalidInheritance()
    {
        var runResult = RegenTests.GenerateFor(
            """
            using Fluss.Regen;

            namespace TestNamespace;

            public class BaseClass { }

            [Crud]
            public partial class TestCrud : BaseClass {
                public int Id { get; set; }
            }
            """
        );

        return Verify(runResult);
    }
}