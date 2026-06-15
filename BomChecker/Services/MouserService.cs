using BomChecker.Models;
using System.Text;
using System.Text.Json;

namespace BomChecker.Services
{
    public class MouserService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public MouserService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<PartDetails?> GetPartDetails(string mpn)
        {
            try
            {
                var apiKey = _config["Mouser:ApiKey"];
                var url = $"https://api.mouser.com/api/v1/search/keyword?apiKey={apiKey}";

                var requestBody = new
                {
                    SearchByKeywordRequest = new
                    {
                        keyword = mpn.Trim(),
                        records = 1,
                        startingRecord = 0,
                        searchOptions = "Exact"
                    }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("Errors", out var errors) &&
                    errors.GetArrayLength() > 0) return null;

                var parts = root
                    .GetProperty("SearchResults")
                    .GetProperty("Parts");

                if (parts.ValueKind == JsonValueKind.Null ||
                    parts.GetArrayLength() == 0) return null;

                var part = parts[0];
                var specs = new Dictionary<string, string>();

                if (part.TryGetProperty("ProductAttributes", out var attrs) &&
                    attrs.ValueKind != JsonValueKind.Null)
                {
                    foreach (var attr in attrs.EnumerateArray())
                    {
                        var k = attr.TryGetProperty("AttributeName", out var n)
                            ? n.GetString() : null;
                        var v = attr.TryGetProperty("AttributeValue", out var av)
                            ? av.GetString() : null;
                        if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                            specs[k] = v;
                    }
                }

                var category = part.TryGetProperty("Category", out var cat)
                    ? cat.GetString() ?? "" : "";

                // Enrich specs with category for better MSL inference
                if (!string.IsNullOrEmpty(category))
                    specs["Category"] = category;

                // Extract package from multiple possible keys
                var pkg = specs.TryGetValue("Case/Package", out var p1) ? p1 :
                          specs.TryGetValue("Packaging", out var p2) ? p2 :
                          specs.TryGetValue("Package", out var p3) ? p3 :
                          specs.TryGetValue("Case", out var p4) ? p4 : "";

                if (!string.IsNullOrEmpty(pkg))
                    specs["Package"] = pkg;

                var desc = part.TryGetProperty("Description", out var d)
                    ? d.GetString() ?? "" : "";

                return new PartDetails
                {
                    Mpn = part.TryGetProperty("ManufacturerPartNumber", out var m)
                        ? m.GetString() ?? mpn : mpn,
                    Description = desc,
                    Manufacturer = part.TryGetProperty("Manufacturer", out var mfr)
                        ? mfr.GetString() ?? "" : "",
                    Category = category,
                    DatasheetUrl = part.TryGetProperty("DataSheetUrl", out var ds)
                        ? ds.GetString() ?? "" : "",
                    ProductUrl = part.TryGetProperty("ProductDetailUrl", out var pu)
                        ? pu.GetString() ?? "" : "",
                    Stock = part.TryGetProperty("Availability", out var av2)
                        ? av2.GetString() ?? "" : "",
                    Specs = specs,
                    Source = "Mouser"
                };
            }
            catch { return null; }
        }
    }
}
