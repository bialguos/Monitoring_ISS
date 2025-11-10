using IISMonitoring.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace IISMonitoring.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IIISMonitoringService _monitoringService;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(IIISMonitoringService monitoringService, ILogger<MonitoringController> logger)
    {
        _monitoringService = monitoringService;
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
}
