using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;

namespace Delbot
{
    public class PayPalClient
    {
        private const int EXPIRE_THRESHOLD = 60;
        
        private const string OAUTH_RESOURCE = "/v1/oauth2/token";
        private const string CREATE_ORDER_RESOURCE = "/v2/checkout/orders";
        
#if DEBUG
        private const string PAYPAL_URL = "https://api.sandbox.paypal.com";
#else
		private const string PAYPAL_URL = "https://api.paypal.com";
#endif

        private string client_id;
        private string client_secret;
        
        private string access_token;
        private DateTime access_expire;

        public PayPalClient(string client_id, string client_secret)
        {
            this.client_id = client_id;
            this.client_secret = client_secret;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (access_token != null && DateTime.Now < access_expire)
            {
                return access_token;
            }

            var client = new RestClient(PAYPAL_URL);
            client.Authenticator = new HttpBasicAuthenticator(client_id, client_secret);

            var request = new RestRequest(OAUTH_RESOURCE, Method.POST);
            request.AddParameter("grant_type", "client_credentials");

            var response = await client.ExecuteAsync(request);
            PayPalTokenModel token_model = JsonConvert.DeserializeObject<PayPalTokenModel>(response.Content);

            access_expire = DateTime.Now + new TimeSpan(0, 0, 0, token_model.expires_in - EXPIRE_THRESHOLD);
            access_token = token_model.access_token;

            return access_token;
        }

        public async Task<Order> CreateOrderAsync(OrderDetails details, ulong admin_id)
        {
	        var model = new CreateOrderModel
	        {
		        intent = "CAPTURE",
				
		        purchase_units =
			        new [] {
				        new PurchaseUnits
				        {
					        amount = new AmountWithBreakdown
					        {
						        currency_code = "USD",
						        value = details.Price.ToString("0.00"),
						        breakdown = new Breakdown
						        {
							        item_total = new Money
							        {
								        currency_code = "USD",
								        value = details.Price.ToString("0.00")
							        }
						        }
					        },
						
					        items = new []
					        {
						        new Item
						        {
							        name = details.Amount + " " + details.ProductName,
							        quantity = "1",
							        unit_amount = new Money
							        {
								        currency_code = "USD",
								        value = details.Price.ToString("0.00")
							        }
						        }
					        }
				        }
			        },
				
		        application_context =
		        {
			        shipping_preference = "NO_SHIPPING",
			        return_url = "https://earlh21.github.io/delsbells/thankyou"
		        }
	        };
	        
	        var client = new RestClient(PAYPAL_URL);

	        var request = new RestRequest(CREATE_ORDER_RESOURCE, Method.POST);
	        request.AddHeader("Authorization", "Bearer " + await GetAccessTokenAsync());
	        request.AddHeader("Content-Type", "application/json");
	        request.AddJsonBody(JsonConvert.SerializeObject(model));

	        var response = await client.ExecuteAsync(request);

	        if (response.IsSuccessful)
	        {
		        CreateOrderResponse order_response = JsonConvert.DeserializeObject<CreateOrderResponse>(response.Content);
		        Order order = new Order(details, order_response.id, admin_id);
		        return order;
	        }

	        return null;
        }

        public async Task<CaptureOrderResponse> CaptureOrderAsync(Order order)
        {
	        var client = new RestClient(PAYPAL_URL);

	        var request = new RestRequest("/v2/checkout/orders/" + order.PayPalId + "/capture", Method.POST);
	        request.AddHeader("Authorization", "Bearer " + await GetAccessTokenAsync());
	        request.AddHeader("Content-Type", "application/json");

	        var response = await client.ExecuteAsync(request);
			
	        if (response.IsSuccessful)
	        {
		        CaptureOrderResponse order_response = JsonConvert.DeserializeObject<CaptureOrderResponse>(response.Content);
		        order_response.successful = true;
		        order_response.raw_content = response.Content;
		        return order_response;
	        }

	        return new CaptureOrderResponse
	        {
		        raw_content = response.Content,
		        successful = false
	        };
        }

        public struct CaptureOrderResponse
		{
			public string raw_content;
			public string status;
			public bool successful;
		}

		public struct CreateOrderResponse
		{
			public string raw_content;
			public string id;
			public LinkDescription[] links;
			public bool successful;
		}

		public struct LinkDescription
		{
			public string href;
			public string rel;
		}

		private struct CreateOrderModel
		{
			public string intent;
			public PurchaseUnits[] purchase_units;
			public ApplicationContext application_context;
		}

		private struct Item
		{
			public string name;
			public Money unit_amount;
			public string quantity;
		}

		private struct Money
		{
			public string currency_code;
			public string value;
		}

		private struct PurchaseUnits
		{
			public AmountWithBreakdown amount;
			public Item[] items;
		}

		private struct AmountWithBreakdown
		{
			public string currency_code;
			public string value;
			public Breakdown breakdown;
		}

		private struct Breakdown
		{
			public Money item_total;
		}

		private struct ApplicationContext
		{
			public string shipping_preference;
			public string return_url;
		}
		
		private struct PayPalTokenModel
		{
			public string scope { get; set; }
			public string nonce { get; set; }
			public string access_token { get; set; }
			public string token_type { get; set; }
			public string app_id { get; set; }
			public int expires_in { get; set; }
		}
    }
}