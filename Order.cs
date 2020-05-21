namespace Delbot
{
    public class Order
    {
        public OrderDetails OrderDetails { get; set; }
        public string PayPalId { get; set; }
        public ulong AdminId { get; set; }

        public Order(OrderDetails details, string paypal_id, ulong admin_id)
        {
            OrderDetails = details;
            PayPalId = paypal_id;
            AdminId = admin_id;
        }
    }
}