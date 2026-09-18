using System;
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
    /// Orquesta un conector pasivo: lee RawSamples, los normaliza a Observations,
    /// las persiste en el Historian y promueve el endpoint a Observed.
    /// </summary>
    public sealed class ObservationService : IAsyncDisposable
    {
        private readonly IConnector _connector;
        private readonly IObservationNormalizer _normalizer;
        private readonly IObservationHistorian _historian;
        private readonly ITrustLadder _ladder;
        private readonly Endpoint _endpoint;

        private CancellationTokenSource? _cts;
        private Task? _loop;
        private int _observationCount;

        public event EventHandler<Observation>? OnObservation;

        public ObservationService(
            IConnector connector,
            IObservationNormalizer normalizer,
            IObservationHistorian historian,
            ITrustLadder ladder,
            Endpoint endpoint)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
            _historian = historian ?? throw new ArgumentNullException(nameof(historian));
            _ladder = ladder ?? throw new ArgumentNullException(nameof(ladder));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        public int ObservationCount => _observationCount;

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_cts is not null) return true; // ya iniciado
            var connected = await _connector.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!connected) return false;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loop = Task.Run(() => ReadLoopAsync(_cts.Token), CancellationToken.None);
            return true;
        }

        public async Task StopAsync()
        {
            if (_cts is null) return;
            _cts.Cancel();
            try { if (_loop is not null) await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* esperado */ }
            await _connector.DisconnectAsync().ConfigureAwait(false);
            _cts.Dispose();
            _cts = null;
            _loop = null;
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                await foreach (var sample in _connector.ReadAllAsync(token).ConfigureAwait(false))
                {
                    _ladder.MarkObserved(_endpoint);
                    var obs = _normalizer.Normalize(sample, _endpoint.Id);
                    Interlocked.Increment(ref _observationCount);
                    await _historian.AppendAsync(obs, token).ConfigureAwait(false);
                    OnObservation?.Invoke(this, obs);
                }
            }
            catch (OperationCanceledException) { /* normal */ }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }
    }
}
