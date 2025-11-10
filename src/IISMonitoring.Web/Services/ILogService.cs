using IISMonitoring.Web.Models;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Interfaz para el servicio de gestión de logs
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Obtiene la lista de archivos de log disponibles
    /// </summary>
    Task<LogFilesResponse> GetLogFilesAsync();

    /// <summary>
    /// Lee el contenido de un archivo de log
    /// </summary>
    /// <param name="fileName">Nombre del archivo de log</param>
    /// <param name="skip">Número de líneas a saltar (paginación)</param>
    /// <param name="take">Número de líneas a tomar (paginación)</param>
    /// <param name="level">Filtrar por nivel de log (opcional)</param>
    /// <param name="search">Buscar texto en el mensaje (opcional)</param>
    Task<LogContentResponse> GetLogContentAsync(string fileName, int skip = 0, int take = 100, string? level = null, string? search = null);
}
