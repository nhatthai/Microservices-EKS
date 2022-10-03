using Microsoft.EntityFrameworkCore;
using NET6.Microservice.Order.API.Infastructure.Repositories;
using NET6.Microservice.Order.API.Models;

namespace NET6.Microservice.Order.API.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderingContext _context;

    //public IUnitOfWork UnitOfWork => _context;

    public OrderRepository(OrderingContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public NET6.Microservice.Order.API.Models.Order Add(NET6.Microservice.Order.API.Models.Order order)
    {
        return _context.Orders.Add(order).Entity;

    }

    public async Task<NET6.Microservice.Order.API.Models.Order> GetAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(x => x.Address)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            order = _context
                        .Orders
                        .Local
                        .FirstOrDefault(o => o.Id == orderId);
        }
        if (order != null)
        {
            await _context.Entry(order)
                .Collection(i => i.OrderItems).LoadAsync();
            await _context.Entry(order)
                .Reference(i => i.OrderStatus).LoadAsync();
        }

        return order;
    }

    public void Update(NET6.Microservice.Order.API.Models.Order order)
    {
        _context.Entry(order).State = EntityState.Modified;
    }
}
