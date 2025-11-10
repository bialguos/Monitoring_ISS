using IISMonitoring.Web.Models;
using IISMonitoring.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IISMonitoring.Web.Controllers;

/// <summary>
/// Controlador para gestionar y visualizar logs de la aplicación
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogService logService, ILogger<LogsController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene la lista de archivos de log disponibles
    /// </summary>
    /// <returns>Lista de archivos de log y directorios</returns>
    [HttpGet("files")]
    public async Task<ActionResult<LogFilesResponse>> GetLogFiles()
    {
        try
        {
            var response = await _logService.GetLogFilesAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de archivos de log");
            return StatusCode(500, new { error = "Error al obtener la lista de archivos de log" });
        }
    }

    /// <summary>
    /// Obtiene el contenido de un archivo de log específico
    /// </summary>
    /// <param name="fileName">Nombre del archivo de log</param>
    /// <param name="skip">Número de registros a saltar (paginación)</param>
    /// <param name="take">Número de registros a obtener (paginación)</param>
    /// <param name="level">Filtrar por nivel de log (Information, Warning, Error, Fatal, Debug, Verbose)</param>
    /// <param name="search">Buscar texto en el mensaje o excepción</param>
    /// <returns>Contenido del archivo de log parseado</returns>
    [HttpGet("content/{fileName}")]
    public async Task<ActionResult<LogContentResponse>> GetLogContent(
        string fileName,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] string? level = null,
        [FromQuery] string? search = null)
    {
        try
        {
            // Validar parámetros
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest(new { error = "El nombre del archivo es requerido" });
            }

            if (skip < 0)
            {
                return BadRequest(new { error = "El parámetro 'skip' no puede ser negativo" });
            }

            if (take <= 0 || take > 1000)
            {
                return BadRequest(new { error = "El parámetro 'take' debe estar entre 1 y 1000" });
            }

            // Sanitizar el nombre del archivo para prevenir path traversal
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest(new { error = "Nombre de archivo inválido" });
            }

            var response = await _logService.GetLogContentAsync(fileName, skip, take, level, search);

            if (response.Entries.Count == 0 && response.TotalLines == 0)
            {
                return NotFound(new { error = "Archivo de log no encontrado" });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el contenido del archivo de log: {FileName}", fileName);
            return StatusCode(500, new { error = "Error al obtener el contenido del archivo de log" });
        }
    }

    /// <summary>
    /// Endpoint de prueba para generar logs de ejemplo
    /// </summary>
    [HttpPost("generate-test-logs")]
    public ActionResult GenerateTestLogs()
    {
        try
        {
            _logger.LogInformation("Log de prueba: Information - Operación exitosa");
            _logger.LogWarning("Log de prueba: Warning - Advertencia de prueba");
            _logger.LogError("Log de prueba: Error - Error de prueba");
            _logger.LogDebug("Log de prueba: Debug - Información de depuración");

            try
            {
                throw new InvalidOperationException("Excepción de prueba generada intencionalmente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log de prueba con excepción");
            }

            return Ok(new { message = "Logs de prueba generados exitosamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar logs de prueba");
            return StatusCode(500, new { error = "Error al generar logs de prueba" });
        }
    }
}
