using System;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Network
{
    /// <summary>
    /// Construye <see cref="TcpConnector"/> desde direcciones "tcp://host:port".
    /// </summary>
    public sealed class TcpConnectorFactory : IConnectorFactory
    {
        public string Scheme => "tcp";

        public bool CanHandle(string address)
            => !string.IsNullOrWhiteSpace(address)
               && address.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase);

        public IConnector Create(string address)
        {
            if (!CanHandle(address))
                throw new ArgumentException($"No se puede manejar '{address}' con scheme '{Scheme}'", nameof(address));

            var rest = address.Substring("tcp://".Length);
            var idx = rest.LastIndexOf(':');
            if (idx <= 0 || idx == rest.Length - 1)
                throw new ArgumentException($"Formato esperado tcp://host:port, recibido '{address}'", nameof(address));

            var host = rest.Substring(0, idx);
            var portStr = rest.Substring(idx + 1);
            if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
                throw new ArgumentException($"Puerto invalido en '{address}'", nameof(address));

            return new TcpConnector(host, port);
        }
    }
}
