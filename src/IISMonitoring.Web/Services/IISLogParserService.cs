using IISMonitoring.Web.Models;
using Microsoft.Web.Administration;
using System.Collections.Concurrent;
using System.Globalization;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio para parsear logs de IIS en formato W3C
/// </summary>
public class IISLogParserService
{
    private readonly ILogger<IISLogParserService> _logger;
    private readonly ConcurrentDictionary<string, long> _filePositions = new();
    private readonly ConcurrentDictionary<string, string> _siteIdToName = new();

    public IISLogParserService(ILogger<IISLogParserService> logger)
    {
        _logger = logger;
        LoadSiteNames();
    }

    /// <summary>
    /// Carga los nombres de los sitios IIS
    /// </summary>
    private void LoadSiteNames()
    {
        try
        {
            using var serverManager = new ServerManager();
            foreach (var site in serverManager.Sites)
            {
                _siteIdToName[site.Id.ToString()] = site.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando nombres de sitios IIS");
        }
    }

    /// <summary>
    /// Obtiene las nuevas entradas de log desde la última lectura
    /// </summary>
    public async Task<List<IISLogEntry>> GetNewLogEntriesAsync(List<string> siteIds, int maxEntries = 1000)
    {
        var entries = new List<IISLogEntry>();

        try
        {
            var logsBaseDir = @"C:\inetpub\logs\LogFiles";
            if (!Directory.Exists(logsBaseDir))
            {
                _logger.LogWarning("Directorio de logs de IIS no encontrado: {Path}", logsBaseDir);
                return entries;
            }

            foreach (var siteId in siteIds)
            {
                var siteLogDir = Path.Combine(logsBaseDir, $"W3SVC{siteId}");
                if (!Directory.Exists(siteLogDir))
                {
                    continue;
                }

                // Obtener el archivo de log más reciente
                var logFiles = Directory.GetFiles(siteLogDir, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(1)
                    .ToList();

                foreach (var logFile in logFiles)
                {
                    var newEntries = await ReadLogFileFromPositionAsync(logFile.FullName, siteId, maxEntries);
                    entries.AddRange(newEntries);
                }
            }

            return entries.OrderByDescending(e => e.DateTime).Take(maxEntries).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo entradas de log de IIS");
            return entries;
        }
    }

    /// <summary>
    /// Lee un archivo de log desde la última posición conocida
    /// </summary>
    private async Task<List<IISLogEntry>> ReadLogFileFromPositionAsync(string filePath, string siteId, int maxEntries)
    {
        var entries = new List<IISLogEntry>();

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return entries;
            }

            // Obtener la última posición leída
            var lastPosition = _filePositions.GetOrAdd(filePath, 0);

            // Si el archivo es más pequeño que la última posición, empezar desde el inicio (archivo rotado)
            if (fileInfo.Length < lastPosition)
            {
                lastPosition = 0;
            }

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(lastPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(fileStream);

            var fields = new List<string>();
            string? line;
            var lineCount = 0;

            while ((line = await reader.ReadLineAsync()) != null && lineCount < maxEntries)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Parsear línea de campos
                if (line.StartsWith("#Fields:"))
                {
                    fields = line.Substring(9).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    continue;
                }

                // Ignorar otras líneas de comentario
                if (line.StartsWith("#"))
                {
                    continue;
                }

                // Parsear entrada de log
                if (fields.Any())
                {
                    var entry = ParseLogLine(line, fields, siteId);
                    if (entry != null)
                    {
                        entries.Add(entry);
                        lineCount++;
                    }
                }
            }

            // Actualizar la posición actual
            _filePositions[filePath] = fileStream.Position;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo archivo de log: {FilePath}", filePath);
        }

        return entries;
    }

    /// <summary>
    /// Parsea una línea de log
    /// </summary>
    private IISLogEntry? ParseLogLine(string line, List<string> fields, string siteId)
    {
        try
        {
            var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < fields.Count)
            {
                return null;
            }

            var entry = new IISLogEntry
            {
                SiteId = siteId,
                SiteName = _siteIdToName.GetValueOrDefault(siteId, $"Site {siteId}")
            };

            for (int i = 0; i < fields.Count && i < values.Length; i++)
            {
                var field = fields[i];
                var value = values[i];

                if (value == "-") continue; // Valor no presente

                switch (field)
                {
                    case "date":
                        if (i + 1 < values.Length && fields[i + 1] == "time")
                        {
                            var dateStr = $"{value} {values[i + 1]}";
                            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss",
                                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime))
                            {
                                entry.DateTime = dateTime.ToUniversalTime();
                            }
                        }
                        break;

                    case "s-ip":
                        entry.ServerIp = value;
                        break;

                    case "cs-method":
                        entry.Method = value;
                        break;

                    case "cs-uri-stem":
                        entry.UriStem = value;
                        break;

                    case "cs-uri-query":
                        entry.UriQuery = value;
                        break;

                    case "s-port":
                        if (int.TryParse(value, out var port))
                            entry.ServerPort = port;
                        break;

                    case "cs-username":
                        entry.Username = value;
                        break;

                    case "c-ip":
                        entry.ClientIp = value;
                        break;

                    case "cs(User-Agent)":
                        entry.UserAgent = value;
                        break;

                    case "cs(Referer)":
                        entry.Referer = value;
                        break;

                    case "sc-status":
                        if (int.TryParse(value, out var status))
                            entry.StatusCode = status;
                        break;

                    case "sc-substatus":
                        if (int.TryParse(value, out var substatus))
                            entry.SubStatus = substatus;
                        break;

                    case "sc-win32-status":
                        if (int.TryParse(value, out var win32status))
                            entry.Win32Status = win32status;
                        break;

                    case "time-taken":
                        if (int.TryParse(value, out var timeTaken))
                            entry.TimeTaken = timeTaken;
                        break;

                    case "cs-host":
                    case "cs(Host)":
                        entry.Host = value;
                        break;
                }
            }

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parseando línea de log: {Line}", line);
            return null;
        }
    }

    /// <summary>
    /// Reinicia las posiciones de lectura de archivos
    /// </summary>
    public void ResetFilePositions()
    {
        _filePositions.Clear();
    }

    /// <summary>
    /// Obtiene la lista de sitios IIS disponibles
    /// </summary>
    public List<(string Id, string Name)> GetAvailableSites()
    {
        var sites = new List<(string Id, string Name)>();

        try
        {
            using var serverManager = new ServerManager();
            foreach (var site in serverManager.Sites)
            {
                sites.Add((site.Id.ToString(), site.Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo sitios IIS");
        }

        return sites;
    }
}
