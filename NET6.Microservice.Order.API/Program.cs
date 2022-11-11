using System.Configuration;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Core.PathBases;
using NET6.Microservice.Messages;
using NET6.Microservice.Messages.Commands;
using NET6.Microservice.Order.API;
using NET6.Microservice.Order.API.Domain.AggregateModels.OrderAggregates;
using NET6.Microservice.Order.API.Infrastructure;
using NET6.Microservice.Order.API.Infrastructure.Repositories;
using NET6.Microservice.Order.API.Queries;
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

builder.Services.AddOptions<MassTransitConfiguration>().Bind(configuration.GetSection("MassTransit"));

AddCustomDBContext(builder.Services, configuration);

InitMassTransitConfig(builder.Services, configuration);

var sources = new string[] { "OrderController" };
OpenTelemetryStartup.InitOpenTelemetryTracing(builder.Services, configuration, "OrderAPI", sources, builder.Environment);


// Call UseServiceProviderFactory on the Host sub property
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

// Call ConfigureContainer on the Host sub property
builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
{
    builder.Register(c => new OrderQueries(configuration["ConnectionString"]))
            .As<IOrderQueries>()
            .InstancePerLifetimeScope();

    builder.RegisterType<OrderRepository>()
        .As<IOrderRepository>()
        .InstancePerLifetimeScope();
});

// Add the IStartupFilter using the helper method
PathBaseStartup.AddPathBaseFilter(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddCustomDBContext(IServiceCollection services, IConfiguration configuration)
{
    services.AddEntityFrameworkSqlServer().AddDbContext<OrderingContext>(options =>
    {
        options.UseSqlServer(configuration["ConnectionString"],
            sqlServerOptionsAction: sqlOptions =>
            {
                //sqlOptions.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 15, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
            });
    },
        ServiceLifetime.Scoped  //Showing explicitly that the DbContext is shared across the HTTP request scope (graph of objects started in the HTTP request)
    );
}

static void InitMassTransitConfig(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<MassTransitConfiguration>();
    services.Configure<MassTransitConfiguration>(configuration.GetSection("MassTransit"));

    var massTransitConfiguration = configuration.GetSection("MassTransit").Get<MassTransitConfiguration>();

    services.AddMassTransit(configureMassTransit =>
    {
        if(massTransitConfiguration.IsUsingAmazonSQS)
        {
            configureMassTransit.UsingAmazonSqs((context, configure) =>
            {
                ServiceBusConnectionConfig.ConfigureNodes(configure, massTransitConfiguration.MessageBusSQS);
            });
        }
        else
        {
            configureMassTransit.UsingRabbitMq((context, configure) =>
            {
                // Ensures the processor gets its own queue for any consumed messages
                //configure.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(true));
                ServiceBusConnectionConfig.ConfigureNodes(configure, massTransitConfiguration.MessageBusRabbitMQ);
            });
        }
    });

    EndpointConvention.Map<OrderMessage>(new Uri(massTransitConfiguration.OrderQueue));
}

public partial class Program { }