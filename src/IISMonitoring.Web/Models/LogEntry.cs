namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa una entrada de log parseada de Serilog
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Timestamp del log
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Nivel de log (Information, Warning, Error, Fatal, Debug, Verbose)
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje del log
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Excepción si existe
    /// </summary>
    public string? Exception { get; set; }

    /// <summary>
    /// Línea original del log
    /// </summary>
    public string RawLine { get; set; } = string.Empty;
}
