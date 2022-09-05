using System.Diagnostics;
using MassTransit;
using NET6.Microservice.WorkerService.Services;
using OpenTelemetry.Trace;

namespace NET6.Microservice.WorkerService.Consumers
{
    public class OrderConsumer : IConsumer<Messages.Commands.Order>
    {
        private readonly ILogger<OrderConsumer> _logger;
        private readonly EmailService _emailService;
        private static readonly ActivitySource _activitySource = new ActivitySource(nameof(OrderConsumer));
        private readonly Tracer _tracer;

        public OrderConsumer(ILogger<OrderConsumer> logger, EmailService emailService, Tracer tracer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _tracer = tracer;
        }

        public Task Consume(ConsumeContext<Messages.Commands.Order> context)
        {
            //using var span = _tracer.StartActiveSpan("Order.Product Consumer", SpanKind.Consumer);
            using var activity = _activitySource.StartActivity("OrderProduct", ActivityKind.Consumer);

            var data = context.Message;
            var correlationId = data.CorrelationId;
            activity?.SetTag("correlationId", correlationId);

            _logger.LogInformation("Consume Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);

            try
            {
                // TODO: call service/task
                Task.Delay(2000);
                _emailService.SendEmail(correlationId, Guid.NewGuid(), "testing@domain.com", "Order: " + data.OrderNumber);
                //span.SetStatus(Status.Ok);
                activity?.SetStatus(ActivityStatusCode.Ok, "Consume a message and process successfully.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to send Email. {CorrelationId} ", correlationId);
                activity?.SetStatus(ActivityStatusCode.Error, "Error occured when sending email in OrderConsumer");
            }

            _logger.LogInformation("Consumed Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);
            return Task.CompletedTask;
        }
    }
}