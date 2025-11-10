using IISMonitoring.Web.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio para gestionar y leer archivos de log de Serilog
/// </summary>
public class LogService : ILogService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LogService> _logger;
    private readonly IIISMonitoringService _iisMonitoringService;

    // Patrón regex para parsear logs de Serilog
    // Formato: 2025-11-10 10:30:45.123 +00:00 [INF] Mensaje del log
    private static readonly Regex LogPattern = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}\s+[+-]\d{2}:\d{2})\s+\[(?<level>\w{3})\]\s+(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public LogService(IWebHostEnvironment environment, ILogger<LogService> logger, IIISMonitoringService iisMonitoringService)
    {
        _environment = environment;
        _logger = logger;
        _iisMonitoringService = iisMonitoringService;
    }

    /// <inheritdoc />
    public async Task<LogFilesResponse> GetLogFilesAsync()
    {
        var response = new LogFilesResponse();
        var logDirectories = new HashSet<string>();

        try
        {
            // 1. Buscar directorios "logs" desde el directorio raíz del contenido de la aplicación
            var contentRoot = _environment.ContentRootPath;
            var logsDir = Path.Combine(contentRoot, "logs");

            if (Directory.Exists(logsDir) && !logDirectories.Contains(logsDir))
            {
                logDirectories.Add(logsDir);
                await ScanDirectoryForLogs(logsDir, response.LogFiles);
            }

            // 2. Buscar recursivamente otros directorios llamados "logs" en la aplicación
            await ScanForLogDirectories(contentRoot, logDirectories, response.LogFiles);

            // 3. Buscar logs en los directorios físicos de los sitios web de IIS
            await ScanIISSitesForLogs(logDirectories, response.LogFiles);

            response.LogDirectories = logDirectories.OrderBy(d => d).ToList();
            response.TotalFiles = response.LogFiles.Count;
            response.TotalSizeBytes = response.LogFiles.Sum(f => f.SizeBytes);

            // Ordenar por fecha de modificación (más reciente primero)
            response.LogFiles = response.LogFiles.OrderByDescending(f => f.LastModified).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de archivos de log");
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<LogContentResponse> GetLogContentAsync(
        string fileIdentifier,
        int skip = 0,
        int take = 100,
        string? level = null,
        string? search = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool sortDescending = true)
    {
        var response = new LogContentResponse();

        try
        {
            // Decodificar la ruta del archivo desde Base64
            var filePathBytes = Convert.FromBase64String(fileIdentifier);
            var filePath = System.Text.Encoding.UTF8.GetString(filePathBytes);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Archivo de log no encontrado: {FilePath}", filePath);
                return new LogContentResponse
                {
                    FileName = Path.GetFileName(filePath)
                };
            }

            response.FileName = Path.GetFileName(filePath);

            // Leer todas las líneas del archivo
            var allLines = await File.ReadAllLinesAsync(filePath);
            response.TotalLines = allLines.Length;

            // Parsear las líneas
            var entries = ParseLogLines(allLines);

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(level))
            {
                entries = entries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries.Where(e =>
                    e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.Exception?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            // Aplicar filtro de fechas
            if (dateFrom.HasValue)
            {
                entries = entries.Where(e => e.Timestamp >= dateFrom.Value).ToList();
            }

            if (dateTo.HasValue)
            {
                entries = entries.Where(e => e.Timestamp <= dateTo.Value).ToList();
            }

            // Aplicar ordenamiento
            entries = sortDescending
                ? entries.OrderByDescending(e => e.Timestamp).ToList()
                : entries.OrderBy(e => e.Timestamp).ToList();

            // Aplicar paginación
            response.ReturnedLines = Math.Min(take, entries.Count - skip);
            response.HasMore = skip + take < entries.Count;
            response.Entries = entries.Skip(skip).Take(take).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer el contenido del archivo de log: {FileIdentifier}", fileIdentifier);
        }

        return response;
    }

    /// <summary>
    /// Escanea un directorio en busca de archivos de log (*.log y log-*)
    /// </summary>
    private Task ScanDirectoryForLogs(string directory, List<LogFileInfo> logFiles, string? iisSiteName = null)
    {
        try
        {
            // Buscar archivos con extensión .log
            var logExtFiles = Directory.GetFiles(directory, "*.log");

            // Buscar archivos que empiecen con "log-"
            var logPrefixFiles = Directory.GetFiles(directory, "log-*");

            // Combinar y eliminar duplicados
            var allFiles = logExtFiles.Union(logPrefixFiles).Distinct().ToList();

            foreach (var file in allFiles)
            {
                var fileInfo = new FileInfo(file);

                // No contar líneas al listar archivos (optimización de rendimiento)
                // El conteo de líneas solo se realiza cuando se abre el archivo específico

                logFiles.Add(new LogFileInfo
                {
                    FileName = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    SizeBytes = fileInfo.Length,
                    SizeFormatted = FormatFileSize(fileInfo.Length),
                    LastModified = fileInfo.LastWriteTime,
                    Directory = fileInfo.DirectoryName ?? string.Empty,
                    LineCount = 0, // No se cuenta al listar por rendimiento
                    IISSiteName = iisSiteName
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al escanear el directorio: {Directory}", directory);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Busca recursivamente directorios llamados "logs"
    /// </summary>
    private async Task ScanForLogDirectories(
        string rootDirectory,
        HashSet<string> logDirectories,
        List<LogFileInfo> logFiles,
        int maxDepth = 3,
        int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
            return;

        try
        {
            var directories = Directory.GetDirectories(rootDirectory);

            foreach (var dir in directories)
            {
                var dirName = new DirectoryInfo(dir).Name.ToLowerInvariant();

                // Si el directorio se llama "logs", agrégalo
                if (dirName == "logs" && !logDirectories.Contains(dir))
                {
                    logDirectories.Add(dir);
                    await ScanDirectoryForLogs(dir, logFiles);
                }

                // Continuar búsqueda recursiva (evitar directorios del sistema)
                if (dirName != "bin" && dirName != "obj" && dirName != "node_modules" &&
                    dirName != ".git" && dirName != ".vs")
                {
                    await ScanForLogDirectories(dir, logDirectories, logFiles, maxDepth, currentDepth + 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar directorios de logs en: {RootDirectory}", rootDirectory);
        }
    }

    /// <summary>
    /// Parsea las líneas de log utilizando el patrón de Serilog
    /// </summary>
    private List<LogEntry> ParseLogLines(string[] lines)
    {
        var entries = new List<LogEntry>();
        LogEntry? currentEntry = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = LogPattern.Match(line);

            if (match.Success)
            {
                // Si hay una entrada anterior, agrégala
                if (currentEntry != null)
                {
                    entries.Add(currentEntry);
                }

                // Crear nueva entrada
                currentEntry = new LogEntry
                {
                    Timestamp = DateTime.Parse(match.Groups["timestamp"].Value, CultureInfo.InvariantCulture),
                    Level = MapLogLevel(match.Groups["level"].Value),
                    Message = match.Groups["message"].Value.Trim(),
                    RawLine = line
                };
            }
            else if (currentEntry != null)
            {
                // Esta línea es parte de una excepción o mensaje multi-línea
                if (currentEntry.Exception == null && line.TrimStart().StartsWith("System."))
                {
                    currentEntry.Exception = line;
                }
                else if (currentEntry.Exception != null)
                {
                    currentEntry.Exception += Environment.NewLine + line;
                }
                else
                {
                    currentEntry.Message += Environment.NewLine + line;
                }
            }
        }

        // Agregar la última entrada
        if (currentEntry != null)
        {
            entries.Add(currentEntry);
        }

        return entries;
    }

    /// <summary>
    /// Mapea los niveles de log de Serilog (INF, WRN, ERR, etc.) a nombres completos
    /// </summary>
    private string MapLogLevel(string shortLevel)
    {
        return shortLevel.ToUpperInvariant() switch
        {
            "VRB" => "Verbose",
            "DBG" => "Debug",
            "INF" => "Information",
            "WRN" => "Warning",
            "ERR" => "Error",
            "FTL" => "Fatal",
            _ => shortLevel
        };
    }

    /// <summary>
    /// Escanea los directorios físicos de los sitios web de IIS en busca de logs
    /// </summary>
    private async Task ScanIISSitesForLogs(HashSet<string> logDirectories, List<LogFileInfo> logFiles)
    {
        try
        {
            // Obtener la lista de sitios web de IIS
            var webSites = await _iisMonitoringService.GetWebSitesAsync();

            foreach (var site in webSites)
            {
                if (string.IsNullOrWhiteSpace(site.PhysicalPath))
                    continue;

                try
                {
                    // Expandir variables de entorno en la ruta física (ej: %SystemDrive%)
                    var expandedPath = Environment.ExpandEnvironmentVariables(site.PhysicalPath);

                    if (!Directory.Exists(expandedPath))
                    {
                        _logger.LogDebug("El directorio físico del sitio {SiteName} no existe: {Path}", site.Name, expandedPath);
                        continue;
                    }

                    // Buscar directorio "logs" dentro del sitio
                    var siteLogsDir = Path.Combine(expandedPath, "logs");
                    if (Directory.Exists(siteLogsDir) && !logDirectories.Contains(siteLogsDir))
                    {
                        _logger.LogInformation("Directorio de logs encontrado para el sitio {SiteName}: {LogsDir}", site.Name, siteLogsDir);
                        logDirectories.Add(siteLogsDir);
                        await ScanDirectoryForLogs(siteLogsDir, logFiles, site.Name);
                    }

                    // Buscar también en subdirectorios comunes
                    var commonLogPaths = new[] { "Logs", "Log", "log" };
                    foreach (var logPath in commonLogPaths)
                    {
                        var logDir = Path.Combine(expandedPath, logPath);
                        if (Directory.Exists(logDir) && !logDirectories.Contains(logDir))
                        {
                            _logger.LogInformation("Directorio de logs encontrado para el sitio {SiteName}: {LogsDir}", site.Name, logDir);
                            logDirectories.Add(logDir);
                            await ScanDirectoryForLogs(logDir, logFiles, site.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al escanear logs del sitio {SiteName} en {PhysicalPath}", site.Name, site.PhysicalPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sitios de IIS para buscar logs");
        }
    }

    /// <summary>
    /// Formatea el tamaño de archivo a una cadena legible
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
