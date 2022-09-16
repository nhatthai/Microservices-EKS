using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NET6.Microservice.Basket.API.Infrastructure.Repositories;
using NET6.Microservice.Basket.API.Models;
using System.Net;

namespace NET6.Microservice.Basket.API.Controllers;

[Route("api/v1/[controller]")]
//[Authorize]
[ApiController]
public class BasketController : ControllerBase
{
    private readonly IBasketRepository _repository;
    private readonly ILogger<BasketController> _logger;

    public BasketController(
        ILogger<BasketController> logger, IBasketRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerBasket), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<CustomerBasket>> GetBasketByIdAsync(string id)
    {
        _logger.LogInformation("Get Basket by Id {Id}", id);

        var basket = await _repository.GetBasketAsync(id);

        return Ok(basket ?? new CustomerBasket(id));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerBasket), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<CustomerBasket>> UpdateBasketAsync([FromBody] CustomerBasket value)
    {
        _logger.LogInformation("Update Basket {BuyerId}", value.BuyerId);

        return Ok(await _repository.UpdateBasketAsync(value));
    }


    [Route("checkout")]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult> CheckoutAsync(
        [FromBody]BasketCheckout basketCheckout, [FromHeader(Name = "x-requestid")] string requestId)
    {
        // var userId = _identityService.GetUserIdentity();
        _logger.LogInformation("Checkout Basket {Buyer} {RequestId} {CardHolderName}", basketCheckout.Buyer, requestId, basketCheckout.CardHolderName);

        basketCheckout.RequestId = (Guid.TryParse(requestId, out Guid guid) && guid != Guid.Empty) ?
            guid : basketCheckout.RequestId;

        var basket = await _repository.GetBasketAsync("User1");

        if (basket == null)
        {
            return BadRequest();
        }

        // var userName = User.FindFirst(x => x.Type == "unique_name").Value;

        // var eventMessage = new UserCheckoutAcceptedIntegrationEvent(
        //     userId, userName, basketCheckout.City, basketCheckout.Street,
        //     basketCheckout.State, basketCheckout.Country, basketCheckout.ZipCode,
        //     basketCheckout.CardNumber, basketCheckout.CardHolderName,
        //     basketCheckout.CardExpiration, basketCheckout.CardSecurityNumber, basketCheckout.CardTypeId,
        //     basketCheckout.Buyer, basketCheckout.RequestId, basket);

        // // Once basket is checkout, sends an integration event to
        // // ordering.api to convert basket to order and proceeds with
        // // order creation process
        // try
        // {
        //     _logger.LogInformation(
        //         "----- Publishing integration event: {IntegrationEventId} from {AppName} - ({@IntegrationEvent})",
        //         eventMessage.Id, Program.AppName, eventMessage);

        //     _eventBus.Publish(eventMessage);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(
        //         ex, "ERROR Publishing integration event: {IntegrationEventId} from {AppName}",
        //         eventMessage.Id, Program.AppName);

        //     throw;
        // }

        return Accepted();
    }

}
