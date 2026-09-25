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
        private readonly ICommandSafetyService _safetyService;
        private readonly FrameProcessor _frameProcessor;
        private readonly IntegrityChecker _integrityChecker;
        private readonly ResponseMatcher _responseMatcher;
        private ConnectorState _state = ConnectorState.Disconnected;

        public string EndpointAddress => _address;
        public string TransportKind => _schema.Transport.ToString().ToLower();
        public ConnectorState State => _state;

        public GenericProtocolConnector(
            string address, 
            ProtocolSchema schema, 
            ILogger<GenericProtocolConnector> logger, 
            IProtocolTransport transport,
            ICommandSafetyService safetyService)
        {
            _address = address;
            _schema = schema;
            _logger = logger;
            _transport = transport;
            _safetyService = safetyService;
            _frameProcessor = new FrameProcessor();
            _integrityChecker = new IntegrityChecker();
            _responseMatcher = new ResponseMatcher();
        }

        public async Task<DecodedResult> ExecuteAsync(ProtocolOperation operation, CancellationToken cancellationToken = default)
        {
            if (_state != ConnectorState.Connected)
                throw new InvalidOperationException("Connector must be connected before executing operations.");

            try
            {
                // 1. Command Safety
                var safetyResult = _safetyService.VerifyCommand(_schema, operation.RequestBytes);
                if (!safetyResult.IsValid)
                {
                    return new DecodedResult { Success = false, Error = $"Safety violation: {safetyResult.Error}" };
                }

                // 2. Send
                await _transport.SendAsync(operation.RequestBytes, cancellationToken);

                // 3. Receive
                // We use the operation's timeout or a default
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(operation.TimeoutMs);

                byte[] rawBuffer = await _transport.ReceiveAsync(4096, cts.Token);

                // 4. Framing
                var framingConfig = operation.Response.FramingOverride ?? _schema.Framing;
                byte[] framedPayload = _frameProcessor.ExtractFrame(rawBuffer, framingConfig);

                // 5. Integrity
                var validationConfig = operation.Response.ValidationOverride ?? _schema.Validation;
                _integrityChecker.Validate(framedPayload, validationConfig);

                // 6. Response Matcher
                _responseMatcher.Match(framedPayload, operation.Response);

                // 7. Protocol Decoder
                var results = new Dictionary<string, object>();
                foreach (var field in _schema.Fields)
                {
                    var value = ProtocolDecoder.Decode(framedPayload, field);
                    results.Add(field.Name, value);
                }

                return new DecodedResult
                {
                    Success = true,
                    Fields = results,
                    RawPayload = framedPayload
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Operation {operation.OperationName} failed: {ex.Message}");
                return new DecodedResult { Success = false, Error = ex.Message };
            }
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

                byte[] framedPayload = _frameProcessor.ExtractFrame(rawBuffer, _schema.Framing);

                if (framedPayload == null)
                {
                    _logger.LogWarning("Could not extract valid frame from transport data.");
                    continue;
                }

                try
                {
                    _integrityChecker.Validate(framedPayload, _schema.Validation);
                }
                catch (IntegrityException ex)
                {
                    _logger.LogWarning($"Frame integrity check failed: {ex.Message}. Skipping...");
                    continue;
                }

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
