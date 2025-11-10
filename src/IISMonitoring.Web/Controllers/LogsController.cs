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
    private readonly IIISMonitoringService _iisMonitoringService;

    public LogsController(ILogService logService, ILogger<LogsController> logger, IIISMonitoringService iisMonitoringService)
    {
        _logService = logService;
        _logger = logger;
        _iisMonitoringService = iisMonitoringService;
    }

    /// <summary>
    /// Obtiene la lista de sitios IIS disponibles
    /// </summary>
    /// <returns>Lista de nombres de sitios IIS</returns>
    [HttpGet("iis-sites")]
    public async Task<ActionResult<IEnumerable<string>>> GetIISSites()
    {
        try
        {
            var sites = await _iisMonitoringService.GetWebSitesAsync();
            var siteNames = sites.Select(s => s.Name).OrderBy(n => n).ToList();
            return Ok(siteNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de sitios IIS");
            return StatusCode(500, new { error = "Error al obtener la lista de sitios IIS" });
        }
    }

    /// <summary>
    /// Obtiene la lista de archivos de log disponibles
    /// </summary>
    /// <param name="iisSite">Filtrar logs por sitio IIS específico (opcional)</param>
    /// <returns>Lista de archivos de log y directorios</returns>
    [HttpGet("files")]
    public async Task<ActionResult<LogFilesResponse>> GetLogFiles([FromQuery] string? iisSite = null)
    {
        try
        {
            var response = await _logService.GetLogFilesAsync();

            // Filtrar por sitio IIS si se especifica
            if (!string.IsNullOrWhiteSpace(iisSite))
            {
                response.LogFiles = response.LogFiles
                    .Where(f => f.IISSiteName != null && f.IISSiteName.Equals(iisSite, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                response.TotalFiles = response.LogFiles.Count;
                response.TotalSizeBytes = response.LogFiles.Sum(f => f.SizeBytes);
            }

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
    /// <param name="fileId">Identificador del archivo (ruta completa codificada en Base64)</param>
    /// <param name="skip">Número de registros a saltar (paginación)</param>
    /// <param name="take">Número de registros a obtener (paginación)</param>
    /// <param name="level">Filtrar por nivel de log (Information, Warning, Error, Fatal, Debug, Verbose)</param>
    /// <param name="search">Buscar texto en el mensaje o excepción</param>
    /// <returns>Contenido del archivo de log parseado</returns>
    [HttpGet("content/{fileId}")]
    public async Task<ActionResult<LogContentResponse>> GetLogContent(
        string fileId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] string? level = null,
        [FromQuery] string? search = null)
    {
        try
        {
            // Validar parámetros
            if (string.IsNullOrWhiteSpace(fileId))
            {
                return BadRequest(new { error = "El identificador del archivo es requerido" });
            }

            if (skip < 0)
            {
                return BadRequest(new { error = "El parámetro 'skip' no puede ser negativo" });
            }

            if (take <= 0 || take > 1000)
            {
                return BadRequest(new { error = "El parámetro 'take' debe estar entre 1 y 1000" });
            }

            var response = await _logService.GetLogContentAsync(fileId, skip, take, level, search);

            if (response.Entries.Count == 0 && response.TotalLines == 0)
            {
                return NotFound(new { error = "Archivo de log no encontrado" });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el contenido del archivo de log");
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
