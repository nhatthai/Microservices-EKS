using MassTransit;
using Microsoft.EntityFrameworkCore;
using NET6.Microservice.Catalog.API.Consumers;
using NET6.Microservice.Catalog.API.Infrastructure;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Core.PathBases;
using NET6.Microservice.Messages;
using NET6.Microservices.Catalog.API;
using NET6.Microservices.Catalog.API.Infrastructure;
using Serilog;
using Serilog.Exceptions;

// This is required if the collector doesn't expose an https endpoint. By default, .NET
// only allows http2 (required for gRPC) to secure endpoints.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

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
builder.Services.AddOptions<MassTransitConfiguration>().Bind(configuration.GetSection("MassTransit"));

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

// AddDbContext(builder.Services, configuration);
InitMassTransitConfig(builder.Services, configuration);

var sources = new string[] { "OrderController" };
var otlpExporterUri = configuration.GetValue<string>("OpenTelemetry:OtelCollector");
OpenTelemetryStartup.InitOpenTelemetryTracing(
    builder.Services, configuration, "CatalogAPI", Array.Empty<string>(), "http://localhost:4317", builder.Environment);

// Add the IStartupFilter using the helper method
PathBaseStartup.AddPathBaseFilter(builder);

var app = builder.Build();

// migrate any database changes on startup (includes initial db creation)
// using (var scope = app.Services.CreateScope())
// {
//     var dataContext = scope.ServiceProvider.GetRequiredService<CatalogContext>();
//     var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
//     dataContext.Database.Migrate();
//     new CatalogContextSeed().SeedAsync(dataContext, env).Wait();
// }

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
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 15,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    });
}

static void InitMassTransitConfig(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<MassTransitConfiguration>();
    services.Configure<MassTransitConfiguration>(configuration.GetSection("MassTransit"));

    var massTransitConfiguration = configuration.GetSection("MassTransit").Get<MassTransitConfiguration>();

    services.AddMassTransit(configureMassTransit =>
    {
        configureMassTransit.AddConsumer<OrderConsumer>(configureConsumer =>
        {
            configureConsumer.UseConcurrentMessageLimit(2);
        });

        if (massTransitConfiguration.IsUsingAmazonSQS)
        {
            configureMassTransit.UsingAmazonSqs((context, configure) =>
            {
                var messageBusSQS = String.Format("{0}:{1}@{2}",
                    massTransitConfiguration.AwsAccessKey,
                    massTransitConfiguration.AwsSecretKey,
                    massTransitConfiguration.AwsRegion);
                ServiceBusConnectionConfig.ConfigureNodes(configure,  messageBusSQS);

                //configure.ConfigureEndpoints(context);

                configure.ReceiveEndpoint(massTransitConfiguration.OrderQueue, receive =>
                {
                    receive.ConfigureConsumer<OrderConsumer>(context);

                    receive.PrefetchCount = 4;
                });
            });
        }
        else
        {
            configureMassTransit.UsingRabbitMq((context, configure) =>
            {
                configure.PrefetchCount = 4;

                // Ensures the processor gets its own queue for any consumed messages
                configure.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(true));

                configure.ReceiveEndpoint(massTransitConfiguration.OrderQueue, receive =>
                {
                    receive.ConfigureConsumer<OrderConsumer>(context);
                });

                ServiceBusConnectionConfig.ConfigureNodes(configure, massTransitConfiguration.MessageBusRabbitMQ);
            });
        }
    });
}