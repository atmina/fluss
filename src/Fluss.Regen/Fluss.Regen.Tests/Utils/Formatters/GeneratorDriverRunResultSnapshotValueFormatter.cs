using System;
using System.Buffers;
using Fluss.Regen.Tests.Utils.Extensions;
using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Tests.Utils.Formatters;

public class GeneratorDriverRunResultSnapshotValueFormatter : SnapshotValueFormatter<GeneratorDriverRunResult>
{
    protected override void Format(IBufferWriter<byte> snapshot, GeneratorDriverRunResult value)
    {
        throw new NotImplementedException();
    }

    protected override void FormatMarkdown(IBufferWriter<byte> snapshot, GeneratorDriverRunResult value)
    {
        foreach (var tree in value.GeneratedTrees)
        {
            snapshot.Append($"## {tree.FilePath}");
            snapshot.AppendLine();
            snapshot.Append("```csharp");
            snapshot.AppendLine();
            snapshot.Append(tree.GetText().ToString());
            snapshot.AppendLine();
            snapshot.Append("```");
            snapshot.AppendLine();
            snapshot.AppendLine();
        }
    }
}
