using MassTransit;
using Microsoft.AspNetCore.Hosting;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Messages;
using NET6.Microservice.WorkerService;
using NET6.Microservice.WorkerService.Consumers;
using NET6.Microservice.WorkerService.Services;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Exceptions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .Enrich.WithExceptionDetails()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Environment", configuration.GetValue<string>("Environment"))
    .CreateLogger();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddOptions();
        services.AddSingleton<EmailService>();

        InitMassTransitConfig(services, configuration);

        string[] sources = new string[1] { nameof(OrderConsumer) };
        OpenTelemetryStartup.InitOpenTelemetryTracing(services, configuration, "Worker", sources);

        services.AddSingleton(TracerProvider.Default.GetTracer("OrderConsumer"));

        services.AddHostedService<Worker>();
    })
    .ConfigureLogging(logging => {
        logging.ClearProviders();
        logging.AddSerilog(Log.Logger);
    })
    .Build();

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

        if (massTransitConfiguration == null)
        {
            throw new ArgumentNullException("MassTransit config is null");
        }

        if (massTransitConfiguration.IsUsingAmazonSQS)
        {
            configureMassTransit.UsingAmazonSqs((context, configure) =>
            {
                ServiceBusConnectionConfig.ConfigureNodes(configure, massTransitConfiguration.MessageBusSQS);

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

    //services.AddMassTransitHostedService();
}

await host.RunAsync();
