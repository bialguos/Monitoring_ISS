using IISMonitoring.Web.Models;
using IISMonitoring.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IISMonitoring.Web.Controllers;

/// <summary>
/// Controller para Application Performance Monitoring (APM)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApmController : ControllerBase
{
    private readonly IApmService _apmService;
    private readonly ILogger<ApmController> _logger;
    private readonly IISLogParserService _iisLogParser;
    private readonly ApmConfigurationService _apmConfig;

    public ApmController(
        IApmService apmService,
        ILogger<ApmController> logger,
        IISLogParserService iisLogParser,
        ApmConfigurationService apmConfig)
    {
        _apmService = apmService;
        _logger = logger;
        _iisLogParser = iisLogParser;
        _apmConfig = apmConfig;
    }

    /// <summary>
    /// Obtiene el dashboard completo de APM con traces recientes y estadísticas
    /// </summary>
    [HttpGet("dashboard")]
    public IActionResult GetDashboard()
    {
        try
        {
            var dashboard = _apmService.GetDashboard();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener dashboard de APM");
            return StatusCode(500, new { error = "Error al obtener dashboard de APM", message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene todos los traces capturados con filtros opcionales
    /// </summary>
    /// <param name="limit">Límite de traces a devolver (default: 100)</param>
    /// <param name="status">Filtro por estado (Success, Error, null para todos)</param>
    [HttpGet("traces")]
    public IActionResult GetTraces([FromQuery] int limit = 100, [FromQuery] string? status = null)
    {
        try
        {
            var traces = _apmService.GetTraces(limit, status);
            return Ok(traces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener traces");
            return StatusCode(500, new { error = "Error al obtener traces", message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un trace específico por su ID
    /// </summary>
    /// <param name="traceId">ID del trace</param>
    [HttpGet("traces/{traceId}")]
    public IActionResult GetTrace(string traceId)
    {
        try
        {
            var trace = _apmService.GetTrace(traceId);

            if (trace == null)
            {
                return NotFound(new { error = "Trace no encontrado", traceId });
            }

            return Ok(trace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener trace {TraceId}", traceId);
            return StatusCode(500, new { error = "Error al obtener trace", message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de APM
    /// </summary>
    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        try
        {
            var statistics = _apmService.GetStatistics();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estadísticas de APM");
            return StatusCode(500, new { error = "Error al obtener estadísticas", message = ex.Message });
        }
    }

    /// <summary>
    /// Limpia todos los traces capturados
    /// </summary>
    [HttpPost("clear")]
    public IActionResult ClearTraces()
    {
        try
        {
            _apmService.ClearTraces();
            return Ok(new { message = "Traces limpiados correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar traces");
            return StatusCode(500, new { error = "Error al limpiar traces", message = ex.Message });
        }
    }

    /// <summary>
    /// Inicia un trace personalizado manualmente
    /// </summary>
    [HttpPost("traces/start")]
    public IActionResult StartTrace([FromBody] StartTraceRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.OperationName))
            {
                return BadRequest(new { error = "OperationName es requerido" });
            }

            var traceId = _apmService.StartTrace(
                request.OperationName,
                request.ServiceName ?? "IISMonitoring");

            return Ok(new { traceId, message = "Trace iniciado correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar trace");
            return StatusCode(500, new { error = "Error al iniciar trace", message = ex.Message });
        }
    }

    /// <summary>
    /// Finaliza un trace personalizado manualmente
    /// </summary>
    [HttpPost("traces/{traceId}/end")]
    public IActionResult EndTrace(string traceId, [FromBody] EndTraceRequest request)
    {
        try
        {
            _apmService.EndTrace(traceId, request.Status ?? "Success", request.ErrorMessage);
            return Ok(new { message = "Trace finalizado correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al finalizar trace {TraceId}", traceId);
            return StatusCode(500, new { error = "Error al finalizar trace", message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene la lista de sitios IIS disponibles para monitorizar
    /// </summary>
    [HttpGet("sites")]
    public IActionResult GetAvailableSites()
    {
        try
        {
            var sites = _iisLogParser.GetAvailableSites();
            var monitoredSites = GetMonitoredSitesFromConfig();

            var result = sites.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                isMonitored = monitoredSites.Contains(s.Id)
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sitios IIS");
            return StatusCode(500, new { error = "Error al obtener sitios IIS", message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los sitios IIS configurados para monitorizar
    /// </summary>
    [HttpGet("sites/monitored")]
    public IActionResult GetMonitoredSites()
    {
        try
        {
            var monitoredSites = GetMonitoredSitesFromConfig();
            return Ok(monitoredSites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sitios monitorizados");
            return StatusCode(500, new { error = "Error al obtener sitios monitorizados", message = ex.Message });
        }
    }

    /// <summary>
    /// Configura los sitios IIS a monitorizar (persiste en archivo)
    /// </summary>
    [HttpPost("sites/monitor")]
    public async Task<IActionResult> SetMonitoredSites([FromBody] SetMonitoredSitesRequest request)
    {
        try
        {
            await _apmConfig.SetMonitoredSitesAsync(request.SiteIds);
            return Ok(new { message = "Configuración actualizada correctamente", siteIds = request.SiteIds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al configurar sitios monitorizados");
            return StatusCode(500, new { error = "Error al configurar sitios", message = ex.Message });
        }
    }

    /// <summary>
    /// Helper para obtener los sitios monitorizados de la configuración
    /// </summary>
    private List<string> GetMonitoredSitesFromConfig()
    {
        var monitoredSites = _apmConfig.GetMonitoredSites();

        // Si no hay sitios configurados, monitorizar todos
        if (!monitoredSites.Any())
        {
            var availableSites = _iisLogParser.GetAvailableSites();
            return availableSites.Select(s => s.Id).ToList();
        }

        return monitoredSites;
    }
}

/// <summary>
/// Request para iniciar un trace
/// </summary>
public class StartTraceRequest
{
    public string OperationName { get; set; } = string.Empty;
    public string? ServiceName { get; set; }
}

/// <summary>
/// Request para finalizar un trace
/// </summary>
public class EndTraceRequest
{
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request para configurar sitios monitorizados
/// </summary>
public class SetMonitoredSitesRequest
{
    public List<string> SiteIds { get; set; } = new();
}
