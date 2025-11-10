using IISMonitoring.Web.Configuration;
using IISMonitoring.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace IISMonitoring.Web.Services;

public class MonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<MonitoringHub> _hubContext;
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly MonitoringOptions _options;
    private readonly HistoricalDataService _historicalDataService;

    public MonitoringBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<MonitoringHub> hubContext,
        ILogger<MonitoringBackgroundService> logger,
        IOptions<MonitoringOptions> options,
        HistoricalDataService historicalDataService)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
        _options = options.Value;
        _historicalDataService = historicalDataService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio de monitorización en segundo plano iniciado (Intervalo: {IntervalMs}ms, Caché: {CacheEnabled})",
            _options.UpdateIntervalMs,
            _options.EnableCounterCaching);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IIISMonitoringService>();

                var dashboardData = await monitoringService.GetDashboardDataAsync();

                // Guardar datos históricos
                if (dashboardData?.ApplicationPools != null)
                {
                    await _historicalDataService.AddDataPointsAsync(dashboardData.ApplicationPools);
                }

                await _hubContext.Clients.All.SendAsync("UpdateDashboard", dashboardData, stoppingToken);

                _logger.LogDebug("Datos del dashboard actualizados y enviados a los clientes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar datos del dashboard");
            }

            // Usar intervalo configurable
            await Task.Delay(_options.UpdateIntervalMs, stoppingToken);
        }

        _logger.LogInformation("Servicio de monitorización en segundo plano detenido");
    }
}
