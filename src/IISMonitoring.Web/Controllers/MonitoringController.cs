using IISMonitoring.Web.Models;
using IISMonitoring.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IISMonitoring.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IIISMonitoringService _monitoringService;
    private readonly HistoricalDataService _historicalDataService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IIISMonitoringService monitoringService,
        HistoricalDataService historicalDataService,
        ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
        _historicalDataService = historicalDataService;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var dashboard = await _monitoringService.GetDashboardDataAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener datos del dashboard");
            return StatusCode(500, new { error = "Error al obtener datos del dashboard", message = ex.Message });
        }
    }

    [HttpGet("applicationpools")]
    public async Task<IActionResult> GetApplicationPools()
    {
        try
        {
            var appPools = await _monitoringService.GetApplicationPoolsAsync();
            return Ok(appPools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener Application Pools");
            return StatusCode(500, new { error = "Error al obtener Application Pools", message = ex.Message });
        }
    }

    [HttpGet("websites")]
    public async Task<IActionResult> GetWebSites()
    {
        try
        {
            var websites = await _monitoringService.GetWebSitesAsync();
            return Ok(websites);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sitios web");
            return StatusCode(500, new { error = "Error al obtener sitios web", message = ex.Message });
        }
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        try
        {
            var metrics = await _monitoringService.GetPerformanceMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener métricas de rendimiento");
            return StatusCode(500, new { error = "Error al obtener métricas de rendimiento", message = ex.Message });
        }
    }

    [HttpPost("applicationpools/{poolName}/start")]
    public async Task<IActionResult> StartApplicationPool(string poolName)
    {
        try
        {
            var result = await _monitoringService.StartApplicationPoolAsync(poolName);
            if (result)
            {
                return Ok(new { success = true, message = $"Application Pool '{poolName}' iniciado correctamente" });
            }
            return BadRequest(new { success = false, message = $"No se pudo iniciar el Application Pool '{poolName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al iniciar Application Pool '{poolName}'");
            return StatusCode(500, new { error = "Error al iniciar Application Pool", message = ex.Message });
        }
    }

    [HttpPost("applicationpools/{poolName}/stop")]
    public async Task<IActionResult> StopApplicationPool(string poolName)
    {
        try
        {
            var result = await _monitoringService.StopApplicationPoolAsync(poolName);
            if (result)
            {
                return Ok(new { success = true, message = $"Application Pool '{poolName}' detenido correctamente" });
            }
            return BadRequest(new { success = false, message = $"No se pudo detener el Application Pool '{poolName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al detener Application Pool '{poolName}'");
            return StatusCode(500, new { error = "Error al detener Application Pool", message = ex.Message });
        }
    }

    [HttpPost("websites/{siteName}/start")]
    public async Task<IActionResult> StartWebSite(string siteName)
    {
        try
        {
            var result = await _monitoringService.StartWebSiteAsync(siteName);
            if (result)
            {
                return Ok(new { success = true, message = $"Sitio Web '{siteName}' iniciado correctamente" });
            }
            return BadRequest(new { success = false, message = $"No se pudo iniciar el Sitio Web '{siteName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al iniciar Sitio Web '{siteName}'");
            return StatusCode(500, new { error = "Error al iniciar Sitio Web", message = ex.Message });
        }
    }

    [HttpPost("websites/{siteName}/stop")]
    public async Task<IActionResult> StopWebSite(string siteName)
    {
        try
        {
            var result = await _monitoringService.StopWebSiteAsync(siteName);
            if (result)
            {
                return Ok(new { success = true, message = $"Sitio Web '{siteName}' detenido correctamente" });
            }
            return BadRequest(new { success = false, message = $"No se pudo detener el Sitio Web '{siteName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al detener Sitio Web '{siteName}'");
            return StatusCode(500, new { error = "Error al detener Sitio Web", message = ex.Message });
        }
    }

    [HttpGet("historical")]
    public async Task<IActionResult> GetHistoricalData()
    {
        try
        {
            // Obtener datos históricos
            var historicalData = await _historicalDataService.GetHistoricalDataAsync();

            // Obtener sitios web para filtrar pools que tienen sitios asociados
            var webSites = await _monitoringService.GetWebSitesAsync();

            // Extraer los nombres de los application pools que tienen sitios web
            var poolsWithWebSites = webSites
                .Where(site => !string.IsNullOrEmpty(site.ApplicationPool) && site.ApplicationPool != "N/A")
                .Select(site => site.ApplicationPool)
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Filtrar datos históricos para incluir solo pools que tienen sitios web
            var filteredPoolData = historicalData.PoolData
                .Where(kvp => poolsWithWebSites.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var filteredAvailablePools = historicalData.AvailablePools
                .Where(poolName => poolsWithWebSites.Contains(poolName))
                .ToList();

            // Crear respuesta filtrada
            var filteredResponse = new HistoricalDataResponse
            {
                PoolData = filteredPoolData,
                AvailablePools = filteredAvailablePools,
                RetentionMinutes = historicalData.RetentionMinutes
            };

            return Ok(filteredResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener datos históricos");
            return StatusCode(500, new { error = "Error al obtener datos históricos", message = ex.Message });
        }
    }
}
