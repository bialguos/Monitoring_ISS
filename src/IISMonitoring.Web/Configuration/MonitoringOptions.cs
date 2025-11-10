namespace IISMonitoring.Web.Configuration;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>
    /// Intervalo de actualización en milisegundos (por defecto 5000ms = 5 segundos)
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Habilitar caché de Performance Counters (recomendado para mejor rendimiento)
    /// </summary>
    public bool EnableCounterCaching { get; set; } = true;

    /// <summary>
    /// Tiempo de vida del caché de contadores en minutos (por defecto 30 minutos)
    /// </summary>
    public int CounterCacheLifetimeMinutes { get; set; } = 30;

    /// <summary>
    /// Número máximo de reintentos al crear contadores
    /// </summary>
    public int MaxCounterRetries { get; set; } = 3;
}
