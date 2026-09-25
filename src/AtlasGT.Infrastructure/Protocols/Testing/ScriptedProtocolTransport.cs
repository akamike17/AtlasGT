using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Infrastructure.Protocols.Testing
{
    /// <summary>
    /// Deterministic transport implementation for protocol verification.
    /// Allows programming exact sequences of bytes and simulating failures.
    /// </summary>
    public class ScriptedProtocolTransport : IProtocolTransport
    {
        private readonly Queue<ScriptedResponse> _responseQueue = new();
        private readonly List<byte[]> _sentData = new();
        private bool _isConnected;
        private bool _shouldFailNextReceive;
        private int _receiveDelayMs = 0;
        private byte[]? _expectedRequest;

        public bool IsConnected => _isConnected;

        public void AddResponse(byte[] data, int delayMs = 0)
        {
            _responseQueue.Enqueue(new ScriptedResponse { Data = data, DelayMs = delayMs });
        }

        public void SetExpectedRequest(byte[] request) => _expectedRequest = request;

        public void ForceDisconnectOnNextReceive() => _shouldFailNextReceive = true;
        public void SetReceiveDelay(int ms) => _receiveDelayMs = ms;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (!_isConnected) throw new InvalidOperationException("Transport is not connected.");

            byte[] sent = data.ToArray();
            
            if (_expectedRequest != null && !sent.SequenceEqual(_expectedRequest))
            {
                throw new System.IO.IOException($"Transport request mismatch. Expected {BitConverter.ToString(_expectedRequest)}, received {BitConverter.ToString(sent)}");
            }

            _sentData.Add(sent);
            return Task.CompletedTask;
        }

        public async Task<byte[]> ReceiveAsync(int maxLength, CancellationToken cancellationToken = default)
        {
            if (!_isConnected) throw new InvalidOperationException("Transport is not connected.");

            if (_shouldFailNextReceive)
            {
                _shouldFailNextReceive = false;
                throw new System.IO.IOException("Simulated transport failure.");
            }

            if (_receiveDelayMs > 0) await Task.Delay(_receiveDelayMs, cancellationToken);

            if (_responseQueue.Count == 0)
            {
                throw new TimeoutException("No scripted response available in queue.");
            }

            var response = _responseQueue.Dequeue();

            if (response.Data.Length > maxLength)
            {
                return response.Data.Take(maxLength).ToArray();
            }

            return response.Data;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
        }

        public IReadOnlyList<byte[]> GetSentData() => _sentData.AsReadOnly();
        public void ClearSentData() => _sentData.Clear();
        public void ClearResponses() => _responseQueue.Clear();

        private class ScriptedResponse
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public int DelayMs { get; set; }
        }
    }
}
