using System.Collections.Immutable;
using System.Linq;
using Fluss.Regen.FileBuilders;
using Fluss.Regen.Helpers;
using Fluss.Regen.Inspectors;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Generators;

public class RegistrationGenerator : ISyntaxGenerator
{
    public void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        if (syntaxInfos.Length == 0)
        {
            return;
        }

        var moduleName = (compilation.AssemblyName ?? "Assembly").Split('.').Last() + "ESComponents";

        using var generator = new RegistrationFileBuilder(moduleName, "Microsoft.Extensions.DependencyInjection");

        generator.WriteHeader();
        generator.WriteBeginNamespace();
        generator.WriteBeginClass();

        var foundInfo = false;

        var aggregateValidators = syntaxInfos.OfType<AggregateValidatorInfo>().ToImmutableHashSet();
        var eventValidators = syntaxInfos.OfType<EventValidatorInfo>().ToImmutableHashSet();
        if (aggregateValidators.Any() || eventValidators.Any())
        {
            generator.WriteBeginRegistrationMethod("Validators");

            foreach (var aggregateValidator in aggregateValidators)
            {
                generator.WriteAggregateValidatorRegistration(aggregateValidator.Type.ToFullyQualified());
            }
            foreach (var eventValidator in eventValidators)
            {
                generator.WriteEventValidatorRegistration(eventValidator.Type.ToFullyQualified());
            }

            generator.WriteEndRegistrationMethod();
            foundInfo = true;
        }

        var policies = syntaxInfos.OfType<PolicyInfo>().ToImmutableHashSet();
        if (policies.Any())
        {
            generator.WriteBeginRegistrationMethod("Policies");
            foreach (var policy in policies)
            {
                generator.WritePolicyRegistration(policy.Type.ToFullyQualified());
            }
            generator.WriteEndRegistrationMethod();
            foundInfo = true;
        }

        var sideEffects = syntaxInfos.OfType<SideEffectInfo>().ToImmutableHashSet();
        if (sideEffects.Any())
        {
            generator.WriteBeginRegistrationMethod("SideEffects");
            foreach (var sideEffect in sideEffects)
            {
                generator.WriteSideEffectRegistration(sideEffect.Type.ToFullyQualified());
            }
            generator.WriteEndRegistrationMethod();
            foundInfo = true;
        }

        var upcasters = syntaxInfos.OfType<UpcasterInfo>().ToImmutableHashSet();
        if (upcasters.Any())
        {
            generator.WriteBeginRegistrationMethod("Upcasters");
            foreach (var upcaster in upcasters)
            {
                generator.WriteUpcasterRegistration(upcaster.Type.ToFullyQualified());
            }
            generator.WriteEndRegistrationMethod();
            foundInfo = true;
        }

        generator.WriteBeginRegistrationMethod("Components");
        if (aggregateValidators.Any() || eventValidators.Any())
        {
            generator.WriteComponentRegistration("Validators");
        }

        if (policies.Any())
        {
            generator.WriteComponentRegistration("Policies");
        }

        if (sideEffects.Any())
        {
            generator.WriteComponentRegistration("SideEffects");
        }

        if (upcasters.Any())
        {
            generator.WriteComponentRegistration("Upcasters");
        }

        generator.WriteEndRegistrationMethod(false);
        generator.WriteEndClass();
        generator.WriteEndNamespace();

        if (foundInfo)
        {
            context.AddSource("Registration.g.cs", generator.ToSourceText());
        }
    }
}