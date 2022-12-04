using MassTransit;
using Microsoft.AspNetCore.Hosting;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Messages;
using NET6.Microservice.WorkerService;
using NET6.Microservice.WorkerService.Consumers;
using NET6.Microservice.WorkerService.Services;
using Serilog;
using Serilog.Exceptions;

// This is required if the collector doesn't expose an https endpoint. By default, .NET
// only allows http2 (required for gRPC) to secure endpoints.
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "WorkerService";
    })
    .ConfigureAppConfiguration((host, builder) =>{
        //builder.AddConfiguration(configuration);
        builder.AddEnvironmentVariables();
        //builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureLogging((hostingContext,logging) => {
        logging.ClearProviders();

        var configuration = hostingContext.Configuration;

        Log.Logger = new LoggerConfiguration()
            .Enrich.WithExceptionDetails()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Environment", configuration.GetValue<string>("Environment"))
            .CreateLogger();

        logging.AddSerilog(Log.Logger);
    })
    .ConfigureServices((hostingContext, services) =>
    {
        var configuration = hostingContext.Configuration;

        services.AddOptions();
        services.AddSingleton<EmailService>();
        services.AddOptions<MassTransitConfiguration>().Bind(configuration.GetSection("MassTransit"));

        InitMassTransitConfig(services,configuration);

        string[] sources = new string[1] { "OrderConsumer" };
        string otlpExporterUri = configuration.GetValue<string>("OpenTelemetry:OtelCollector");
        OpenTelemetryStartup.InitOpenTelemetryTracing(services, configuration, "Worker", sources, otlpExporterUri);
        
        services.AddHostedService<Worker>();
    })
    .Build();

static void InitMassTransitConfig(IServiceCollection services, IConfiguration configuration)
{
    services.AddOptions<MassTransitConfiguration>();
    services.Configure<MassTransitConfiguration>(configuration.GetSection("MassTransit"));

    var massTransitConfiguration = configuration.GetSection("MassTransit").Get<MassTransitConfiguration>();

    services.AddMassTransit(configureMassTransit =>
    {
        if (massTransitConfiguration == null)
        {
            throw new ArgumentNullException("MassTransit config is null");
        }

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

    services.AddMassTransitHostedService();

    // OPTIONAL, but can be used to configure the bus options
    services.AddOptions<MassTransitHostOptions>().Configure(options =>
    {
        // if specified, waits until the bus is started before
        // returning from IHostedService.StartAsync
        // default is false
        options.WaitUntilStarted = true;

        // if specified, limits the wait time when starting the bus
        options.StartTimeout = TimeSpan.FromSeconds(40);

        // if specified, limits the wait time when stopping the bus
        options.StopTimeout = TimeSpan.FromSeconds(60);
    });
}

//await host.Services.GetRequiredService<IBusControl>().StartAsync();
await host.RunAsync();
