
using NET6.Microservice.Order.API.Models;

namespace NET6.Microservice.Order.API.Infastructure.Repositories;

public interface IOrderRepository
{
    NET6.Microservice.Order.API.Models.Order Add(NET6.Microservice.Order.API.Models.Order order);

    void Update(NET6.Microservice.Order.API.Models.Order order);

    Task<NET6.Microservice.Order.API.Models.Order> GetAsync(int orderId);
}