using System.Runtime.CompilerServices;

namespace Fluss.UnitTest;

public static class Setup
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
        Verifier.UseSourceFileRelativeDirectory("Snapshots");
    }
}