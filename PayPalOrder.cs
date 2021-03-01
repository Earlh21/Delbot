namespace Delbot
{
    public class PayPalOrder
    {
        public OrderDetails OrderDetails { get; set; }
        public string PayPalId { get; set; }
        public ulong AdminId { get; set; }
        public string ApprovalLink { get; set; }

        public PayPalOrder(OrderDetails details, string paypal_id, ulong admin_id, string approval_link)
        {
            OrderDetails = details;
            PayPalId = paypal_id;
            ApprovalLink = approval_link;
            AdminId = admin_id;
        }
    }
}