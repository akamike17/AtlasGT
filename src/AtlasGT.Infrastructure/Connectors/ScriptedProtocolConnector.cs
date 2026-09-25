using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using AtlasGT.Infrastructure.Protocols.Testing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Infrastructure.Connectors
{
    public class ScriptedProtocolConnector : IConnector
    {
        private readonly string _address;
        private readonly ScriptedProtocol _script;
        private readonly ILogger<ScriptedProtocolConnector> _logger;
        private readonly ScriptedTransport _transport;
        private ConnectorState _state = ConnectorState.Disconnected;

        public string EndpointAddress => _address;
        public string TransportKind => _script.Transport.ToString().ToLower();
        public ConnectorState State => _state;

        public ScriptedProtocolConnector(string address, ScriptedProtocol script, ILogger<ScriptedProtocolConnector> logger, ScriptedTransport transport)
        {
            _address = address;
            _script = script;
            _logger = logger;
            _transport = transport;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Connected;
            return true;
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                byte[] finalPayload = null;

                try
                {
                    foreach (var step in _script.Steps)
                    {
                        if (step.RequestPayload != null)
                        {
                            // Use the deterministic transport
                            finalPayload = _transport.SendAndReceive(step.RequestPayload);
                        }
                    }

                    if (finalPayload != null)
                    {
                        yield return new RawSample
                        {
                            EndpointAddress = _address,
                            ReceivedAtUtc = DateTimeOffset.UtcNow,
                            Payload = finalPayload,
                            TransportKind = TransportKind
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Protocol failure: {ex.Message}");
                    _state = ConnectorState.Faulted;
                    yield break;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Disconnected;
            await Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
        }
    }
}
