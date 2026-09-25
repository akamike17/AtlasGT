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
        private readonly FrameProcessor _frameProcessor;
        private readonly IntegrityChecker _integrityChecker;
        private readonly ResponseMatcher _matcher;
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
            _frameProcessor = new FrameProcessor();
            _integrityChecker = new IntegrityChecker();
            _matcher = new ResponseMatcher();
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
                            // 1. Transport: Send and receive raw bytes
                            byte[] rawBytes = _transport.SendAndReceive(step.RequestPayload);

                            // 2. Framing: Extract the frame
                            byte[] framedBytes = _frameProcessor.ExtractFrame(rawBytes, _script.ResponseSchema.Framing);

                            // 3. Integrity: Validate Checksum/CRC
                            _integrityChecker.Validate(framedBytes, _script.ResponseSchema.Validation);

                            // 4. Matcher: Verify response pattern (if any)
                            // In a real scenario, the step might define the pattern
                            _matcher.Match(framedBytes, null); 

                            finalPayload = framedBytes;
                        }
                    }

                    if (finalPayload != null)
                    {
                        // Capture sample inside try, but return it outside if necessary.
                        // However, in an async enumerable, we can just move the yield out of the try.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Pipeline failure: {ex.Message}");
                    _state = ConnectorState.Faulted;
                    yield break;
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
