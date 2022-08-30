using System.ComponentModel.DataAnnotations;

namespace NET6.Microservice.Order.WebAPI.Models.Requests
{
    public class OrderRequest
    {
        [Required]
        public double OrderAmount { get; set; }

        [Required]
        public string OrderNumber { get; set; }
    }
}
