namespace Fluss.Regen.Attributes;

public sealed class CrudAttribute : IRegenAttribute
{
    private const string Namespace = "Fluss.Regen";
    private const string AttributeName = "CrudAttribute";

    public static string FullName => $"{Namespace}.{AttributeName}";

    public string FileName => $"{AttributeName}.g.cs";
    public string SourceCode => $$"""
                                  namespace {{Namespace}}
                                  {
                                      [global::System.AttributeUsage(global::System.AttributeTargets.Class)]
                                      public class {{AttributeName}} : System.Attribute
                                      {
                                      }
                                  }
                                  """;
}