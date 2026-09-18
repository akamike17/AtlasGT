using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Network
{
    /// <summary>
    /// HTTP polling connector. Lee periodicamente un endpoint GET.
    /// Pasivo: solo GET.
    /// </summary>
    public sealed class HttpConnector : IConnector
    {
        private readonly Uri _uri;
        private readonly TimeSpan _pollInterval;
        private readonly HttpClient _http;
        private ConnectorState _state = ConnectorState.Disconnected;

        public HttpConnector(Uri uri, TimeSpan? pollInterval = null, HttpMessageHandler? handler = null)
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
            _http = handler is null ? new HttpClient() : new HttpClient(handler);
        }

        public string EndpointAddress => _uri.ToString();
        public string TransportKind => "http";
        public ConnectorState State => _state;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            // HTTP no mantiene conexion persistente; "connected" significa "listo".
            _state = ConnectorState.Connected;
            return Task.FromResult(true);
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                HttpResponseMessage? response = null;
                bool faulted = false;
                RawSample? sample = null;
                try
                {
                    response = await _http.GetAsync(_uri, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                        sample = new RawSample
                        {
                            EndpointAddress = EndpointAddress,
                            ReceivedAtUtc = DateTimeOffset.UtcNow,
                            Payload = bytes,
                            TransportKind = "http"
                        };
                    }
                }
                catch (OperationCanceledException) { response?.Dispose(); yield break; }
                catch (HttpRequestException) { _state = ConnectorState.Faulted; faulted = true; }
                finally
                {
                    response?.Dispose();
                }

                if (faulted) yield break;
                if (sample is not null) yield return sample;

                try { await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Disconnected;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _http.Dispose();
            _state = ConnectorState.Disposed;
        }
    }

    public sealed class HttpConnectorFactory : IConnectorFactory
    {
        public string Scheme => "http";

        public bool CanHandle(string address) =>
            !string.IsNullOrWhiteSpace(address)
            && (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        public IConnector Create(string address)
        {
            if (!CanHandle(address)) throw new ArgumentException($"No manejado: '{address}'");
            return new HttpConnector(new Uri(address));
        }
    }
}
