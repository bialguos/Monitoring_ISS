namespace IISMonitoring.Web.Models;

/// <summary>
/// Dashboard de APM con métricas y traces recientes
/// </summary>
public class ApmDashboard
{
    /// <summary>
    /// Lista de traces recientes
    /// </summary>
    public List<TraceInfo> RecentTraces { get; set; } = new();

    /// <summary>
    /// Estadísticas generales de APM
    /// </summary>
    public ApmStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Timestamp de la captura
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Estadísticas de APM
/// </summary>
public class ApmStatistics
{
    /// <summary>
    /// Total de traces capturados
    /// </summary>
    public int TotalTraces { get; set; }

    /// <summary>
    /// Número de traces con errores
    /// </summary>
    public int ErrorTraces { get; set; }

    /// <summary>
    /// Duración promedio de traces en milisegundos
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Duración del trace más lento en milisegundos
    /// </summary>
    public double MaxDurationMs { get; set; }

    /// <summary>
    /// Duración del trace más rápido en milisegundos
    /// </summary>
    public double MinDurationMs { get; set; }

    /// <summary>
    /// Tasa de error (porcentaje)
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Operaciones más lentas
    /// </summary>
    public List<OperationStatistic> SlowestOperations { get; set; } = new();
}

/// <summary>
/// Estadística de una operación
/// </summary>
public class OperationStatistic
{
    /// <summary>
    /// Nombre de la operación
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Duración promedio en milisegundos
    /// </summary>
    public double AverageDurationMs { get; set; }

    /// <summary>
    /// Número de ejecuciones
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Número de errores
    /// </summary>
    public int ErrorCount { get; set; }
}
