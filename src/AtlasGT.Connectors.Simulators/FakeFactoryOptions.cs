using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Simulators
{
    /// <summary>
    /// Configuracion de la fabrica simulada. Cada campo representa
    /// un comportamiento deseado y documentado.
    /// </summary>
    public class FakeFactoryOptions
    {
        /// <summary>Numero de maquinas a simular.</summary>
        public int MachineCount { get; set; } = 3;

        /// <summary>Intervalo entre envios normales.</summary>
        public TimeSpan SendInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Puerto base (incremental por maquina).</summary>
        public int BasePort { get; set; } = 5000;

        /// <summary>Habilita desconexiones aleatorias.</summary>
        public bool EnableDisconnects { get; set; }

        /// <summary>Probabilidad de desconexion por interval (0..1).</summary>
        public double DisconnectProbability { get; set; } = 0.05;

        /// <summary>Duracion de una desconexion.</summary>
        public TimeSpan DisconnectDuration { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>Habilita datos malformados ocasionales.</summary>
        public bool EnableMalformedData { get; set; }

        /// <summary>Probabilidad de dato malformado.</summary>
        public double MalformedDataProbability { get; set; } = 0.05;

        /// <summary>Habilita rollover de contadores.</summary>
        public bool EnableCounterRollover { get; set; }

        /// <summary>Valor maximo antes de rollover.</summary>
        public int CounterRolloverAt { get; set; } = 100;

        /// <summary>Latencia artificial entre mensaje y envio real (ms).</summary>
        public int ArtificialLatencyMs { get; set; }
    }
}
