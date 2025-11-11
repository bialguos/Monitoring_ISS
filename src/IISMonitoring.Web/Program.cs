using IISMonitoring.Web.Configuration;
using IISMonitoring.Web.Hubs;
using IISMonitoring.Web.Services;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "logs/iis-monitoring-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Usar Serilog como proveedor de logging
builder.Host.UseSerilog();

// Configurar opciones de monitorización desde appsettings.json
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection(MonitoringOptions.SectionName));

// Agregar servicios al contenedor
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrar el servicio de monitorización optimizado como Singleton
// Usamos Singleton porque cachea los PerformanceCounters para mejor rendimiento
builder.Services.AddSingleton<IIISMonitoringService, OptimizedIISMonitoringService>();

// Registrar el servicio de datos históricos como Singleton
// Usamos Singleton para gestionar el acceso al archivo de forma centralizada
builder.Services.AddSingleton<HistoricalDataService>();

// Registrar el servicio de APM como Singleton
// Usamos Singleton para mantener el estado de traces en memoria
builder.Services.AddSingleton<IApmService, ApmService>();

// Registrar el servicio de parseo de logs de IIS como Singleton
builder.Services.AddSingleton<IISLogParserService>();

// Registrar el servicio de configuración de APM como Singleton
builder.Services.AddSingleton<ApmConfigurationService>();

// Registrar el servicio de logs como Scoped
builder.Services.AddScoped<ILogService, LogService>();

// Registrar servicios en segundo plano
builder.Services.AddHostedService<MonitoringBackgroundService>();
builder.Services.AddHostedService<IISApmBackgroundService>();

// Configurar CORS para desarrollo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configurar el pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();
app.MapHub<MonitoringHub>("/monitoringHub");

// Página principal
app.MapGet("/", () => Results.Redirect("/index.html"));

try
{
    Log.Information("Iniciando aplicación de monitorización IIS");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
