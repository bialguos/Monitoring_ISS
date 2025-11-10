namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa un punto de datos histórico para un Application Pool
/// </summary>
public class HistoricalDataPoint
{
    /// <summary>
    /// Timestamp del dato
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Nombre del Application Pool
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Uso de CPU en porcentaje
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Uso de memoria en MB
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// Estado del pool
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Número de requests activos
    /// </summary>
    public int ActiveRequests { get; set; }
}

/// <summary>
/// Respuesta con datos históricos agrupados por pool
/// </summary>
public class HistoricalDataResponse
{
    /// <summary>
    /// Datos históricos agrupados por nombre de pool
    /// </summary>
    public Dictionary<string, List<HistoricalDataPoint>> PoolData { get; set; } = new();

    /// <summary>
    /// Lista de todos los nombres de pools disponibles
    /// </summary>
    public List<string> AvailablePools { get; set; } = new();

    /// <summary>
    /// Tiempo de retención configurado (en minutos)
    /// </summary>
    public int RetentionMinutes { get; set; }
}
