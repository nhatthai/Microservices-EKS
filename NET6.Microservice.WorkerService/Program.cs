using MassTransit;
using NET6.Microservice.Messages;
using NET6.Microservice.WorkerService;
using NET6.Microservice.WorkerService.Consumers;
using NET6.Microservice.WorkerService.Services;
using OpenTelemetry.Resources;
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

        services.AddOpenTelemetryTracing(builder =>
        {
            builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Worker"))
                .AddSource(nameof(OrderConsumer)) // when we manually create activities, we need to setup the sources here
                .AddZipkinExporter(options =>
                {
                    var zipkinURI = configuration.GetValue<string>("OpenTelemetry:ZipkinURI");

                    if (!string.IsNullOrEmpty(zipkinURI))
                    {
                        options.Endpoint = new Uri(zipkinURI);
                    }
                })
                .AddJaegerExporter(options =>
                {
                    var agentHost = configuration.GetValue<string>("OpenTelemetry:JaegerHost");
                    var agentPort = configuration.GetValue<int>("OpenTelemetry:JaegerPort");

                    if (!string.IsNullOrEmpty(agentHost) && agentPort > 0)
                    {
                        options.AgentHost = agentHost;
                        options.AgentPort = agentPort;
                    }
                });
        });

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
