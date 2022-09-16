using Microsoft.Extensions.Options;
using NET6.Microservice.Basket.API;
using NET6.Microservice.Basket.API.Infrastructure.Filters;
using NET6.Microservice.Basket.API.Infrastructure.Repositories;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Core.PathBases;
using Serilog;
using Serilog.Exceptions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(
    options =>
    {
        options.Filters.Add(typeof(HttpGlobalExceptionFilter));
        options.Filters.Add(typeof(ValidateModelStateFilter));
    }
).AddJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);

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

builder.Services.AddOptions();
builder.Services.Configure<BasketSettings>(configuration);

builder.Services.AddSingleton<ConnectionMultiplexer>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<BasketSettings>>().Value;
    var configuration = ConfigurationOptions.Parse(settings.ConnectionString, true);

    configuration.ResolveDns = true;

    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<IBasketRepository, RedisBasketRepository>();

OpenTelemetryStartup.InitOpenTelemetryTracing(
    builder.Services, configuration, "BasketAPI", Array.Empty<string>(), builder.Environment);

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
