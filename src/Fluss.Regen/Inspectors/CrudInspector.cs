using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using Fluss.Regen.Attributes;
using Fluss.Regen.Helpers;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class CrudInspector : ISyntaxInspector
{
    private static readonly DiagnosticDescriptor ClassMustBePartial = new(
        "FLUSS0001",
        "Class must be partial",
        "Class '{0}' must be partial",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor IdPropertyTypeMustBeSupported = new(
        "FLUSS0002",
        "ID property type must be supported",
        "The ID property '{0}' must be of type Guid or a struct that could be a strongly-typed ID",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor DuplicatePropertyName = new(
        "FLUSS0003",
        "Duplicate property name",
        "Duplicate property name '{0}' found in CRUD class '{1}'",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor ReservedPropertyName = new(
        "FLUSS0004",
        "Reserved property name",
        "The property name 'Exists' is reserved for internal use in CRUD classes",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor NamespaceMissing = new(
        "FLUSS0005",
        "Namespace missing",
        "CRUD classes must be defined within a namespace",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        "FLUSS0006",
        "Unsupported property type",
        "Property '{0}' has an unsupported type '{1}' for CRUD operations",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor NamingConflict = new(
        "FLUSS0007",
        "Naming conflict",
        "The name '{0}' conflicts with a generated command or event name",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor InvalidInheritance = new(
        "FLUSS0008",
        "Invalid inheritance",
        "CRUD classes should not inherit from other classes as they will inherit from AggregateRoot",
        "Fluss.Regen",
        DiagnosticSeverity.Error,
        true
    );

    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out SyntaxInfo? syntaxInfo)
    {
        if (context.Node is ClassDeclarationSyntax classSyntax)
        {
            var symbol = ModelExtensions.GetDeclaredSymbol(context.SemanticModel, classSyntax);
            if (symbol is INamedTypeSymbol classSymbol &&
                classSymbol.GetAttributes().Any(i => i.AttributeClass?.ToFullyQualified() == "global::" + CrudAttribute.FullName))
            {
                var crudInfo = new CrudInfo(classSymbol, classSyntax);
                syntaxInfo = crudInfo;

                CheckIfClassIsPartial(crudInfo, classSyntax, classSymbol);
                CheckIdPropertyType(crudInfo, classSymbol);
                CheckPropertyUniqueness(crudInfo, classSymbol);
                CheckReservedPropertyNames(crudInfo, classSymbol);
                CheckNamespace(crudInfo, classSymbol);
                CheckPropertyTypes(crudInfo, classSymbol);
                CheckNamingConflicts(crudInfo, classSymbol);
                CheckInheritance(crudInfo, classSymbol);

                return true;
            }
        }

        syntaxInfo = null;
        return false;
    }

    private void CheckIfClassIsPartial(CrudInfo syntaxInfo, ClassDeclarationSyntax classSyntax, INamedTypeSymbol classSymbol)
    {
        if (!classSyntax.Modifiers.Any(i => i.IsKind(SyntaxKind.PartialKeyword)))
        {
            syntaxInfo.AddDiagnostic(
                Diagnostic.Create(
                    ClassMustBePartial,
                    classSyntax.GetLocation(),
                    classSymbol.Name
                )
            );
        }
    }

    private void CheckIdPropertyType(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        var idProperty = classSymbol.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == "Id");
        if (idProperty != null)
        {
            var idType = idProperty.Type;
            if (idType.ToFullyQualified() != "global::System.Guid" && idType.TypeKind != TypeKind.Struct)
            {
                syntaxInfo.AddDiagnostic(
                    Diagnostic.Create(
                        IdPropertyTypeMustBeSupported,
                        idProperty.Locations.FirstOrDefault(),
                        idProperty.Name
                    )
                );
            }
        }
    }

    private void CheckPropertyUniqueness(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        var propertyNames = new HashSet<string>();
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (!propertyNames.Add(member.Name))
            {
                syntaxInfo.AddDiagnostic(
                    Diagnostic.Create(
                        DuplicatePropertyName,
                        member.Locations.FirstOrDefault(),
                        member.Name,
                        classSymbol.Name
                    )
                );
            }
        }
    }

    private void CheckReservedPropertyNames(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        var existsProperty = classSymbol.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(p => p.Name == "Exists");
        if (existsProperty != null)
        {
            syntaxInfo.AddDiagnostic(
                Diagnostic.Create(
                    ReservedPropertyName,
                    existsProperty.Locations.FirstOrDefault()
                )
            );
        }
    }

    private void CheckNamespace(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            syntaxInfo.AddDiagnostic(
                Diagnostic.Create(
                    NamespaceMissing,
                    classSymbol.Locations.FirstOrDefault()
                )
            );
        }
    }

    private void CheckPropertyTypes(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (!IsSupportedType(property.Type))
            {
                syntaxInfo.AddDiagnostic(
                    Diagnostic.Create(
                        UnsupportedPropertyType,
                        property.Locations.FirstOrDefault(),
                        property.Name,
                        property.Type.ToDisplayString()
                    )
                );
            }
        }
    }

    private bool IsSupportedType(ITypeSymbol type)
    {
        return type.IsValueType || type.SpecialType == SpecialType.System_String;
    }

    private void CheckNamingConflicts(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        var reservedNames = new[]
        {
            $"{classSymbol.Name}Create",
            $"{classSymbol.Name}Change",
            $"{classSymbol.Name}Delete",
            $"{classSymbol.Name}Created",
            $"{classSymbol.Name}Changed",
            $"{classSymbol.Name}Deleted"
        };

        foreach (var member in classSymbol.GetMembers())
        {
            if (reservedNames.Contains(member.Name))
            {
                syntaxInfo.AddDiagnostic(
                    Diagnostic.Create(
                        NamingConflict,
                        member.Locations.FirstOrDefault(),
                        member.Name
                    )
                );
            }
        }
    }

    private void CheckInheritance(CrudInfo syntaxInfo, INamedTypeSymbol classSymbol)
    {
        if (classSymbol.BaseType != null && classSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            syntaxInfo.AddDiagnostic(
                Diagnostic.Create(
                    InvalidInheritance,
                    classSymbol.Locations.FirstOrDefault()
                )
            );
        }
    }
}