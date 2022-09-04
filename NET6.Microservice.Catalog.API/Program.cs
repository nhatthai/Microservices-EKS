using NET6.Microservice.Core.PathBases;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

InitOpenTelemetryTracing(builder, configuration);

// Add the IStartupFilter using the helper method
PathBaseStartup.AddPathBaseFilter(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void InitOpenTelemetryTracing(WebApplicationBuilder builder, IConfiguration configuration)
{
    var serviceName = "CatalogApi";

    builder.Services.AddOpenTelemetryTracing(builder =>
    {
        builder
            .AddSource(serviceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddZipkinExporter(options =>
            {
                var zipkinURI = configuration.GetValue<string>("OpenTelemetry:ZipkinURI");
                if (!string.IsNullOrEmpty(zipkinURI))
                {
                    options.Endpoint = new Uri(zipkinURI);
                }
            });
    });
}