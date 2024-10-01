using System;
using System.IO;
using System.Text;

namespace Fluss.Regen.Helpers;

public class CodeWriter : TextWriter
{
    private readonly TextWriter _writer;
    private readonly bool _disposeWriter;
    private bool _disposed;
    private int _indent;

    public CodeWriter(TextWriter writer)
    {
        _writer = writer;
        _disposeWriter = false;
    }

    public CodeWriter(StringBuilder text)
    {
        _writer = new StringWriter(text);
        _disposeWriter = true;
    }

    public override Encoding Encoding { get; } = Encoding.UTF8;

    public override void Write(char value) =>
        _writer.Write(value);

    public void WriteIndent()
    {
        var spaces = _indent * 4;
        for (var i = 0; i < spaces; i++)
        {
            Write(' ');
        }
    }

    public void WriteIndentedLine(string format, params object?[] args)
    {
        WriteIndent();

        if (args.Length == 0)
        {
            Write(format);
        }
        else
        {
            Write(format, args);
        }

        WriteLine();
    }

    public void WriteIndented(string format, params object?[] args)
    {
        WriteIndent();

        if (args.Length == 0)
        {
            Write(format);
        }
        else
        {
            Write(format, args);
        }
    }

    public IDisposable IncreaseIndent()
    {
        _indent++;
        return new Block(DecreaseIndent);
    }

    public void DecreaseIndent()
    {
        if (_indent > 0)
        {
            _indent--;
        }
    }

    public IDisposable WriteBraces()
    {
        WriteLeftBrace();
        WriteLine();

#pragma warning disable CA2000
        var indent = IncreaseIndent();
#pragma warning restore CA2000

        return new Block(() =>
        {
            indent.Dispose();
            WriteIndent();
            WriteRightBrace();
            WriteLine();
        });
    }

    public void WriteLeftBrace() => Write('{');

    public void WriteRightBrace() => Write('}');

    public override void Flush()
    {
        base.Flush();
        _writer.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && _disposeWriter)
        {
            if (disposing)
            {
                _writer.Dispose();
            }
            _disposed = true;
        }
    }

    internal CommaSeparatedWriter CommaSeparatedIndented()
    {
        return new CommaSeparatedWriter(this);
    }


    private sealed class Block : IDisposable
    {
        private readonly Action _decrease;

        public Block(Action close)
        {
            _decrease = close;
        }

        public void Dispose() => _decrease();
    }

    public sealed class CommaSeparatedWriter : IDisposable
    {
        private readonly CodeWriter _writer;
        private bool _first = true;
        public CommaSeparatedWriter(CodeWriter writer)
        {
            _writer = writer;
            _writer.IncreaseIndent();
        }

        public void Write(string value)
        {
            if (!_first)
            {
                _writer.Write(",");
                _writer.WriteLine();
            }
            _first = false;
            _writer.WriteIndented(value);
        }

        public void Dispose()
        {
            _writer.DecreaseIndent();
            _writer.WriteLine();
        }
    }
}
