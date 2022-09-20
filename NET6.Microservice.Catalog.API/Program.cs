using Microsoft.EntityFrameworkCore;
using NET6.Microservice.Catalog.API.Infrastructure;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Core.PathBases;
using NET6.Microservices.Catalog.API;
using NET6.Microservices.Catalog.API.Infrastructure;
using Serilog;
using Serilog.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

//create the logger and setup your sinks, filters and properties
Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Environment", configuration.GetValue<string>("Environment"))
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.Configure<CatalogSettings>(configuration);

// Add services to the container.
builder.Services.AddCors(c =>
{
    c.AddPolicy("AllowOrigin", builder =>
        builder.WithOrigins(configuration.GetValue<string>("OriginWeb"))
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
    );
});

AddDbContext(builder.Services, configuration);

OpenTelemetryStartup.InitOpenTelemetryTracing(
    builder.Services, configuration, "CatalogAPI", Array.Empty<string>(), builder.Environment);

// Add the IStartupFilter using the helper method
PathBaseStartup.AddPathBaseFilter(builder);

var app = builder.Build();

// migrate any database changes on startup (includes initial db creation)
using (var scope = app.Services.CreateScope())
{
    var dataContext = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
    dataContext.Database.Migrate();
    new CatalogContextSeed().SeedAsync(dataContext, env).Wait();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowOrigin");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddDbContext(IServiceCollection services, IConfiguration configuration)
{
    services.AddEntityFrameworkSqlServer().AddDbContext<CatalogContext>(options =>
    {
        options.UseSqlServer(configuration["ConnectionStrings:CatalogContext"],  sqlServerOptionsAction: sqlOptions =>
        {
            //Configuring Connection Resiliency: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        });
    });
}