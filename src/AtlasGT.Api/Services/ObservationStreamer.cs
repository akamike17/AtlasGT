using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Application;
using AtlasGT.Application.Alarms;
using AtlasGT.Api.Hubs;
using AtlasGT.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace AtlasGT.Api.Services
{
    /// <summary>
    /// Publica observations a un Hub SignalR en tiempo real.
    /// </summary>
    public sealed class ObservationStreamer
    {
        private readonly IHubContext<ObservationsHub>? _hub;
        private readonly AlarmEngine? _alarms;

        public ObservationStreamer(IHubContext<ObservationsHub>? hub, AlarmEngine? alarms = null)
        {
            _hub = hub;
            _alarms = alarms;
        }

        public async Task PublishAsync(Observation obs, CancellationToken ct = default)
        {
            if (obs is null) throw new ArgumentNullException(nameof(obs));

            // Procesar alarmas
            var raised = _alarms?.Process(obs) ?? new System.Collections.Generic.List<AlarmInstance>();

            if (_hub is null) return;
            var assetId = obs.EndpointId?.ToString() ?? "unknown";
            await _hub.Clients.Group($"asset-{assetId}")
                .SendAsync("OnObservation", obs, ct).ConfigureAwait(false);

            foreach (var a in raised)
                await _hub.Clients.All.SendAsync("OnAlarm", a, ct).ConfigureAwait(false);
        }
    }
}
