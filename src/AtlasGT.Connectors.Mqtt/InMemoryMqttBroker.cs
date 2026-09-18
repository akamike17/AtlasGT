using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Mqtt
{
    /// <summary>
    /// MQTT broker embebido minimo: pub/sub en memoria.
    /// No es un broker completo; solo para pruebas y desarrollo local.
    /// </summary>
    public sealed class InMemoryMqttBroker
    {
        private readonly Dictionary<string, List<Func<string, byte[], Task>>> _subs = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async Task PublishAsync(string topic, byte[] payload)
        {
            List<Func<string, byte[], Task>> handlers;
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                handlers = _subs.TryGetValue(topic, out var h) ? new List<Func<string, byte[], Task>>(h) : new();
            }
            finally { _gate.Release(); }

            foreach (var handler in handlers)
            {
                try { await handler(topic, payload).ConfigureAwait(false); }
                catch (OperationCanceledException) { /* subscriber cancelado */ }
            }
        }

        public async Task SubscribeAsync(string topic, Func<string, byte[], Task> handler)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_subs.TryGetValue(topic, out var list))
                {
                    list = new List<Func<string, byte[], Task>>();
                    _subs[topic] = list;
                }
                list.Add(handler);
            }
            finally { _gate.Release(); }
        }

        public async Task UnsubscribeAsync(string topic, Func<string, byte[], Task> handler)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_subs.TryGetValue(topic, out var list))
                    list.Remove(handler);
            }
            finally { _gate.Release(); }
        }

        public async Task<int> GetSubscriberCountAsync(string topic)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { return _subs.TryGetValue(topic, out var l) ? l.Count : 0; }
            finally { _gate.Release(); }
        }
    }
}
