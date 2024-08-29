using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluss.Regen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Fluss.Regen.Generators;

public sealed class SelectorSyntaxGenerator : IDisposable
{
    private StringBuilder _sb;
    private CodeWriter _writer;
    private bool _disposed;

    public SelectorSyntaxGenerator()
    {
        _sb = StringBuilderPool.Get();
        _writer = new CodeWriter(_sb);
    }

    public void WriteHeader()
    {
        _writer.WriteFileHeader();
        _writer.WriteIndentedLine("using Microsoft.Extensions.DependencyInjection;");
        _writer.WriteLine();
        _writer.WriteIndentedLine("namespace {0}", "Fluss");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteInterfaceHeader()
    {
        _writer.WriteIndentedLine("public partial interface IUnitOfWork");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteClassHeader()
    {
        _writer.WriteIndentedLine("public static class UnitOfWorkSelectors");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEndNamespace()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.WriteLine();
    }

    public void WriteSelectorInterface(
        string name,
        bool isPublic,
        ITypeSymbol key,
        ITypeSymbol value)
    {
        _writer.WriteIndentedLine(
            "{0} interface {1}",
            isPublic
                ? "public"
                : "internal",
            name);
        _writer.IncreaseIndent();

        _writer.WriteIndentedLine(
            ": global::GreenDonut.ISelector<{0}, {1}[]>",
            key.ToFullyQualified(),
            value.ToFullyQualified());

        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("{");
        _writer.WriteIndentedLine("}");
        _writer.WriteLine();
    }

    public void WriteBeginSelectorMethod(
        string name,
        string interfaceName,
        bool isPublic,
        ITypeSymbol key,
        ITypeSymbol value)
    {
        _writer.WriteIndentedLine(
            "{0} sealed class {1}",
            isPublic
                ? "public"
                : "internal",
            name);
        _writer.IncreaseIndent();

        _writer.WriteIndentedLine(
            ": global::GreenDonut.CacheSelector<{0}, {1}>",
            key.ToFullyQualified(),
            value.ToFullyQualified());

        _writer.WriteIndentedLine(", {0}", interfaceName);
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEndSelectorClass()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.WriteLine();
    }
    
    public void WriteStartSelectorSelectInterfaceMethod(string methodName,
        ITypeSymbol value)
    {
        _writer.WriteIndentedLine(
            "public global::{0}<{1}> Select{2}(",
            typeof(ValueTask).FullName,
            value.ToFullyQualified(),
            methodName);
        _writer.IncreaseIndent();
    }
    
    public void WriteStartSelectorSelectMethod(string methodName,
        ITypeSymbol value)
    {
        _writer.WriteIndentedLine(
            "public static async global::{0}<{1}> Select{2}(this global::Fluss.IUnitOfWork unitOfWork, ",
            typeof(ValueTask).FullName,
            value.ToFullyQualified(),
            methodName);
        _writer.IncreaseIndent();
    }

    public void WriteSelectorMethodParameter(ITypeSymbol parameterType, string parameterName, bool isLast)
    {
        _writer.WriteIndentedLine(
            "{0} {1}{2}",
            parameterType.ToFullyQualified(),
            parameterName,
            isLast ? "" : ","
            );
    }

    public void WriteSelectorMethodCall(string containingType, string methodName, bool isAsync)
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine(")");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("return {0}global::{1}.{2}(", isAsync ? "await " : "", containingType, methodName);
        _writer.IncreaseIndent();
    }

    public void WriteSelectorMethodCallParameter(string parameterName, bool isLast)
    {
        _writer.WriteIndentedLine("{0}{1}", parameterName, isLast ? "" : ",");
    }

    public void WriteSelectorInterfaceEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine(");");
    }

    public void WriteSelectorMethodEnd(bool isAsync)
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("){0};", isAsync ? ".ConfigureAwait(false)" : "");
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