using System;
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
        
        /// <summary>
		/// Creates an order through PayPal and returns PayPal's response.
		/// </summary>
		/// <param name="amount">Amount of bells in millions.</param>
		/// <param name="price">Total price of the order.</param>
		/// <returns>PayPal's response, containing the order ID.</returns>
		public async Task<CreateOrderResponse> CreateOrderAsync(int amount, decimal price)
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
							value = price.ToString("0.00"),
							breakdown = new Breakdown
							{
								item_total = new Money
								{
									currency_code = "USD",
									value = price.ToString("0.00")
								}
							}
						},
						
						items = new []
						{
							new Item
							{
								name = amount + " million ACNH Bells",
								quantity = "1",
								unit_amount = new Money
								{
									currency_code = "USD",
									value = price.ToString("0.00")
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
				order_response.successful = true;
				order_response.raw_content = response.Content;
				return order_response;
			}

			return new CreateOrderResponse
			{
				raw_content = response.Content,
				successful = false
			};
		}

		/// <summary>
		/// Captures an approved order's payment.
		/// </summary>
		/// <param name="order_id">ID of the order to capture.</param>
		/// <returns>PayPal's response.</returns>
		public async Task<CaptureOrderResponse> CaptureOrderAsync(string order_id)
		{
			var client = new RestClient(PAYPAL_URL);

			var request = new RestRequest("/v2/checkout/orders/" + order_id + "/capture", Method.POST);
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