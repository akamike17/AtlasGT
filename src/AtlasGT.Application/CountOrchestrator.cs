using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;
using AtlasGT.Normalization;
using AtlasGT.Security;

namespace AtlasGT.Application
{
    /// <summary>
    /// Orquesta N ObservationServices en paralelo con manejo de fallos.
    /// Si un connector falla, no tumba a los demas.
    /// </summary>
    public sealed class CountOrchestrator : IAsyncDisposable
    {
        private readonly List<ObservationService> _services = new();
        private readonly List<Task> _serviceTasks = new();

        public int TotalObservations
        {
            get
            {
                var total = 0;
                foreach (var svc in _services) total += svc.ObservationCount;
                return total;
            }
        }

        public int ServicesRunning => _services.Count;

        public async Task<int> AttachEndpointsAsync(
            IEnumerable<(Endpoint Endpoint, IConnector Connector)> endpoints,
            IObservationNormalizer normalizer,
            IObservationHistorian historian,
            ITrustLadder ladder,
            CancellationToken ct = default)
        {
            var started = 0;
            foreach (var (ep, connector) in endpoints)
            {
                var svc = new ObservationService(connector, normalizer, historian, ladder, ep);
                var ok = await svc.StartAsync(ct).ConfigureAwait(false);
                if (ok)
                {
                    _services.Add(svc);
                    started++;
                }
            }
            return started;
        }

        public async Task StopAllAsync()
        {
            foreach (var svc in _services)
            {
                try { await svc.StopAsync().ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAllAsync().ConfigureAwait(false);
        }
    }
}
