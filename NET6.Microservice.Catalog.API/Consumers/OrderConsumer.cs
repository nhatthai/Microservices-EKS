using System.Diagnostics;
using MassTransit;
using NET6.Microservice.Core.OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace NET6.Microservice.Catalog.API.Consumers
{
    public class OrderConsumer : IConsumer<Messages.Commands.OrderMessage>
    {
        private readonly ILogger<OrderConsumer> _logger;
        private static readonly ActivitySource _activitySource = new ActivitySource("CatalogOrderConsumer");
        private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

        public OrderConsumer(ILogger<OrderConsumer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task Consume(ConsumeContext<Messages.Commands.OrderMessage> context)
        {
            var data = context.Message;
            var correlationId = data.CorrelationId;

            _logger.LogInformation("Catalog Consume Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);

            // set property for extracting Propagation context
            var pros = new Dictionary<string, object>();
            pros["traceparent"] = correlationId;

            // Extract the PropagationContext of order message
            var parentContext = Propagator.Extract(default, pros, OpenTelemetryActivity.ExtractTraceContextFromProperties);

            using var activity = _activitySource.StartActivity(
                "Catalog Order Consumer", ActivityKind.Consumer, parentContext.ActivityContext);

            OpenTelemetryActivity.AddActivityTagsMessage(activity);

            try
            {
                Task.Delay(1000);
                activity?.SetStatus(ActivityStatusCode.Ok, "Consumed a message and processed successfully.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to process. {CorrelationId} ", correlationId);
                activity?.SetStatus(ActivityStatusCode.Error, "Error occured when OrderConsumer");
            }

            _logger.LogInformation("Consumed Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);
            return Task.CompletedTask;
        }
    }
}