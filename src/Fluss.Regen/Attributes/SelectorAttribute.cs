namespace Fluss.Regen.Attributes;

public sealed class SelectorAttribute : IRegenAttribute
{
    private const string Namespace = "Fluss.Regen";
    private const string AttributeName = "SelectorAttribute";

    public static string FullName => $"{Namespace}.{AttributeName}";

    public string FileName => $"{AttributeName}.g.cs";
    public string SourceCode => $$"""
                                  namespace {{Namespace}}
                                  {
                                      [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
                                      public class {{AttributeName}} : System.Attribute
                                      {
                                      }
                                  }
                                  """;
}