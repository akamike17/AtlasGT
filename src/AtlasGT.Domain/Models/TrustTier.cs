namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Escalera de confianza segun spec seccion 2. Un endpoint desconocido
    /// arranca en <see cref="Passive"/> y solo puede ser promovido
    /// automaticamente hasta <see cref="Observed"/>. Mas arriba requiere
    /// autorizacion explicita.
    /// </summary>
    public enum TrustTier
    {
        /// <summary>Recien descubierto; no se le habla, solo se escucha.</summary>
        Passive = 0,

        /// <summary>Hemos recibido datos crudos del endpoint (Observed automatico).</summary>
        Observed = 1,

        /// <summary>Hay hipotesis sobre el protocolo/semantica (con evidencia).</summary>
        Decoded = 2,

        /// <summary>La hipotesis fue validada.</summary>
        Verified = 3,

        /// <summary>Lectura autorizada.</summary>
        ReadOnly = 4,

        /// <summary>Escritura autorizada. Requiere autorizacion explicita.</summary>
        WriteCapable = 5,

        /// <summary>Control (comandos) autorizado. Requiere autorizacion explicita.</summary>
        ControlCapable = 6
    }
}
