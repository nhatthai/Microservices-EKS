using System.Diagnostics;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using NET6.Microservice.Core.OpenTelemetry;
using NET6.Microservice.Order.API.Models.Requests;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace NET6.Microservice.Order.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly IBus _bus;
        private static readonly ActivitySource _activitySource = new ActivitySource(nameof(OrderController));
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        public OrderController(ILogger<OrderController> logger, IBus bus)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bus = bus ?? throw new ArgumentNullException(nameof(logger));
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
            using var activity = _activitySource.StartActivity("Order.Product Send", ActivityKind.Producer);

            _logger.LogInformation(
                "Post Order API {CorrelationId} {OrderAmount}, {OrderNumber}",
                 activity?.Id, order.OrderAmount, order.OrderNumber);

            if (order != null)
            {
                //set properties for Propagator
                var props = new Dictionary<string, object>();
                props["traceparent"] = activity?.Id;

                // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
                Propagator.Inject(
                    new PropagationContext(activity.Context, Baggage.Current), props,
                    OpenTelemetryActivity.InjectContextMessage);

                OpenTelemetryActivity.AddActivityTagsMessage(activity);

                await _bus.Send(new Messages.Commands.Order()
                {
                    OrderId = Guid.NewGuid(),
                    OrderAmount = order.OrderAmount,
                    OrderDate = DateTime.Now,
                    OrderNumber = order.OrderNumber,
                    CorrelationId = activity?.Id
                });

                _logger.LogInformation(
                    "Send to a message {CorrelationId} {OrderAmount}, {OrderNumber}",
                    activity?.Id, order.OrderAmount, order.OrderNumber);

                activity?.SetStatus(ActivityStatusCode.Ok, "Send a message successfully.");

                return Ok();
            }

            activity?.SetStatus(ActivityStatusCode.Error, "Error occurred when sending a message in Post Order API");

            return BadRequest();
        }

        [HttpGet("{orderId:int}")]
        [ProducesResponseType(typeof(Order), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult> GetOrderAsync(int orderId)
        {
            try
            {
                //Todo: It's good idea to take advantage of GetOrderByIdQuery and handle by GetCustomerByIdQueryHandler
                //var order customer = await _mediator.Send(new GetOrderByIdQuery(orderId));
                var order = await _orderQueries.GetOrderAsync(orderId);

                return Ok(order);
            }
            catch
            {
                return NotFound();
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<OrderSummary>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<IEnumerable<OrderSummary>>> GetOrdersAsync()
        {
            var userid = _identityService.GetUserIdentity();
            var orders = await _orderQueries.GetOrdersFromUserAsync(Guid.Parse(userid));

            return Ok(orders);
        }

        [HttpPut("cancel")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CancelOrderAsync([FromBody] CancelOrderCommand command, [FromHeader(Name = "x-requestid")] string requestId)
        {
            bool commandResult = false;

            if (Guid.TryParse(requestId, out Guid guid) && guid != Guid.Empty)
            {
                var requestCancelOrder = new IdentifiedCommand<CancelOrderCommand, bool>(command, guid);

                _logger.LogInformation(
                    "----- Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                    requestCancelOrder.GetGenericTypeName(),
                    nameof(requestCancelOrder.Command.OrderNumber),
                    requestCancelOrder.Command.OrderNumber,
                    requestCancelOrder);

                //commandResult = await _mediator.Send(requestCancelOrder);
            }

            //if (!commandResult)
            //{
            //    return BadRequest();
            //}

            return Ok();
        }
    }
}