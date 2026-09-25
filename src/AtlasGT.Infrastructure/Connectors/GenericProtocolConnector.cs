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
        private ConnectorState _state = ConnectorState.Disconnected;

        public string EndpointAddress => _address;
        public string TransportKind => _schema.Transport.ToString().ToLower();
        public ConnectorState State => _state;

        public GenericProtocolConnector(string address, ProtocolSchema schema, ILogger<GenericProtocolConnector> logger)
        {
            _address = address;
            _schema = schema;
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Connecting;
            _logger.LogInformation($"Connecting to {_address} using schema {_schema.ProtocolName}...");
            
            // In a real implementation, this would initialize the actual TCP/Serial socket
            // For this architectural implementation, we simulate the connection
            await Task.Delay(100, cancellationToken);
            
            _state = ConnectorState.Connected;
            return true;
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                // SIMULATION: In reality, this reads from the socket
                byte[] rawBuffer = SimulateIncomingData();
                
                // 1. Framing & Validation (Simplified for this implementation)
                if (!ValidateFrame(rawBuffer))
                {
                    _logger.LogWarning("Invalid frame received. Skipping...");
                    continue;
                }

                // 2. Produce the RawSample for the system
                yield return new RawSample
                {
                    EndpointAddress = _address,
                    ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Payload = rawBuffer,
                    TransportKind = TransportKind
                };

                await Task.Delay(1000, cancellationToken);
            }
        }

        private bool ValidateFrame(byte[] data)
        {
            if (_schema.Framing.Type == FramingType.FixedLength && data.Length != _schema.Framing.FixedLength)
                return false;

            if (_schema.Validation.EnableChecksum)
            {
                // Implementation of CRC/Checksum check would go here
                _logger.LogDebug("Validating checksum...");
            }

            return true;
        }

        private byte[] SimulateIncomingData()
        {
            // Generates data that matches the schema to prove the concept
            byte[] data = new byte[_schema.Framing.FixedLength > 0 ? _schema.Framing.FixedLength : 10];
            new Random().NextBytes(data);
            return data;
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
