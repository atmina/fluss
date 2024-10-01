﻿//HintName: Selectors.g.cs
// <auto-generated/>

#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Fluss
{
    public static class UnitOfWorkSelectors
    {
        private static global::Microsoft.Extensions.Caching.Memory.MemoryCache _cache = new (new global::Microsoft.Extensions.Caching.Memory.MemoryCacheOptions { SizeLimit = 1024 });

        public static async global::System.Threading.Tasks.ValueTask<int> SelectAdd(this global::Fluss.IUnitOfWork unitOfWork, 
            int a,
            int b
        )
        {
            var key = (
                "TestNamespace.Test.Add",
                a,
                b
            );

            if (_cache.TryGetValue(key, out var result) && result is CacheEntryValue entryValue && await MatchesEventListenerState(unitOfWork, entryValue)) {
                return (int)entryValue.Value;
            }

            result = TestNamespace.Test.Add(
                a,
                b
            );

            using (var entry = _cache.CreateEntry(key)) {
                entry.Value = new CacheEntryValue(result, recordingUnitOfWork.GetRecordedListeners());
                entry.Size = 1;
            }

            return (int)result;
        }
        private record CacheEntryValue(object Value, global::System.Collections.Generic.IReadOnlyList<global::Fluss.UnitOfWorkRecordingProxy.EventListenerTypeWithKeyAndVersion>? EventListeners);

        private static async global::System.Threading.Tasks.ValueTask<bool> MatchesEventListenerState(global::Fluss.IUnitOfWork unitOfWork, CacheEntryValue value) {
            foreach (var eventListenerData in value.EventListeners ?? global::System.Array.Empty<global::Fluss.UnitOfWorkRecordingProxy.EventListenerTypeWithKeyAndVersion>()) {
                if (!await eventListenerData.IsStillUpToDate(unitOfWork)) {
                    return false;
                }
            }
            return true;
        }
    }
}

