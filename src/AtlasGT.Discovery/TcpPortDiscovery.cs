using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Discovery
{
    /// <summary>
    /// Descubrimiento pasivo de endpoints TCP. Un endpoint detectado NO recibe
    /// comandos; solo se marca como "Observed" cuando logramos abrir socket.
    /// (Spec sec. 2: promocion automatica maxima = Observed.)
    /// </summary>
    public interface IEndpointDiscovery
    {
        Task<IReadOnlyList<Endpoint>> ScanTcpAsync(
            IPAddress address,
            IEnumerable<int> ports,
            TimeSpan perPortTimeout,
            CancellationToken cancellationToken = default);
    }

    public sealed class TcpPortDiscovery : IEndpointDiscovery
    {
        /// <summary>Prueba ligera: intenta abrir un socket TCP. No envia ni lee datos.</summary>
        public async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host requerido", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            try
            {
                await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (SocketException) { return false; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        }

        public async Task<IReadOnlyList<Endpoint>> ScanTcpAsync(
            IPAddress address,
            IEnumerable<int> ports,
            TimeSpan perPortTimeout,
            CancellationToken cancellationToken = default)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            if (ports is null) throw new ArgumentNullException(nameof(ports));
            if (perPortTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(perPortTimeout));

            var results = new List<Endpoint>();
            var portList = ports.ToList();

            using var throttler = new SemaphoreSlim(32);
            var tasks = portList.Select(async port =>
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(perPortTimeout);

                    using var client = new TcpClient();
                    await client.ConnectAsync(address, port, cts.Token).ConfigureAwait(false);

                    lock (results)
                    {
                        results.Add(new Endpoint
                        {
                            Id = Guid.NewGuid(),
                            Name = $"tcp://{address}:{port}",
                            Address = $"tcp://{address}:{port}",
                            TrustTier = TrustTier.Passive,
                            LastObservedAtUtc = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (OperationCanceledException) { /* timeout */ }
                catch (SocketException) { /* cerrado o filtrado */ }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return results;
        }
    }
}
