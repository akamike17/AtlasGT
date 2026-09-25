using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AtlasGT.Infrastructure.Connectors
{
    public class GenericProtocolConnector : IConnector
    {
        private readonly string _address;
        private readonly ProtocolSchema _schema;
        private readonly ILogger<GenericProtocolConnector> _logger;
        private readonly IProtocolTransport _transport;
        private readonly FrameProcessor _frameProcessor;
        private readonly IntegrityChecker _integrityChecker;
        private ConnectorState _state = ConnectorState.Disconnected;

        public string EndpointAddress => _address;
        public string TransportKind => _schema.Transport.ToString().ToLower();
        public ConnectorState State => _state;

        public GenericProtocolConnector(string address, ProtocolSchema schema, ILogger<GenericProtocolConnector> logger, IProtocolTransport transport)
        {
            _address = address;
            _schema = schema;
            _logger = logger;
            _transport = transport;
            _frameProcessor = new FrameProcessor();
            _integrityChecker = new IntegrityChecker();
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Connecting;
            _logger.LogInformation($"Connecting to {_address} using transport {_schema.Transport}...");

            try
            {
                await _transport.ConnectAsync(cancellationToken);
                _state = ConnectorState.Connected;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection failed: {ex.Message}");
                _state = ConnectorState.Faulted;
                return false;
            }
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                byte[] rawBuffer = null;

                try
                {
                    // Read raw bytes from transport. 
                    // We use a reasonable default max length or one from the schema if available.
                    int maxLength = 4096; 
                    rawBuffer = await _transport.ReceiveAsync(maxLength, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Transport read failure: {ex.Message}");
                    _state = ConnectorState.Faulted;
                    yield break;
                }

                if (rawBuffer == null || rawBuffer.Length == 0) continue;

                // 1. Framing: Extract the actual frame from the raw stream
                byte[] framedPayload = _frameProcessor.ExtractFrame(rawBuffer, _schema.Framing);

                if (framedPayload == null)
                {
                    _logger.LogWarning("Could not extract valid frame from transport data.");
                    continue;
                }

                // 2. Integrity: Validate Checksum/CRC
                try
                {
                    _integrityChecker.Validate(framedPayload, _schema.Validation);
                }
                catch (IntegrityException ex)
                {
                    _logger.LogWarning($"Frame integrity check failed: {ex.Message}. Skipping...");
                    continue;
                }

                // 3. Produce the RawSample for the system
                yield return new RawSample
                {
                    EndpointAddress = _address,
                    ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Payload = framedPayload,
                    TransportKind = TransportKind
                };

                await Task.Delay(1000, cancellationToken);
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.DisconnectAsync(cancellationToken);
            _state = ConnectorState.Disconnected;
        }

        public async ValueTask DisposeAsync()
        {
            await _transport.DisposeAsync();
            await DisconnectAsync();
        }
    }
}
