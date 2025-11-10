namespace IISMonitoring.Web.Models;

/// <summary>
/// Información sobre un archivo de log
/// </summary>
public class LogFileInfo
{
    /// <summary>
    /// Nombre del archivo
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta completa del archivo
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo en bytes
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Tamaño del archivo formateado (KB, MB, etc.)
    /// </summary>
    public string SizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de última modificación
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Directorio donde se encuentra el archivo
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Número de líneas en el archivo (aproximado)
    /// </summary>
    public int LineCount { get; set; }
}
