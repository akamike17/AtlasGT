using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Mqtt
{
    /// <summary>
    /// Connector MQTT pasivo que se suscribe a un topic del broker embebido.
    /// </summary>
    public sealed class MqttConnector : IConnector
    {
        private readonly InMemoryMqttBroker _broker;
        private readonly string _topic;
        private readonly string _address;
        private ConnectorState _state = ConnectorState.Disconnected;
        private Func<string, byte[], Task>? _handler;

        public MqttConnector(InMemoryMqttBroker broker, string topic, string address = "mqtt://local")
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _address = address;
        }

        public string EndpointAddress => _address;
        public string TransportKind => "mqtt";
        public ConnectorState State => _state;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _state = ConnectorState.Connected;
            return Task.FromResult(true);
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var queue = new System.Collections.Concurrent.ConcurrentQueue<RawSample>();
            var signal = new SemaphoreSlim(0);

            _handler = (topic, payload) =>
            {
                queue.Enqueue(new RawSample
                {
                    EndpointAddress = _address,
                    ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Payload = payload,
                    TransportKind = "mqtt"
                });
                signal.Release();
                return Task.CompletedTask;
            };

            await _broker.SubscribeAsync(_topic, _handler).ConfigureAwait(false);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                    while (queue.TryDequeue(out var s)) yield return s;
                }
            }
            finally
            {
                if (_handler is not null)
                    await _broker.UnsubscribeAsync(_topic, _handler).ConfigureAwait(false);
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
            _state = ConnectorState.Disposed;
        }
    }

    public sealed class MqttConnectorFactory : IConnectorFactory
    {
        private readonly InMemoryMqttBroker _broker;
        public MqttConnectorFactory(InMemoryMqttBroker broker) { _broker = broker; }

        public string Scheme => "mqtt";
        public bool CanHandle(string address) =>
            !string.IsNullOrWhiteSpace(address) && address.StartsWith("mqtt://", StringComparison.OrdinalIgnoreCase);

        public IConnector Create(string address)
        {
            // mqtt://broker/topic  -> topic = "/topic"
            var idx = address.IndexOf('/', "mqtt://".Length);
            var topic = idx >= 0 ? address.Substring(idx + 1) : "#";
            return new MqttConnector(_broker, topic, address);
        }
    }
}
