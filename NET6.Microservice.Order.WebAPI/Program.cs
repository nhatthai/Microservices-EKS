using MassTransit;
using NET6.Microservice.Core.PathBases;
using NET6.Microservice.Messages;
using NET6.Microservice.Messages.Commands;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var configuration = builder.Configuration;

builder.Services.AddOptions<MassTransitConfiguration>().Bind(configuration.GetSection("MassTransit"));
InitMassTransitConfig(builder.Services, configuration);

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


static void InitMassTransitConfig(IServiceCollection services, IConfiguration configuration)
{
    services.AddMassTransit(configureMassTransit =>
    {
        if(Boolean.Parse(configuration["MassTransit:IsUsingAmazonSQS"]))
        {
            configureMassTransit.UsingAmazonSqs((context, configure) =>
            {
                ServiceBusConnectionConfig.ConfigureNodes(configuration, configure, "MassTransit:MessageBusSQS");
            });
        }
        else
        {
            configureMassTransit.UsingRabbitMq((context, configure) =>
            {
                // Ensures the processor gets its own queue for any consumed messages
                //configure.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(true));
                ServiceBusConnectionConfig.ConfigureNodes(configuration, configure, "MassTransit:MessageBusRabbitMQ");
            });
        }
    });

    EndpointConvention.Map<Order>(new Uri(configuration["MassTransit:OrderQueue"]));
}

public partial class Program { }