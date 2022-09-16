using NET6.Microservice.Basket.API.Models;

namespace NET6.Microservice.Basket.API.Infrastructure.Repositories;

public interface IBasketRepository
{
    Task<CustomerBasket> GetBasketAsync(string customerId);

    IEnumerable<string> GetUsers();

    Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket);

    Task<bool> DeleteBasketAsync(string id);
}
