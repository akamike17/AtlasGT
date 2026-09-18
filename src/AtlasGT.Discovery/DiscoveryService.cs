using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Discovery
{
    /// <summary>
    /// Mantiene inventario de endpoints descubiertos.
    /// </summary>
    public interface IDiscoveryInventory
    {
        void Upsert(Endpoint endpoint);
        IReadOnlyList<Endpoint> GetAll();
        Endpoint? FindByAddress(string address);
    }

    public sealed class InMemoryDiscoveryInventory : IDiscoveryInventory
    {
        private readonly Dictionary<string, Endpoint> _byAddress = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public void Upsert(Endpoint endpoint)
        {
            if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
            lock (_lock)
            {
                _byAddress[endpoint.Address] = endpoint;
            }
        }

        public IReadOnlyList<Endpoint> GetAll()
        {
            lock (_lock) { return _byAddress.Values.ToList(); }
        }

        public Endpoint? FindByAddress(string address)
        {
            lock (_lock) { return _byAddress.TryGetValue(address, out var ep) ? ep : null; }
        }
    }

    /// <summary>
    /// Discovery coordinator: ejecuta scanners pasivos y Los mete al inventario.
    /// </summary>
    public sealed class DiscoveryService
    {
        private readonly IEndpointDiscovery _scanner;
        private readonly IDiscoveryInventory _inventory;

        public DiscoveryService(IEndpointDiscovery scanner, IDiscoveryInventory inventory)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public async Task<IReadOnlyList<Endpoint>> DiscoverTcpRangeAsync(
            IPAddress address,
            IEnumerable<int> ports,
            TimeSpan perPortTimeout,
            CancellationToken ct = default)
        {
            var found = await _scanner.ScanTcpAsync(address, ports, perPortTimeout, ct).ConfigureAwait(false);
            foreach (var ep in found)
            {
                // Upsert: si ya existe, preservar TrustTier actual
                var existing = _inventory.FindByAddress(ep.Address);
                if (existing is null)
                {
                    _inventory.Upsert(ep);
                }
                else
                {
                    existing.LastObservedAtUtc = DateTime.UtcNow;
                }
            }
            return found;
        }
    }
}
