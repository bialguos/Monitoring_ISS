namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa un trace completo que agrupa múltiples spans relacionados
/// </summary>
public class TraceInfo
{
    /// <summary>
    /// Identificador único del trace
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del servicio que originó el trace
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la operación principal (ej: GET /api/monitoring/dashboard)
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp de inicio del trace
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Duración total del trace en milisegundos
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Estado del trace (Success, Error, Timeout)
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Lista de spans que componen este trace
    /// </summary>
    public List<SpanInfo> Spans { get; set; } = new();

    /// <summary>
    /// Tags adicionales del trace
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Mensaje de error si el trace falló
    /// </summary>
    public string? ErrorMessage { get; set; }
}
