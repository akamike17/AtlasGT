using System;
using System.Collections.Generic;
using System.Linq;

namespace AtlasGT.Domain.Models
{
    /// <summary>Severidad de alarma (spec sec. 20).</summary>
    public enum AlarmSeverity
    {
        Info = 0,
        Warning = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>Estado interno de una alarma.</summary>
    public enum AlarmState
    {
        Normal = 0,
        Active = 1,
        Acknowledged = 2,
        Cleared = 3,
        Suppressed = 4
    }
}
