namespace Fluss.Regen.Attributes;

public interface IRegenAttribute
{
    string FileName { get; }
    string SourceCode { get; }
}