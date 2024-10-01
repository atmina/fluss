using System;
using System.Text;
using Fluss.Regen.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace Fluss.Regen.Generators;

public sealed class RegistrationSyntaxGenerator : IDisposable
{
    private readonly string _moduleName;
    private readonly string _ns;
    private StringBuilder _sb;
    private CodeWriter _writer;
    private bool _disposed;

    public RegistrationSyntaxGenerator(string moduleName, string ns)
    {
        _moduleName = moduleName;
        _ns = ns;
        _sb = StringBuilderPool.Get();
        _writer = new CodeWriter(_sb);
    }

    public void WriteHeader()
    {
        _writer.WriteFileHeader();
        _writer.WriteLine();
    }

    public void WriteBeginNamespace()
    {
        _writer.WriteIndentedLine("namespace {0} {{", _ns);
        _writer.IncreaseIndent();
    }

    public void WriteEndNamespace()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteBeginClass()
    {
        _writer.WriteIndentedLine("public static partial class {0}ComponentsServiceCollectionExtensions {{", _moduleName);
        _writer.IncreaseIndent();
    }

    public void WriteEndClass()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteBeginRegistrationMethod(string componentType)
    {
        _writer.WriteIndentedLine(
            "public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add{0}{1}(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection sc) {{",
            _moduleName, componentType);
        _writer.IncreaseIndent();
    }

    public void WriteEndRegistrationMethod(bool includeNewLine = true)
    {
        _writer.WriteIndentedLine("return sc;");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        if (includeNewLine)
        {
            _writer.WriteLine();
        }
    }

    public void WriteAggregateValidatorRegistration(string aggregateValidatorType)
    {
        _writer.WriteIndentedLine("global::Fluss.Validation.ValidationServiceCollectionExtension.AddAggregateValidator<{0}>(sc);", aggregateValidatorType);
    }

    public void WriteEventValidatorRegistration(string eventValidatorType)
    {
        _writer.WriteIndentedLine("global::Fluss.Validation.ValidationServiceCollectionExtension.AddEventValidator<{0}>(sc);", eventValidatorType);
    }

    public void WritePolicyRegistration(string policyType)
    {
        _writer.WriteIndentedLine("global::Fluss.Authentication.ServiceCollectionExtensions.AddPolicy<{0}>(sc);", policyType);
    }

    public void WriteSideEffectRegistration(string sideEffectType)
    {
        _writer.WriteIndentedLine("global::Fluss.SideEffects.SideEffectsServiceCollectionExtension.AddSideEffect<{0}>(sc);", sideEffectType);
    }

    public void WriteUpcasterRegistration(string upcasterType)
    {
        _writer.WriteIndentedLine("global::Fluss.ServiceCollectionExtensions.AddUpcaster<{0}>(sc);", upcasterType);
    }

    public void WriteComponentRegistration(string componentName)
    {
        _writer.WriteIndentedLine("Add{0}{1}(sc);", _moduleName, componentName);
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
