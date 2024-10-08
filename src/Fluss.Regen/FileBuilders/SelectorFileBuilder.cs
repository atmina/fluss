using System;
using System.Text;
using System.Threading.Tasks;
using Fluss.Regen.Generators;
using Fluss.Regen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Fluss.Regen.FileBuilders;

public sealed class SelectorFileBuilder : IDisposable
{
    private StringBuilder _sb;
    private CodeWriter _writer;
    private bool _disposed;

    public SelectorFileBuilder()
    {
        _sb = StringBuilderPool.Get();
        _writer = new CodeWriter(_sb);
    }

    public void WriteHeader()
    {
        _writer.WriteFileHeader();
        _writer.WriteLine();
        _writer.WriteIndentedLine("namespace {0}", "Fluss");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteClassHeader()
    {
        _writer.WriteIndentedLine("public static class UnitOfWorkSelectors");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("private static global::Microsoft.Extensions.Caching.Memory.MemoryCache _cache = new (new global::Microsoft.Extensions.Caching.Memory.MemoryCacheOptions { SizeLimit = 1024 });");
    }

    public void WriteEndNamespace()
    {
        _writer.WriteIndentedLine("private record CacheEntryValue(object Value, global::System.Collections.Generic.IReadOnlyList<global::Fluss.UnitOfWorkRecordingProxy.EventListenerTypeWithKeyAndVersion>? EventListeners);");
        _writer.WriteLine();
        _writer.WriteIndented("private static async global::System.Threading.Tasks.ValueTask<bool> MatchesEventListenerState(global::Fluss.IUnitOfWork unitOfWork, CacheEntryValue value) ");
        using (_writer.WriteBraces())
        {
            _writer.WriteIndented("foreach (var eventListenerData in value.EventListeners ?? global::System.Array.Empty<global::Fluss.UnitOfWorkRecordingProxy.EventListenerTypeWithKeyAndVersion>()) ");
            using (_writer.WriteBraces())
            {
                _writer.WriteIndented("if (!await eventListenerData.IsStillUpToDate(unitOfWork)) ");
                using (_writer.WriteBraces())
                {
                    _writer.WriteIndentedLine("return false;");
                }
            }

            _writer.WriteIndentedLine("return true;");
        }

        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.WriteLine();
    }

    public void WriteMethodSignatureStart(string methodName, string returnType, bool noParameters)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine(
            "public static async global::{0}<{1}> Select{2}(this global::Fluss.IUnitOfWork unitOfWork{3}",
            typeof(ValueTask).FullName,
            returnType,
            methodName,
            noParameters ? "" : ", ");
        _writer.IncreaseIndent();
    }

    public void WriteMethodSignatureParameter(string parameterType, string parameterName, bool isLast)
    {
        _writer.WriteIndentedLine(
            "{0} {1}{2}",
            parameterType,
            parameterName,
            isLast ? "" : ","
            );
    }

    public void WriteMethodSignatureEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine(")");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteRecordingUnitOfWork()
    {
        _writer.WriteIndentedLine("var recordingUnitOfWork = new global::Fluss.UnitOfWorkRecordingProxy(unitOfWork);");
    }

    public void WriteKeyStart(string containingType, string methodName, bool noParameters)
    {
        _writer.WriteIndentedLine("var key = (");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("\"{0}.{1}\"{2}", containingType, methodName, noParameters ? "" : ",");
    }

    public void WriteKeyParameter(string parameterName, bool isLast)
    {
        _writer.WriteIndentedLine("{0}{1}", parameterName, isLast ? "" : ",");
    }

    public void WriteKeyEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine(");");
        _writer.WriteLine();
    }

    public void WriteMethodCacheHit(string returnType)
    {
        _writer.WriteIndented("if (_cache.TryGetValue(key, out var result) && result is CacheEntryValue entryValue && await MatchesEventListenerState(unitOfWork, entryValue)) ");
        using (_writer.WriteBraces())
        {
            _writer.WriteIndentedLine("return ({0})entryValue.Value;", returnType);
        }
        _writer.WriteLine();
    }

    public void WriteMethodCall(string containingType, string methodName, bool isAsync)
    {
        _writer.WriteIndentedLine("result = {0}{1}.{2}(", isAsync ? "await " : "", containingType, methodName);
        _writer.IncreaseIndent();
    }

    public void WriteMethodCallParameter(string parameterName, bool isLast)
    {
        _writer.WriteIndentedLine("{0}{1}", parameterName, isLast ? "" : ",");
    }

    public void WriteMethodCallEnd(bool isAsync)
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("){0};", isAsync ? ".ConfigureAwait(false)" : "");
        _writer.WriteLine();
    }

    public void WriteMethodCacheMiss(string returnType)
    {
        _writer.WriteIndented("using (var entry = _cache.CreateEntry(key)) ");

        using (_writer.WriteBraces())
        {
            _writer.WriteIndentedLine("entry.Value = new CacheEntryValue(result, recordingUnitOfWork.GetRecordedListeners());");
            _writer.WriteIndentedLine("entry.Size = 1;");
        }

        _writer.WriteLine();
        _writer.WriteIndentedLine("return ({0})result;", returnType);
    }

    public void WriteMethodEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public override string ToString()
        => _sb.ToString();

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StringBuilderPool.Return(_sb);
        _sb = default!;
        _writer = default!;
        _disposed = true;
    }
}
