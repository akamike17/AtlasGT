using System;

namespace AtlasGT.Domain.Models
{
    /// <summary>Calidad de una observacion de signal.</summary>
    public enum QualityKind
    {
        Unknown = 0,
        Good = 1,
        Bad = 2,
        Stale = 3,
    }

    /// <summary>Provenance: de donde sale la confianza del dato (sec. 14).</summary>
    public enum ProvenanceKind
    {
        Unknown = 0,
        Documented = 1,      // manual / datasheet
        Verified = 2,        // probado en pruebas
        Experimental = 3,    // hipotesis en pruebas
        Inferred = 4,        // correlacion estadistica
    }

    /// <summary>Capability modes (Read/Write/Control) para cada endpoint.</summary>
    public enum CapabilityMode
    {
        None = 0,
        Read = 1,
        Write = 2,
        Control = 4,
    }

    /// <summary>Estado ON/OFF de la maquina/equipo observado (sec. 22).</summary>
    public enum MachineStateKind
    {
        Unknown = 0,
        Running = 1,
        Stopped = 2,
        Idle = 3,
        Alarmed = 4,
        Offline = 5,
    }
}
