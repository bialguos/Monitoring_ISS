using IISMonitoring.Web.Configuration;
using IISMonitoring.Web.Hubs;
using IISMonitoring.Web.Services;

var builder = WebApplication.CreateBuilder(args);

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

// Registrar el servicio en segundo plano
builder.Services.AddHostedService<MonitoringBackgroundService>();

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

app.Run();
