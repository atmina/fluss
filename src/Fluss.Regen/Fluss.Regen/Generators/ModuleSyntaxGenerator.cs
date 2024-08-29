using System;
using System.Text;
using Fluss.Regen.Helpers;
using Microsoft.CodeAnalysis.Text;

namespace Fluss.Regen.Generators;

public sealed class ModuleSyntaxGenerator : IDisposable
{
    private readonly string _moduleName;
    private readonly string _ns;
    private StringBuilder _sb;
    private CodeWriter _writer;
    private bool _disposed;

    public ModuleSyntaxGenerator(string moduleName, string ns)
    {
        _moduleName = moduleName;
        _ns = ns;
        _sb = StringBuilderPool.Get();
        _writer = new CodeWriter(_sb);
    }

    public void WriterHeader()
    {
        _writer.WriteFileHeader();
        _writer.WriteLine();
    }

    public void WriteBeginNamespace()
    {
        _writer.WriteIndentedLine("namespace {0}", _ns);
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEndNamespace()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteBeginClass()
    {
        _writer.WriteIndentedLine("public static partial class {0}RequestExecutorBuilderExtensions", _moduleName);
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEndClass()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteBeginRegistrationMethod()
    {
        _writer.WriteIndentedLine(
            "public static IRequestExecutorBuilder Add{0}(this IRequestExecutorBuilder builder)",
            _moduleName);
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEndRegistrationMethod()
    {
        _writer.WriteIndentedLine("return builder;");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteRegisterType(string typeName)
        => _writer.WriteIndentedLine("builder.AddType<global::{0}>();", typeName);

    public void WriteRegisterTypeExtension(string typeName, bool staticType)
        => _writer.WriteIndentedLine(
            staticType
                ? "builder.AddTypeExtension(typeof(global::{0}));"
                : "builder.AddTypeExtension<global::{0}>();",
            typeName);

    public void WriteRegisterObjectTypeExtension(string runtimeTypeName, string extensionType)
    {
        _writer.WriteIndentedLine(
            "AddTypeExtension_8734371<{0}>(builder, {1}.Initialize);",
            runtimeTypeName,
            extensionType);
    }

    public void WriteRegisterObjectTypeExtensionHelpers()
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("private static void AddTypeExtension_8734371<T>(");

        using (_writer.IncreaseIndent())
        {
            _writer.WriteIndentedLine("global::HotChocolate.Execution.Configuration.IRequestExecutorBuilder builder,");
            _writer.WriteIndentedLine("Action<IObjectTypeDescriptor<T>> initialize)");
        }

        _writer.WriteIndentedLine("{");

        using (_writer.IncreaseIndent())
        {
            _writer.WriteIndentedLine("builder.ConfigureSchema(sb =>");
            _writer.WriteIndentedLine("{");

            using (_writer.IncreaseIndent())
            {
                _writer.WriteIndentedLine("string typeName = typeof(T).FullName!;");
                _writer.WriteIndentedLine("string typeKey = $\"8734371_Type_ObjectType<{typeName}>\";");
                _writer.WriteIndentedLine("string hooksKey = $\"8734371_Hooks_ObjectType<{typeName}>\";");
                _writer.WriteLine();
                _writer.WriteIndentedLine("if (!sb.ContextData.ContainsKey(typeKey))");
                _writer.WriteIndentedLine("{");

                using (_writer.IncreaseIndent())
                {
                    _writer.WriteIndentedLine("sb.AddObjectType<T>(");
                    using (_writer.IncreaseIndent())
                    {
                        _writer.WriteIndentedLine("descriptor =>");
                        _writer.WriteIndentedLine("{");

                        using (_writer.IncreaseIndent())
                        {
                            _writer.WriteIndentedLine(
                                "var hooks = (global::System.Collections.Generic.List<" +
                                "Action<IObjectTypeDescriptor<T>>>)" +
                                "descriptor.Extend().Context.ContextData[hooksKey]!;");
                            _writer.WriteIndentedLine("foreach (var configure in hooks)");
                            _writer.WriteIndentedLine("{");

                            using (_writer.IncreaseIndent())
                            {
                                _writer.WriteIndentedLine("configure(descriptor);");
                            }

                            _writer.WriteIndentedLine("};");
                        }

                        _writer.WriteIndentedLine("});");
                    }

                    _writer.WriteIndentedLine("sb.ContextData.Add(typeKey, null);");
                }

                _writer.WriteIndentedLine("}");
                _writer.WriteLine();

                _writer.WriteIndentedLine("if (!sb.ContextData.TryGetValue(hooksKey, out var value))");
                _writer.WriteIndentedLine("{");

                using (_writer.IncreaseIndent())
                {
                    _writer.WriteIndentedLine(
                        "value = new System.Collections.Generic.List<Action<IObjectTypeDescriptor<T>>>();");
                    _writer.WriteIndentedLine("sb.ContextData.Add(hooksKey, value);");
                }

                _writer.WriteIndentedLine("}");
                _writer.WriteLine();
                _writer.WriteIndentedLine(
                    "((System.Collections.Generic.List<Action<IObjectTypeDescriptor<T>>>)value!)" +
                    ".Add(initialize);");
            }

            _writer.WriteIndentedLine("});");
        }

        _writer.WriteIndentedLine("}");
    }

    public void WriteRegisterSelector(string typeName)
        => _writer.WriteIndentedLine("builder.AddSelector<global::{0}>();", typeName);

    public void WriteRegisterSelector(string typeName, string interfaceTypeName)
        => _writer.WriteIndentedLine("builder.AddSelector<global::{0}, global::{1}>();", interfaceTypeName, typeName);
    
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
