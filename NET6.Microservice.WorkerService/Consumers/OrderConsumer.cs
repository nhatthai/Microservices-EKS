using MassTransit;
using NET6.Microservice.WorkerService.Services;


namespace NET6.Microservice.WorkerService.Consumers
{
    public class OrderConsumer : IConsumer<Messages.Commands.Order>
    {
        private readonly ILogger<OrderConsumer> _logger;
        private readonly EmailService _emailService;

        public OrderConsumer(ILogger<OrderConsumer> logger, EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        public Task Consume(ConsumeContext<Messages.Commands.Order> context)
        {
            var data = context.Message;
            var correlationId = Guid.NewGuid();

            _logger.LogInformation("Consume Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);

            try
            {
                // TODO: call service/task
                Task.Delay(2000);
                _emailService.SendEmail(correlationId, "testing@domain.com", "Order: " + data.OrderNumber);
            }
            catch (Exception exception)
            {
                _logger.LogError( exception, "Unable to send Email. {CorrelationId} ", correlationId);
            }

            _logger.LogInformation("Consumed Order Message {CorrelationId} {OrderNumber}", correlationId, data.OrderNumber);

            return Task.CompletedTask;
        }
    }
}