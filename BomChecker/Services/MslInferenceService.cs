//using System.Text;
//using System.Text.Json;

//namespace BomChecker.Services
//{
//    public class MslInferenceService
//    {
//        private readonly IHttpClientFactory _httpClientFactory;
//        private readonly IConfiguration _config;

//        public MslInferenceService(IHttpClientFactory httpClientFactory, IConfiguration config)
//        {
//            _httpClientFactory = httpClientFactory;
//            _config = config;
//        }


//        public async Task<string> InferMsl(
//            string partNumber, string description, string package)
//        {
//            try
//            {
//                var apiKey = _config["Anthropic:ApiKey"];
//                if (string.IsNullOrWhiteSpace(apiKey)) return "N/A";

//                var prompt = $"""
//                    You are an electronics component expert specializing in IPC/JEDEC J-STD-020.

//                    Part Number : {partNumber}
//                    Description : {description}
//                    Package     : {package}

//                    MSL 1 (Unlimited) — default for small/simple parts:
//                    - ALL through-hole parts (DIP, SIP, TO-92, TO-220, TO-247, TO-263, axial, radial)
//                    - Passive components: resistors, capacitors, inductors (0201/0402/0603/0805/1206/1210/2512)
//                    - Small signal transistors and diodes: SOT-23, SOT-323, SOT-523, SC-70, SC-88, SOD-123, SOD-323
//                    - Standard logic ICs, op-amps, comparators in SOIC-8, SOIC-14, SOIC-16, SOT-23-5, SOT-23-6
//                    - Linear regulators (LDO): SOT-23, SOT-89, DPAK, D2PAK, TO-252, TO-263
//                    - Simple MOSFETs and BJTs: SOT-23, SOT-223, DPAK, TO-252
//                    - LEDs, crystals, oscillators, fuses, connectors, transformers

//                    MSL 2 (1 Year):
//                    - Standard MCUs in TSSOP, SSOP, SOIC-28, SOIC-32
//                    - Small QFN packages (≤32 pins, body ≤5x5mm)
//                    - Op-amps and analog ICs in TSSOP, MSOP
//                    - EEPROMs, small memory ICs in SOIC/TSSOP

//                    MSL 2a (4 Weeks):
//                    - Only when datasheet explicitly states MSL 2a

//                    MSL 3 (168 Hours):
//                    - Larger QFN (>32 pins or body >5x5mm)
//                    - QFP, LQFP (any pin count)
//                    - Large MCUs and FPGAs in QFP/LQFP
//                    - DDR memory in TSOP

//                    MSL 4+ :
//                    - BGA, LGA, WLCSP packages only

//                    CRITICAL: When in doubt, choose the LOWER level.
//                    SOT-23, SC-70, SOIC-8 and all small SMD passives = always MSL 1 (Unlimited).

//                    Reply with ONLY one of these exact values — nothing else:
//                    MSL 1 (Unlimited), MSL 2 (1 Year), MSL 2a (4 Weeks), MSL 3 (168 Hours),
//                    MSL 4 (72 Hours), MSL 5 (48 Hours), MSL 5a (24 Hours), MSL 6 (TOL), N/A
//                    """;

//                var requestBody = new
//                {
//                    model = "claude-sonnet-4-5",
//                    max_tokens = 20,
//                    messages = new[] { new { role = "user", content = prompt } }
//                };

//                var client = _httpClientFactory.CreateClient();
//                var request = new HttpRequestMessage(HttpMethod.Post,
//                    "https://api.anthropic.com/v1/messages");
//                request.Headers.Add("x-api-key", apiKey);
//                request.Headers.Add("anthropic-version", "2023-06-01");
//                request.Content = new StringContent(
//                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

//                var response = await client.SendAsync(request);
//                if (!response.IsSuccessStatusCode) return "N/A";

//                var content = await response.Content.ReadAsStringAsync();
//                using var doc = JsonDocument.Parse(content);
//                var text = doc.RootElement
//                    .GetProperty("content")[0]
//                    .GetProperty("text")
//                    .GetString()?.Trim();

//                var valid = new[]
//                {
//                    "MSL 1 (Unlimited)", "MSL 2 (1 Year)", "MSL 2a (4 Weeks)",
//                    "MSL 3 (168 Hours)", "MSL 4 (72 Hours)", "MSL 5 (48 Hours)",
//                    "MSL 5a (24 Hours)", "MSL 6 (TOL)", "N/A"
//                };

//                return valid.Contains(text) ? text! : "N/A";
//            }
//            //catch { return "N/A"; }

//            catch (Exception ex)
//            {
//                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE ERROR] {ex.Message}");
//                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE ERROR] {ex.StackTrace}");
//                return "N/A";
//            }
//        }

//        public string ExtractMountType(Dictionary<string, string> specs, string description)
//        {
//            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);

//            string mountType = "N/A";
//            string[] mountKeys = { "Mounting Type", "Mounting Style", "Mount Type", "Mounting" };
//            foreach (var key in mountKeys)
//                if (specsCI.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
//                { mountType = v; break; }

//            if (mountType != "N/A")
//            {
//                var mt = mountType.ToLower();
//                if (mt.Contains("surface") || mt.Contains("smt") || mt.Contains("smd"))
//                    return "SMT";
//                if (mt.Contains("through") || mt.Contains("thru"))
//                    return "Through-Hole";
//            }

//            var pkg = (specsCI.GetValueOrDefault("Package / Case", "") +
//                       specsCI.GetValueOrDefault("Supplier Device Package", "") +
//                       specsCI.GetValueOrDefault("Case/Package", "") +
//                       specsCI.GetValueOrDefault("Package", "")).ToLower();

//            if (pkg.Contains("soic") || pkg.Contains("qfp") || pkg.Contains("qfn") ||
//                pkg.Contains("sot-") || pkg.Contains("tssop") || pkg.Contains("bga") ||
//                pkg.Contains("dfn") || pkg.Contains("lqfp") || pkg.Contains("msop") ||
//                pkg.Contains("wlcsp") || pkg.Contains("0201") || pkg.Contains("0402") ||
//                pkg.Contains("0603") || pkg.Contains("0805") || pkg.Contains("1206") ||
//                pkg.Contains("smd") || pkg.Contains("sc-70") || pkg.Contains("sc-88"))
//                return "SMT";

//            if (pkg.Contains("dip") || pkg.Contains("to-92") ||
//                pkg.Contains("to-220") || pkg.Contains("to-247") ||
//                pkg.Contains("axial") || pkg.Contains("radial") ||
//                pkg.Contains("through"))
//                return "Through-Hole";

//            return "N/A";
//        }

//        public string ExtractPackage(Dictionary<string, string> specs)
//        {
//            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);
//            return specsCI.TryGetValue("Package / Case", out var p1) ? p1 :
//                   specsCI.TryGetValue("Supplier Device Package", out var p2) ? p2 :
//                   specsCI.TryGetValue("Case/Package", out var p3) ? p3 :
//                   specsCI.TryGetValue("Packaging", out var p4) ? p4 :
//                   specsCI.TryGetValue("Package", out var p5) ? p5 :
//                   specsCI.TryGetValue("Case", out var p6) ? p6 : "N/A";
//        }
//    }
//}













//using System.Text;
//using System.Text.Json;

//namespace BomChecker.Services
//{
//    public class MslInferenceService
//    {
//        private readonly IHttpClientFactory _httpClientFactory;
//        private readonly IConfiguration _config;

//        // Limit to 1 concurrent Claude call to avoid 429 rate limit
//        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(1, 1);

//        public MslInferenceService(IHttpClientFactory httpClientFactory, IConfiguration config)
//        {
//            _httpClientFactory = httpClientFactory;
//            _config = config;
//        }

//        public async Task<string> InferMsl(
//            string partNumber, string description, string package)
//        {
//            await _throttle.WaitAsync();
//            try
//            {
//                var apiKey = _config["Anthropic:ApiKey"];
//                if (string.IsNullOrWhiteSpace(apiKey))
//                {
//                    _throttle.Release();
//                    return "N/A";
//                }

//                var prompt = $"""
//                    You are an electronics component expert specializing in IPC/JEDEC J-STD-020.

//                    Part Number : {partNumber}
//                    Description : {description}
//                    Package     : {package}

//                    MSL 1 (Unlimited) — default for small/simple parts:
//                    - ALL through-hole parts (DIP, SIP, TO-92, TO-220, TO-247, TO-263, axial, radial)
//                    - Passive components: resistors, capacitors, inductors (0201/0402/0603/0805/1206/1210/2512)
//                    - Small signal transistors and diodes: SOT-23, SOT-323, SOT-523, SC-70, SC-88, SOD-123, SOD-323
//                    - Standard logic ICs, op-amps, comparators in SOIC-8, SOIC-14, SOIC-16, SOT-23-5, SOT-23-6
//                    - Linear regulators (LDO): SOT-23, SOT-89, DPAK, D2PAK, TO-252, TO-263
//                    - Simple MOSFETs and BJTs: SOT-23, SOT-223, DPAK, TO-252
//                    - LEDs, crystals, oscillators, fuses, connectors, transformers

//                    MSL 2 (1 Year):
//                    - Standard MCUs in TSSOP, SSOP, SOIC-28, SOIC-32
//                    - Small QFN packages (≤32 pins, body ≤5x5mm)
//                    - Op-amps and analog ICs in TSSOP, MSOP
//                    - EEPROMs, small memory ICs in SOIC/TSSOP

//                    MSL 2a (4 Weeks):
//                    - Only when datasheet explicitly states MSL 2a

//                    MSL 3 (168 Hours):
//                    - Larger QFN (>32 pins or body >5x5mm)
//                    - QFP, LQFP (any pin count)
//                    - Large MCUs and FPGAs in QFP/LQFP
//                    - DDR memory in TSOP

//                    MSL 4+ :
//                    - BGA, LGA, WLCSP packages only

//                    CRITICAL: When in doubt, choose the LOWER level.
//                    SOT-23, SC-70, SOIC-8 and all small SMD passives = always MSL 1 (Unlimited).

//                    Reply with ONLY one of these exact values — nothing else:
//                    MSL 1 (Unlimited), MSL 2 (1 Year), MSL 2a (4 Weeks), MSL 3 (168 Hours),
//                    MSL 4 (72 Hours), MSL 5 (48 Hours), MSL 5a (24 Hours), MSL 6 (TOL), N/A
//                    """;

//                var requestBody = new
//                {
//                    model = "claude-sonnet-4-5",
//                    max_tokens = 20,
//                    messages = new[] { new { role = "user", content = prompt } }
//                };

//                var client = _httpClientFactory.CreateClient();
//                var request = new HttpRequestMessage(HttpMethod.Post,
//                    "https://api.anthropic.com/v1/messages");
//                request.Headers.Add("x-api-key", apiKey);
//                request.Headers.Add("anthropic-version", "2023-06-01");
//                request.Content = new StringContent(
//                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

//                var response = await client.SendAsync(request);
//                if (!response.IsSuccessStatusCode) return "N/A";

//                var content = await response.Content.ReadAsStringAsync();
//                using var doc = JsonDocument.Parse(content);
//                var text = doc.RootElement
//                    .GetProperty("content")[0]
//                    .GetProperty("text")
//                    .GetString()?.Trim();

//                var valid = new[]
//                {
//                    "MSL 1 (Unlimited)", "MSL 2 (1 Year)", "MSL 2a (4 Weeks)",
//                    "MSL 3 (168 Hours)", "MSL 4 (72 Hours)", "MSL 5 (48 Hours)",
//                    "MSL 5a (24 Hours)", "MSL 6 (TOL)", "N/A"
//                };

//                return valid.Contains(text) ? text! : "N/A";
//            }
//            catch { return "N/A"; }
//        }

//        public string ExtractMountType(Dictionary<string, string> specs, string description)
//        {
//            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);

//            string mountType = "N/A";
//            string[] mountKeys = { "Mounting Type", "Mounting Style", "Mount Type", "Mounting" };
//            foreach (var key in mountKeys)
//                if (specsCI.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
//                { mountType = v; break; }

//            if (mountType != "N/A")
//            {
//                var mt = mountType.ToLower();
//                if (mt.Contains("surface") || mt.Contains("smt") || mt.Contains("smd"))
//                    return "SMT";
//                if (mt.Contains("through") || mt.Contains("thru"))
//                    return "Through-Hole";
//            }

//            var pkg = (specsCI.GetValueOrDefault("Package / Case", "") +
//                       specsCI.GetValueOrDefault("Supplier Device Package", "") +
//                       specsCI.GetValueOrDefault("Case/Package", "") +
//                       specsCI.GetValueOrDefault("Package", "")).ToLower();

//            if (pkg.Contains("soic") || pkg.Contains("qfp") || pkg.Contains("qfn") ||
//                pkg.Contains("sot-") || pkg.Contains("tssop") || pkg.Contains("bga") ||
//                pkg.Contains("dfn") || pkg.Contains("lqfp") || pkg.Contains("msop") ||
//                pkg.Contains("wlcsp") || pkg.Contains("0201") || pkg.Contains("0402") ||
//                pkg.Contains("0603") || pkg.Contains("0805") || pkg.Contains("1206") ||
//                pkg.Contains("smd") || pkg.Contains("sc-70") || pkg.Contains("sc-88"))
//                return "SMT";

//            if (pkg.Contains("dip") || pkg.Contains("to-92") ||
//                pkg.Contains("to-220") || pkg.Contains("to-247") ||
//                pkg.Contains("axial") || pkg.Contains("radial") ||
//                pkg.Contains("through"))
//                return "Through-Hole";

//            return "N/A";
//        }

//        public string ExtractPackage(Dictionary<string, string> specs)
//        {
//            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);
//            return specsCI.TryGetValue("Package / Case", out var p1) ? p1 :
//                   specsCI.TryGetValue("Supplier Device Package", out var p2) ? p2 :
//                   specsCI.TryGetValue("Case/Package", out var p3) ? p3 :
//                   specsCI.TryGetValue("Packaging", out var p4) ? p4 :
//                   specsCI.TryGetValue("Package", out var p5) ? p5 :
//                   specsCI.TryGetValue("Case", out var p6) ? p6 : "N/A";
//        }
//    }
//}











using System.Text;
using System.Text.Json;

namespace BomChecker.Services
{
    public class MslInferenceService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        // Only 1 Claude call at a time, as a last resort
        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(1, 1);

        public MslInferenceService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<string> InferMsl(
            string partNumber, string description, string package)
        {
            // ── Step 1: Infer from package name (instant, no API call) ──
            var mslFromPackage = InferMslFromPackage(package, description);
            if (mslFromPackage != "N/A")
                return mslFromPackage;

            // ── Step 2: Only call Claude if package inference failed ─────
            return await CallClaudeForMsl(partNumber, description, package);
        }

        // ── Infer MSL purely from package name and description ────────────
        private static string InferMslFromPackage(string package, string description)
        {
            var p = (package + " " + description).ToLower();

            // Through-hole → always MSL 1
            if (p.Contains("dip") || p.Contains("to-92") || p.Contains("to-220") ||
                p.Contains("to-247") || p.Contains("to-263") || p.Contains("to-252") ||
                p.Contains("axial") || p.Contains("radial") || p.Contains("through hole") ||
                p.Contains("through-hole") || p.Contains("thru-hole") || p.Contains("sip"))
                return "MSL 1 (Unlimited)";

            // Standard SMD passives → always MSL 1
            if (p.Contains("0201") || p.Contains("0402") || p.Contains("0603") ||
                p.Contains("0805") || p.Contains("1206") || p.Contains("1210") ||
                p.Contains("2512") || p.Contains("1812") || p.Contains("2010"))
                return "MSL 1 (Unlimited)";

            // Small SMD transistors/diodes → MSL 1
            if (p.Contains("sot-23") || p.Contains("sot23") || p.Contains("sot-323") ||
                p.Contains("sot-523") || p.Contains("sot-363") || p.Contains("sc-70") ||
                p.Contains("sc-88") || p.Contains("sod-123") || p.Contains("sod-323") ||
                p.Contains("sod-523") || p.Contains("do-214") || p.Contains("dpak") ||
                p.Contains("d2pak") || p.Contains("to-252") || p.Contains("sot-89") ||
                p.Contains("sot-223"))
                return "MSL 1 (Unlimited)";

            // Standard SOIC → MSL 1
            if (p.Contains("soic-8") || p.Contains("soic-14") || p.Contains("soic-16") ||
                p.Contains("soic-20") || p.Contains("soic-24") || p.Contains("soic-28") ||
                p.Contains("8-soic") || p.Contains("16-soic") || p.Contains("so-8") ||
                p.Contains("so-14") || p.Contains("so-16") || p.Contains("sop-8") ||
                p.Contains("sop-16") || p.Contains("ssop-8") || p.Contains("ssop-16"))
                return "MSL 1 (Unlimited)";

            // Connectors, crystals, LEDs, relays → MSL 1
            if (p.Contains("connector") || p.Contains("crystal") || p.Contains("oscillator") ||
                p.Contains("relay") || p.Contains("fuse") || p.Contains("switch") ||
                p.Contains("transformer") || p.Contains("led ") || p.Contains(" led"))
                return "MSL 1 (Unlimited)";

            // Electrolytic / tantalum caps → MSL 1
            if (p.Contains("radial") || p.Contains("alum") || p.Contains("electrolytic") ||
                p.Contains("tantalum") || p.Contains("tant"))
                return "MSL 1 (Unlimited)";

            // TSSOP, MSOP, SSOP → MSL 2
            if (p.Contains("tssop") || p.Contains("msop") || p.Contains("ssop") ||
                p.Contains("tsop") || p.Contains("soj"))
                return "MSL 2 (1 Year)";

            // Small QFN/DFN → MSL 2
            if ((p.Contains("qfn") || p.Contains("dfn") || p.Contains("mlf")) &&
                (p.Contains("-8") || p.Contains("-12") || p.Contains("-16") ||
                 p.Contains("-20") || p.Contains("-24") || p.Contains("-32")))
                return "MSL 2 (1 Year)";

            // QFP / LQFP → MSL 3
            if (p.Contains("qfp") || p.Contains("lqfp") || p.Contains("pqfp") ||
                p.Contains("tqfp"))
                return "MSL 3 (168 Hours)";

            // Large QFN → MSL 3
            if (p.Contains("qfn") || p.Contains("dfn"))
                return "MSL 3 (168 Hours)";

            // BGA / LGA / WLCSP → MSL 3+
            if (p.Contains("bga") || p.Contains("lga") || p.Contains("wlcsp") ||
                p.Contains("csp") || p.Contains("fcbga"))
                return "MSL 3 (168 Hours)";

            // Cable ties, mechanical, non-electronic → MSL 1
            if (p.Contains("cable") || p.Contains("wire") || p.Contains("tie") ||
                p.Contains("screw") || p.Contains("nut ") || p.Contains("bolt") ||
                p.Contains("heat shrink") || p.Contains("label") || p.Contains("tape"))
                return "MSL 1 (Unlimited)";

            return "N/A";
        }

        // ── Claude fallback — only called when package inference fails ────
        private async Task<string> CallClaudeForMsl(
            string partNumber, string description, string package)
        {
            await _throttle.WaitAsync();
            try
            {
                var apiKey = _config["Anthropic:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _throttle.Release();
                    return "N/A";
                }

                var prompt = $"""
                    You are an electronics component expert.
                    Given: Part={partNumber}, Description={description}, Package={package}
                    What is the MSL level per IPC/JEDEC J-STD-020?
                    Reply with ONLY one of:
                    MSL 1 (Unlimited), MSL 2 (1 Year), MSL 2a (4 Weeks),
                    MSL 3 (168 Hours), MSL 4 (72 Hours), MSL 5 (48 Hours),
                    MSL 5a (24 Hours), MSL 6 (TOL), N/A
                    """;

                var requestBody = new
                {
                    model = "claude-sonnet-4-5",
                    max_tokens = 20,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var client = _httpClientFactory.CreateClient();

                // Retry up to 2 times on 429
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    var request = new HttpRequestMessage(HttpMethod.Post,
                        "https://api.anthropic.com/v1/messages");
                    request.Headers.Add("x-api-key", apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(content);
                        var text = doc.RootElement
                            .GetProperty("content")[0]
                            .GetProperty("text")
                            .GetString()?.Trim();

                        var valid = new[]
                        {
                            "MSL 1 (Unlimited)", "MSL 2 (1 Year)", "MSL 2a (4 Weeks)",
                            "MSL 3 (168 Hours)", "MSL 4 (72 Hours)", "MSL 5 (48 Hours)",
                            "MSL 5a (24 Hours)", "MSL 6 (TOL)", "N/A"
                        };

                        await Task.Delay(300);
                        _throttle.Release();
                        return valid.Contains(text) ? text! : "N/A";
                    }

                    if ((int)response.StatusCode == 429 && attempt < 2)
                        await Task.Delay(3000);
                }

                _throttle.Release();
                return "N/A";
            }
            catch
            {
                _throttle.Release();
                return "N/A";
            }
        }

        // ── Extract mount type from specs ─────────────────────────────────
        public string ExtractMountType(Dictionary<string, string> specs, string description)
        {
            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);

            string mountType = "N/A";
            string[] mountKeys = { "Mounting Type", "Mounting Style", "Mount Type", "Mounting" };
            foreach (var key in mountKeys)
                if (specsCI.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                { mountType = v; break; }

            if (mountType != "N/A")
            {
                var mt = mountType.ToLower();
                if (mt.Contains("surface") || mt.Contains("smt") || mt.Contains("smd"))
                    return "SMT";
                if (mt.Contains("through") || mt.Contains("thru"))
                    return "Through-Hole";
            }

            var pkg = (specsCI.GetValueOrDefault("Package / Case", "") +
                       specsCI.GetValueOrDefault("Supplier Device Package", "") +
                       specsCI.GetValueOrDefault("Case/Package", "") +
                       specsCI.GetValueOrDefault("Package", "")).ToLower();

            if (pkg.Contains("soic") || pkg.Contains("qfp") || pkg.Contains("qfn") ||
                pkg.Contains("sot-") || pkg.Contains("tssop") || pkg.Contains("bga") ||
                pkg.Contains("dfn") || pkg.Contains("lqfp") || pkg.Contains("msop") ||
                pkg.Contains("wlcsp") || pkg.Contains("0201") || pkg.Contains("0402") ||
                pkg.Contains("0603") || pkg.Contains("0805") || pkg.Contains("1206") ||
                pkg.Contains("smd") || pkg.Contains("sc-70") || pkg.Contains("sc-88"))
                return "SMT";

            if (pkg.Contains("dip") || pkg.Contains("to-92") ||
                pkg.Contains("to-220") || pkg.Contains("to-247") ||
                pkg.Contains("axial") || pkg.Contains("radial") ||
                pkg.Contains("through"))
                return "Through-Hole";

            return "N/A";
        }

        // ── Extract package from specs ────────────────────────────────────
        public string ExtractPackage(Dictionary<string, string> specs)
        {
            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);
            return specsCI.TryGetValue("Package / Case", out var p1) ? p1 :
                   specsCI.TryGetValue("Supplier Device Package", out var p2) ? p2 :
                   specsCI.TryGetValue("Case/Package", out var p3) ? p3 :
                   specsCI.TryGetValue("Packaging", out var p4) ? p4 :
                   specsCI.TryGetValue("Package", out var p5) ? p5 :
                   specsCI.TryGetValue("Case", out var p6) ? p6 : "N/A";
        }
    }
}
