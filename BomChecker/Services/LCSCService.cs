using BomChecker.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BomChecker.Services
{
    public class LCSCService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private const string BaseUrl = "https://api.lcsc.com/openapi/v1/products/search";

        public LCSCService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.lcsc.com/");
        }

        public async Task<PartDetails?> GetPartDetails(string mpn)
        {
            try
            {
                var key = _config["LCSC:ApiKey"];
                var secret = _config["LCSC:ApiSecret"];

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                    return null;

                var nonce = Guid.NewGuid().ToString("N")[..16];
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                var rawSig = $"key={key}&nonce={nonce}&secret={secret}&timestamp={timestamp}";

                using var sha1 = SHA1.Create();
                var sig = BitConverter.ToString(
                    sha1.ComputeHash(Encoding.UTF8.GetBytes(rawSig)))
                    .Replace("-", "").ToLower();

                var url = $"{BaseUrl}?key={Uri.EscapeDataString(key)}" +
                          $"&nonce={nonce}&timestamp={timestamp}&sign={sig}" +
                          $"&keyword={Uri.EscapeDataString(mpn)}&match_type=fuzzy&page_size=1";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                    return null;

                if (!root.TryGetProperty("result", out var resultObj)) return null;

                JsonElement list = default;
                if (resultObj.TryGetProperty("productList", out var pl)) list = pl;
                else if (resultObj.TryGetProperty("list", out var l)) list = l;
                else if (resultObj.ValueKind == JsonValueKind.Array) list = resultObj;

                if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                    return null;

                var item = list[0];
                var pkg = GetStr(item, "encapStandard", "package", "casePackage");
                var desc = GetStr(item, "productDescEn", "description", "productDesc");
                var lcscPart = GetStr(item, "productCode", "lcscPart", "productNumber");

                return new PartDetails
                {
                    Mpn = GetStr(item, "productModel", "mfcPart", "mpn"),
                    Description = desc,
                    Manufacturer = GetStr(item, "brandNameEn", "manufacturer", "brandName"),
                    Category = GetStr(item, "catalogName", "category", "categoryName"),
                    Package = pkg,
                    ProductUrl = string.IsNullOrEmpty(lcscPart)
                        ? "" : $"https://www.lcsc.com/product-detail/{lcscPart}.html",
                    Stock = GetStr(item, "stockNumber", "stock", "qty"),
                    Specs = new Dictionary<string, string>
                    {
                        ["Package"] = pkg,
                        ["LCSC Part No"] = lcscPart
                    },
                    Source = "LCSC"
                };
            }
            catch { return null; }
        }

        private static string GetStr(JsonElement el, params string[] keys)
        {
            foreach (var key in keys)
                if (el.TryGetProperty(key, out var val) && val.ValueKind != JsonValueKind.Null)
                    return val.ToString();
            return "";
        }
    }
}
