using System.Diagnostics;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using NET6.Microservice.Order.API.Models.Requests;
using OpenTelemetry.Trace;

namespace NET6.Microservice.Order.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly IBus _bus;
        private readonly Tracer _tracer;
        private static readonly ActivitySource _activitySource = new ActivitySource(nameof(OrderController));

        public OrderController(ILogger<OrderController> logger, IBus bus, Tracer tracer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bus = bus ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        [HttpGet]
        public IEnumerable<string> Get()
        {
            _logger.LogInformation("Get Order API");
            return new List<string>();
        }

        [HttpPost]
        public async Task<IActionResult> OrderProduct(OrderRequest order)
        {
            //using var span = _tracer.StartActiveSpan("Order.Product Send", SpanKind.Producer);
            using var activity = _activitySource.StartActivity("OrderProduct", ActivityKind.Server);

            var correlationId = Guid.NewGuid();
            activity?.SetTag("correlationId", correlationId);

            _logger.LogInformation("Post Order API {CorrelationId} {OrderAmount}, {OrderNumber}", correlationId, order.OrderAmount, order.OrderNumber);

            // TODO: check store, and reduce

            if (order != null)
            {
                await _bus.Send(new Messages.Commands.Order()
                {
                    OrderId = correlationId,
                    OrderAmount = order.OrderAmount,
                    OrderDate = DateTime.Now,
                    OrderNumber = order.OrderNumber,
                    CorrelationId = activity?.Id
                }); ;

                _logger.LogInformation("Send to a message {CorrelationId} {OrderAmount}, {OrderNumber}", correlationId, order.OrderAmount, order.OrderNumber);

                //span.SetStatus(Status.Ok);
                activity?.SetStatus(ActivityStatusCode.Ok, "Send a message successfully.");

                return Ok();
            }

            activity?.SetStatus(ActivityStatusCode.Error, "Error occurred when sending a message in Post Order API");
            return BadRequest();
        }
    }
}