using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
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
        private ConnectorState _state = ConnectorState.Disconnected;

        public string EndpointAddress => _address;
        public string TransportKind => _script.Transport.ToString().ToLower();
        public ConnectorState State => _state;

        public ScriptedProtocolConnector(string address, ScriptedProtocol script, ILogger<ScriptedProtocolConnector> logger)
        {
            _address = address;
            _script = script;
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Connecting;
            // In a real implementation, this opens the socket based on _script.Transport
            await Task.Delay(50, cancellationToken);
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
                    // PIPELINE: Transport -> Request/Response Cycle
                    foreach (var step in _script.Steps)
                    {
                        _logger.LogDebug($"Executing step: {step.StepName}");
                        
                        // 1. Transport: Send Request
                        if (step.RequestPayload != null)
                        {
                            await SendBytesAsync(step.RequestPayload, cancellationToken);
                        }

                        // 2. Framing & Integrity: Read response
                        if (step.BlockUntilResponse)
                        {
                            var response = await ReceiveBytesAsync(step.ExpectedResponseLength, step.TimeoutMs, cancellationToken);
                            
                            // If this is the last step, this is our primary payload
                            finalPayload = response;
                        }
                    }

                    if (finalPayload != null)
                    {
                        // 3. Matcher & Decoder: The sample is produced
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
                    _logger.LogError($"Pipeline error in scripted transport: {ex.Message}");
                    _state = ConnectorState.Faulted;
                    yield break;
                }

                await Task.Delay(1000, cancellationToken);
            }
        }

        private async Task SendBytesAsync(byte[] data, CancellationToken ct)
        {
            // Simulating physical write
            await Task.CompletedTask;
        }

        private async Task<byte[]> ReceiveBytesAsync(int length, int timeout, CancellationToken ct)
        {
            // Simulating physical read with timeout
            await Task.Delay(10, ct);
            return new byte[length];
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
