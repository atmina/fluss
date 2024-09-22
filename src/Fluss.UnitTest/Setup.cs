using System.Runtime.CompilerServices;
using DiffEngine;

namespace Fluss.UnitTest;

public static class Setup
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
        UseSourceFileRelativeDirectory("Snapshots");
        VerifierSettings.UniqueForRuntimeAndVersion();
        DiffRunner.Disabled = true;
    }
}