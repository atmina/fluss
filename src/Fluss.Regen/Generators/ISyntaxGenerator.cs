using System.Collections.Immutable;
using Fluss.Regen.Inspectors;
using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Generators;

public interface ISyntaxGenerator
{
    void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SyntaxInfo> syntaxInfos);
}