﻿//HintName: Registration.g.cs
// <auto-generated/>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DependencyInjection {
    public static partial class SelectorGeneratorTestsESServiceCollectionExtensions {
        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddSelectorGeneratorTestsESPolicies(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection sc) {
            global::Fluss.Authentication.ServiceCollectionExtensions.AddPolicy<global::TestNamespace.TestPolicy>(sc);
            return sc;
        }

        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddSelectorGeneratorTestsESComponents(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection sc) {
            AddSelectorGeneratorTestsESPolicies(sc);
            return sc;
        }
    }
}
