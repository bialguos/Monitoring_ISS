namespace IISMonitoring.Web.Models;

/// <summary>
/// Respuesta con la lista de archivos de log disponibles
/// </summary>
public class LogFilesResponse
{
    /// <summary>
    /// Lista de archivos de log encontrados
    /// </summary>
    public List<LogFileInfo> LogFiles { get; set; } = new();

    /// <summary>
    /// Lista de directorios donde se encontraron logs
    /// </summary>
    public List<string> LogDirectories { get; set; } = new();

    /// <summary>
    /// Total de archivos encontrados
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Tamaño total de todos los archivos en bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }
}

/// <summary>
/// Respuesta con el contenido de un archivo de log
/// </summary>
public class LogContentResponse
{
    /// <summary>
    /// Nombre del archivo
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Entradas de log parseadas
    /// </summary>
    public List<LogEntry> Entries { get; set; } = new();

    /// <summary>
    /// Total de líneas en el archivo
    /// </summary>
    public int TotalLines { get; set; }

    /// <summary>
    /// Número de líneas devueltas
    /// </summary>
    public int ReturnedLines { get; set; }

    /// <summary>
    /// Indica si hay más líneas disponibles
    /// </summary>
    public bool HasMore { get; set; }
}
