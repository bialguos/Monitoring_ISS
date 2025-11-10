namespace IISMonitoring.Web.Models;

public class WebSiteInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Id { get; set; }
    public List<string> Bindings { get; set; } = new();
    public string ApplicationPool { get; set; } = string.Empty;
    public string PhysicalPath { get; set; } = string.Empty;
    public long RequestsPerSecond { get; set; }
    public long BytesSentPerSecond { get; set; }
    public long BytesReceivedPerSecond { get; set; }
    public int CurrentConnections { get; set; }
}
