using Microsoft.AspNetCore.SignalR;

namespace IISMonitoring.Web.Hubs;

public class MonitoringHub : Hub
{
    public async Task RequestUpdate()
    {
        await Clients.Caller.SendAsync("ReceiveUpdate", "Update requested");
    }
}
