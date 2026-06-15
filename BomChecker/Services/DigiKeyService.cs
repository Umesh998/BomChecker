using BomChecker.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BomChecker.Services
{
    public class DigiKeyService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private string _cachedToken = "";
        private DateTime _tokenExpiry = DateTime.MinValue;

        public DigiKeyService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        private async Task<string> GetAccessToken()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.Now < _tokenExpiry)
                return _cachedToken;

            var clientId = _config["DigiKey:ClientId"];
            var clientSecret = _config["DigiKey:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                throw new Exception("DigiKey ClientId or ClientSecret missing from appsettings.json");

            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api.digikey.com/v1/oauth2/token");

            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type",    "client_credentials"),
                new KeyValuePair<string, string>("client_id",     clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret)
            });

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"DigiKey auth failed: {response.StatusCode}");

            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;

            _cachedToken = root.GetProperty("access_token").GetString() ?? "";
            _tokenExpiry = DateTime.Now.AddSeconds(
                root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() - 60 : 3540);

            return _cachedToken;
        }

        public async Task<PartDetails?> GetPartDetails(string mpn)
        {
            try
            {
                var token = await GetAccessToken();

                var requestBody = new
                {
                    keywords = mpn.Trim(),
                    limit = 1,
                    offset = 0,
                    filterOptionsRequest = new { manufacturerFilter = Array.Empty<object>() }
                };

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.digikey.com/products/v4/search/keyword");

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Add("X-DIGIKEY-Client-Id", _config["DigiKey:ClientId"]);
                request.Headers.Add("X-DIGIKEY-Locale-Site", "US");
                request.Headers.Add("X-DIGIKEY-Locale-Language", "en");
                request.Headers.Add("X-DIGIKEY-Locale-Currency", "USD");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Products", out var products) ||
                    products.ValueKind == JsonValueKind.Null ||
                    products.GetArrayLength() == 0)
                    return null;

                var part = products[0];
                var specs = ExtractSpecs(part, "Parameters", "ParameterText", "ValueText");

                return new PartDetails
                {
                    Mpn = GetString(part, "ManufacturerProductNumber") ?? mpn,
                    Description = GetNested(part, "Description", "ProductDescription"),
                    Manufacturer = GetNested(part, "Manufacturer", "Name"),
                    Category = GetNested(part, "Category", "Name"),
                    DatasheetUrl = GetString(part, "DatasheetUrl") ?? "",
                    ProductUrl = GetString(part, "ProductUrl") ?? "",
                    Stock = part.TryGetProperty("QuantityAvailable", out var qty)
                        ? qty.GetInt64().ToString() : "0",
                    Specs = specs,
                    Source = "DigiKey"
                };
            }
            catch { return null; }
        }

        private static Dictionary<string, string> ExtractSpecs(
            JsonElement part, string arrayProp, string keyProp, string valProp)
        {
            var specs = new Dictionary<string, string>();
            if (!part.TryGetProperty(arrayProp, out var arr) ||
                arr.ValueKind != JsonValueKind.Array) return specs;

            foreach (var item in arr.EnumerateArray())
            {
                var k = item.TryGetProperty(keyProp, out var kp) ? kp.GetString() : null;
                var v = item.TryGetProperty(valProp, out var vp) ? vp.GetString() : null;
                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    specs[k] = v;
            }
            return specs;
        }

        private static string GetNested(JsonElement el, string prop, string subprop)
        {
            if (el.TryGetProperty(prop, out var outer) && outer.ValueKind != JsonValueKind.Null)
                if (outer.TryGetProperty(subprop, out var inner))
                    return inner.GetString() ?? "";
            return "";
        }

        private static string? GetString(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null
               ? v.GetString() : null;
    }
}
