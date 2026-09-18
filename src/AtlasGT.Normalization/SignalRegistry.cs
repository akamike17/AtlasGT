using System;
using System.Collections.Generic;
using System.Linq;
using AtlasGT.Domain.Models;

namespace AtlasGT.Normalization
{
    /// <summary>
    /// Almacen de Signals por Asset con timestamp y quality.
    /// </summary>
    public interface ISignalRegistry
    {
        void Set(Guid assetId, Signal signal);
        Signal? Get(Guid assetId, string key);
        IReadOnlyList<Signal> GetAll(Guid assetId);
    }

    public sealed class InMemorySignalRegistry : ISignalRegistry
    {
        private readonly Dictionary<(Guid AssetId, string Key), Signal> _data = new();
        private readonly object _lock = new();

        public void Set(Guid assetId, Signal signal)
        {
            if (signal is null) throw new ArgumentNullException(nameof(signal));
            lock (_lock)
            {
                _data[(assetId, signal.Key)] = signal;
            }
        }

        public Signal? Get(Guid assetId, string key)
        {
            lock (_lock)
            {
                return _data.TryGetValue((assetId, key), out var s) ? s : null;
            }
        }

        public IReadOnlyList<Signal> GetAll(Guid assetId)
        {
            lock (_lock)
            {
                return _data.Where(kvp => kvp.Key.AssetId == assetId)
                            .Select(kvp => kvp.Value)
                            .ToList();
            }
        }
    }
}
