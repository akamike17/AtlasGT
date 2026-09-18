using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;

namespace AtlasGT.Connectors.Mqtt
{
    /// <summary>
    /// Exportador: observa el buffer store-and-forward y publica a MQTT.
    /// Cierra el ciclo: observer → buffer → forward (con reintentos).
    /// </summary>
    public sealed class MqttForwarder
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        private readonly IStoreAndForwardBuffer _buffer;
        private readonly InMemoryMqttBroker _broker;
        private readonly string _topicPrefix;

        public MqttForwarder(IStoreAndForwardBuffer buffer, InMemoryMqttBroker broker, string topicPrefix = "atlasgt")
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _topicPrefix = topicPrefix;
        }

        /// <summary>
        /// Publica hasta <paramref name="maxBatch"/> observaciones pendientes.
        /// Devuelve cuantas se entregaron.
        /// </summary>
        public async Task<int> FlushAsync(int maxBatch = 50, CancellationToken ct = default)
        {
            return await _buffer.DrainAsync(async (obs, token) =>
            {
                var topic = $"{_topicPrefix}/observations";
                var json = JsonSerializer.Serialize(obs, Json);
                try
                {
                    await _broker.PublishAsync(topic, Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { return false; } // broker caido: reintentar luego
            }, maxBatch, ct).ConfigureAwait(false);
        }
    }
}
