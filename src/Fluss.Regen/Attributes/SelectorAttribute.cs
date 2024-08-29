﻿namespace Fluss.Regen.Attributes;

public abstract class SelectorAttribute
{
    public static string FullName => $"{Namespace}.{AttributeName}";

    private const string Namespace = "Fluss.Regen";
    private const string AttributeName = "SelectorAttribute";

    public const string AttributeSourceCode = $@"// <auto-generated/>

namespace {Namespace}
{{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class {AttributeName} : System.Attribute
    {{
    }}
}}";
}