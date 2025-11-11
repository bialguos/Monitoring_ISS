namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa una operación individual dentro de un trace
/// </summary>
public class SpanInfo
{
    /// <summary>
    /// Identificador único del span
    /// </summary>
    public string SpanId { get; set; } = string.Empty;

    /// <summary>
    /// Identificador del trace al que pertenece
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Identificador del span padre (null si es el span raíz)
    /// </summary>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Nombre de la operación (ej: HTTP GET, SQL Query, External API Call)
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de span (Http, Database, Internal, External)
    /// </summary>
    public string SpanKind { get; set; } = "Internal";

    /// <summary>
    /// Servicio que ejecutó esta operación
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp de inicio del span
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Duración del span en milisegundos
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Estado del span (Success, Error)
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Tags adicionales del span (ej: http.method, http.url, db.statement)
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Eventos que ocurrieron durante este span
    /// </summary>
    public List<ApmEvent> Events { get; set; } = new();

    /// <summary>
    /// Mensaje de error si el span falló
    /// </summary>
    public string? ErrorMessage { get; set; }
}
