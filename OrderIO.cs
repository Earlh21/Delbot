using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

namespace Delbot
{
    public static class OrderIO
    {
        private const string VERIFY_PATTERN = "[A-Za-z0-9:]+";

        public static readonly string[] REQUIRED_ORDER_PARAMETERS =
        {
            "in game name",
            "island name",
            "paypal name",
            "timezone"
        };

        public static readonly string[] PRODUCT_TYPES =
        {
            "bells",
            "tickets"
        };

        public static readonly Dictionary<string, string> PRODUCT_DISPLAY_NAMES =
            new Dictionary<string, string>
            {
                {"bells", "ACNH Bells (millions)"},
                {"tickets", "Nook Mile Tickets"}
            };

        public static readonly Dictionary<string, Dictionary<int, decimal>> PRICES =
            new Dictionary<string, Dictionary<int, decimal>>
            {
                {"bells", new Dictionary<int, decimal>
                {
                    {3, 4.99m},
                    {6, 7.49m},
                    {10, 11.49m}
                }},
            };

#if DEBUG
        private static readonly string CURRENT_ORDERS_DIR =
            Environment.GetEnvironmentVariable("HOME") + "/debug_active_orders/";
#else
		private static readonly string CURRENT_ORDERS_DIR =
			Environment.GetEnvironmentVariable("HOME") + "/active_orders/";
#endif

        public static OrderDetails ParseMessage(IMessage message)
        {
            string content = message.Content;
            Regex verify_regex = new Regex(VERIFY_PATTERN);
            Match match = verify_regex.Match(content);

            //Verify that the message only contains allowed characters
            if (match == null)
            {
                throw new ArgumentException("Invalid order request: character '" + content[0] +
                                            "' not allowed.");
            }

            if (match.Length != content.Length)
            {
                throw new ArgumentException("Invalid order request: character '" + content[match.Length] +
                                            "' not allowed.");
            }

            //Parse the message into a dictionary
            string[] lines = message.Content.Split("\n");
            Dictionary<string, string> order_details = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                if (line.IndexOf(":") == -1)
                {
                    throw new ArgumentException(
                        "Invalid order request: colon required to separate parameter name and value\n" +
                        "\tat " + line);
                }

                string[] line_data = line.Split(":");
                order_details[line_data[0].ToLower().Trim()] = line_data[1].ToLower().Trim();
            }

            //Make sure all required parameters are present
            foreach (string required_parameter in REQUIRED_ORDER_PARAMETERS)
            {
                if (!order_details.ContainsKey(required_parameter))
                {
                    throw new ArgumentException("Invalid order request: parameter " + '\"' +
                                                required_parameter + '\"' +
                                                " required.");
                }

                if (String.IsNullOrWhiteSpace(order_details[required_parameter]))
                {
                    throw new ArgumentException("Invalid order request: parameter " + '\"' +
                                                required_parameter + '\"' + " cannot be empty");
                }
            }

            string product_name = "";
            int product_type_count = 0;
            foreach (string product_type in PRODUCT_TYPES)
            {
                if (order_details.ContainsKey(product_type))
                {
                    product_type_count++;
                    product_name = product_type;
                }
            }
            
            //Make sure exactly one product was ordered
            if (product_type_count < 1)
            {
                //TODO: Don't hardcode the product types
                throw new ArgumentException(
                    "Invalid order request: no product was ordered. Select from [bells, tickets]");
            }

            if (product_type_count > 1)
            {
                throw new ArgumentException(
                    "Invalid order request: ambiguous order. Select only one product type from [bells, tickets]");
            }

            int amount;
            decimal price;
            string in_game_name;
            ulong discord_id = message.Author.Id;
            ulong message_id = message.Id;
            string island_name;
            string paypal_name;

            try
            {
                amount = Convert.ToInt32(order_details["bell bundle"]);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid order request: bell bundle must be a number");
            }

            try
            {
                price = PRICES[product_name][amount];
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException("Invalid order request: requested bell bundle is not supported");
            }

            in_game_name = order_details["in game name"];
            island_name = order_details["island name"];
            paypal_name = order_details["paypal name"];

            return new OrderDetails(amount, price, in_game_name, discord_id, message_id, island_name,
                paypal_name, product_name);
        }

        public static async void SaveOrderAsync(PayPalOrder order)
        {
            //TODO: Replace this mess with serialization
            List<string> parameters = new List<string>();
            parameters.Add("Amount:" + order.OrderDetails.Amount);
            parameters.Add("Price:" + order.OrderDetails.Price);
            parameters.Add("InGameName:" + order.OrderDetails.InGameName);
            parameters.Add("UserId:" + order.OrderDetails.UserId);
            parameters.Add("MessageId:" + order.OrderDetails.MessageId);
            parameters.Add("IslandName:" + order.OrderDetails.IslandName);
            parameters.Add("PayPalName:" + order.OrderDetails.PayPalName);
            parameters.Add("ProductName:" + order.OrderDetails.ProductName);
            parameters.Add("PayPalId:" + order.PayPalId);
            parameters.Add("AdminId:" + order.AdminId);
            parameters.Add("ApprovalLink:" + order.ApprovalLink);

            string file_path = CURRENT_ORDERS_DIR + order.PayPalId + ".txt";

            await File.WriteAllLinesAsync(file_path, parameters);
        }

        public static void RemoveOrder(PayPalOrder order)
        {
            string file_path = CURRENT_ORDERS_DIR + order.PayPalId + ".txt";
            File.Delete(file_path);
        }

        public static async Task<PayPalOrder> GetOrderAsync(string order_id)
        {
            string file_path = CURRENT_ORDERS_DIR + order_id + ".txt";
            if (!File.Exists(file_path))
            {
                return null;
            }

            string[] contents = await File.ReadAllLinesAsync(file_path);
            
            Dictionary<string, string> data = new Dictionary<string, string>();
            foreach (string datum in contents)
            {
                string[] split_datum = datum.Split(":");
                data[split_datum[0]] = split_datum[1];
            }

            int amount = Convert.ToInt32(data["Amount"]);
            decimal price = Convert.ToDecimal(data["Price"]);
            string in_game_name = data["InGameName"];
            ulong user_id = Convert.ToUInt64(data["UserId"]);
            ulong message_id = Convert.ToUInt64(data["MessageId"]);
            string island_name = data["IslandName"];
            string paypal_name = data["PayPalName"];
            string product_name = data["ProdutName"];
            string paypal_id = data["PayPalId"];
            ulong admin_id = Convert.ToUInt64(data["AdminId"]);
            string approval_link = data["ApprovalLink"];
            
            OrderDetails order_details = new OrderDetails(amount, price, in_game_name, user_id, message_id, island_name, paypal_name, product_name);
            return new PayPalOrder(order_details, paypal_id, admin_id, approval_link);
        }
    }
}