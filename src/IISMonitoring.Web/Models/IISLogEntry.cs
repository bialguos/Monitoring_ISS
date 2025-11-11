namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa una entrada de log de IIS en formato W3C
/// </summary>
public class IISLogEntry
{
    /// <summary>
    /// Fecha del request
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// IP del servidor
    /// </summary>
    public string ServerIp { get; set; } = string.Empty;

    /// <summary>
    /// Método HTTP (GET, POST, etc.)
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// URI solicitado
    /// </summary>
    public string UriStem { get; set; } = string.Empty;

    /// <summary>
    /// Query string
    /// </summary>
    public string UriQuery { get; set; } = string.Empty;

    /// <summary>
    /// Puerto del servidor
    /// </summary>
    public int ServerPort { get; set; }

    /// <summary>
    /// Nombre de usuario (si hay autenticación)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// IP del cliente
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent del cliente
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Referer
    /// </summary>
    public string Referer { get; set; } = string.Empty;

    /// <summary>
    /// Código de estado HTTP
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Sub-status
    /// </summary>
    public int SubStatus { get; set; }

    /// <summary>
    /// Win32 status
    /// </summary>
    public int Win32Status { get; set; }

    /// <summary>
    /// Tiempo tomado en milisegundos
    /// </summary>
    public int TimeTaken { get; set; }

    /// <summary>
    /// Host header
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del sitio IIS
    /// </summary>
    public string SiteName { get; set; } = string.Empty;

    /// <summary>
    /// ID del sitio IIS (de W3SVC{id})
    /// </summary>
    public string SiteId { get; set; } = string.Empty;
}
