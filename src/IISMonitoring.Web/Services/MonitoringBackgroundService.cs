using IISMonitoring.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IISMonitoring.Web.Services;

public class MonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<MonitoringHub> _hubContext;
    private readonly ILogger<MonitoringBackgroundService> _logger;

    public MonitoringBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<MonitoringHub> hubContext,
        ILogger<MonitoringBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de monitorización en segundo plano iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IIISMonitoringService>();

                var dashboardData = await monitoringService.GetDashboardDataAsync();

                await _hubContext.Clients.All.SendAsync("UpdateDashboard", dashboardData, stoppingToken);

                _logger.LogDebug("Datos del dashboard actualizados y enviados a los clientes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar datos del dashboard");
            }

            // Actualizar cada 5 segundos
            await Task.Delay(5000, stoppingToken);
        }

        _logger.LogInformation("Servicio de monitorización en segundo plano detenido");
    }
}
