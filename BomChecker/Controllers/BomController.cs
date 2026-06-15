using BomChecker.Data;
using BomChecker.Models;
using BomChecker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Text.Json;

namespace BomChecker.Controllers
{
    public class BomController : Controller
    {
        private readonly DigiKeyService _digikey;
        private readonly MouserService _mouser;
        private readonly LCSCService _lcsc;
        private readonly MslInferenceService _msl;
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public BomController
            (
            DigiKeyService digikey,
            MouserService mouser,
            LCSCService lcsc,
            MslInferenceService msl,
            IMemoryCache cache,
            AppDbContext db,
            IHttpClientFactory httpClientFactory,   // ← ADD
            IConfiguration config                // ← ADD
            )
        {
            _digikey = digikey;
            _mouser = mouser;
            _lcsc = lcsc;
            _msl = msl;
            _cache = cache;
            _db = db;
            _httpClientFactory = httpClientFactory; // ← ADD
            _config = config;                       // ← ADD
        }

        // ── GET: /Bom ─────────────────────────────────────────────
        [HttpGet]
        public IActionResult Index() => View();

        // ── POST: /Bom/Upload ─────────────────────────────────────
        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile bomFile)
        {
            if (bomFile == null || bomFile.Length == 0)
            {
                ViewBag.Error = "Please select a valid .xlsx file.";
                return View("Index");
            }

            if (!Path.GetExtension(bomFile.FileName)
                    .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Only .xlsx files are supported.";
                return View("Index");
            }

            try
            {
                using var stream = new MemoryStream();
                await bomFile.CopyToAsync(stream);
                stream.Position = 0;

                ExcelPackage.License.SetNonCommercialPersonal("BomChecker");
                using var package = new ExcelPackage(stream);
                var sheet = package.Workbook.Worksheets[0];

                if (sheet.Dimension == null)
                {
                    ViewBag.Error = "The Excel sheet is empty.";
                    return View("Index");
                }

                var headers = new List<string>();
                for (int col = 1; col <= sheet.Dimension.End.Column; col++)
                {
                    var h = sheet.Cells[1, col].Text?.Trim();
                    if (!string.IsNullOrEmpty(h))
                        headers.Add(h);
                }

                if (headers.Count == 0)
                {
                    ViewBag.Error = "No column headers found in row 1.";
                    return View("Index");
                }

                var cacheKey = Guid.NewGuid().ToString("N");
                var fileBytes = stream.ToArray();
                _cache.Set(cacheKey + "_file", fileBytes,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(60)));
                _cache.Set(cacheKey + "_name", bomFile.FileName,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(60)));

                var vm = new BomUploadViewModel
                {
                    ColumnHeaders = headers,
                    CacheKey = cacheKey,
                    FileName = bomFile.FileName,
                    TotalRows = sheet.Dimension.End.Row - 1
                };

                return View("SelectColumns", vm);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to read file: " + ex.Message;
                return View("Index");
            }
        }

        // ── POST: /Bom/Verify ─────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Verify(BomVerifyRequest request)
        {
            if (string.IsNullOrEmpty(request.CacheKey) ||
                string.IsNullOrEmpty(request.DescriptionColumn) ||
                request.PartNumberColumns == null ||
                request.PartNumberColumns.Count == 0)
            {
                ViewBag.Error = "Please select a description column and at least one part number column.";
                return View("Index");
            }

            if (!_cache.TryGetValue(request.CacheKey + "_file", out byte[] fileBytes))
            {
                ViewBag.Error = "Session expired. Please upload the file again.";
                return View("Index");
            }

            var fileName = _cache.TryGetValue(request.CacheKey + "_name", out string fn)
                ? fn : "BOM.xlsx";

            try
            {
                ExcelPackage.License.SetNonCommercialPersonal("BomChecker");
                using var stream = new MemoryStream(fileBytes);
                using var package = new ExcelPackage(stream);
                var sheet = package.Workbook.Worksheets[0];

                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= sheet.Dimension.End.Column; col++)
                {
                    var h = sheet.Cells[1, col].Text?.Trim();
                    if (!string.IsNullOrEmpty(h))
                        headerMap[h] = col;
                }

                if (!headerMap.TryGetValue(request.DescriptionColumn, out int descCol))
                {
                    ViewBag.Error = "Description column not found.";
                    return View("Index");
                }

                var partCols = new Dictionary<string, int>();
                foreach (var pnCol in request.PartNumberColumns)
                    if (headerMap.TryGetValue(pnCol, out int idx))
                        partCols[pnCol] = idx;

                // ── Save report to DB ─────────────────────────────
                var report = new BomReport
                {
                    FileName = fileName,
                    UploadedAt = DateTime.Now,
                    TotalRows = sheet.Dimension.End.Row - 1
                };
                _db.BomReports.Add(report);
                await _db.SaveChangesAsync();

                var result = new BomVerifyResult
                {
                    FileName = fileName,
                    TotalRows = sheet.Dimension.End.Row - 1,
                    PartNumberColumns = request.PartNumberColumns
                };

                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    var desc = sheet.Cells[row, descCol].Value?.ToString()?.Trim() ?? "";

                    bool hasAnyData = !string.IsNullOrWhiteSpace(desc) ||
                        partCols.Values.Any(c =>
                            !string.IsNullOrWhiteSpace(
                                sheet.Cells[row, c].Value?.ToString()));
                    if (!hasAnyData) continue;

                    var bomRow = new BomRow
                    {
                        RowNumber = row,
                        OriginalDescription = desc
                    };

                    var tasks = partCols.Select(async kvp =>
                    {
                        var colName = kvp.Key;
                        var colIdx = kvp.Value;
                        var partNumber = sheet.Cells[row, colIdx].Value?.ToString()?.Trim() ?? "";
                        return await VerifyPartAsync(colName, partNumber, desc);
                    });

                    var partResults = await Task.WhenAll(tasks);
                    bomRow.PartResults = partResults.ToList();

                    var best = bomRow.PartResults
                        .Where(p => p.Found && p.MatchScore > 0)
                        .OrderByDescending(p => p.MatchScore)
                        .FirstOrDefault();

                    if (best != null)
                    {
                        bomRow.BestMatchPartNumber = best.PartNumber;
                        bomRow.BestMatchScore = best.MatchScore;
                    }

                    // ── Save row + part results to DB ─────────────
                    var rowEntity = new BomRowEntity
                    {
                        ReportId = report.Id,
                        RowNumber = bomRow.RowNumber,
                        OriginalDescription = bomRow.OriginalDescription,
                        BestMatchPartNumber = bomRow.BestMatchPartNumber,
                        BestMatchScore = bomRow.BestMatchScore,
                        PartResults = bomRow.PartResults.Select(pr => new PartResultEntity
                        {
                            ColumnName = pr.ColumnName,
                            PartNumber = pr.PartNumber,
                            ApiDescription = pr.ApiDescription,
                            Manufacturer = pr.Manufacturer,
                            Package = pr.Package,
                            MslLevel = pr.MslLevel,
                            MountType = pr.MountType,
                            MatchScore = pr.MatchScore,
                            MatchVerdict = pr.MatchVerdict,
                            Source = pr.Source,
                            ProductUrl = pr.ProductUrl,
                            Found = pr.Found
                        }).ToList()
                    };
                    _db.BomRows.Add(rowEntity);

                    result.Rows.Add(bomRow);
                    result.ProcessedRows++;

                    await Task.Delay(500);
                }

                // Update report processed count
                report.ProcessedRows = result.ProcessedRows;
                await _db.SaveChangesAsync();

                // Cache result for Excel export
                _cache.Set(request.CacheKey + "_result",
                    JsonSerializer.Serialize(result),
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(30)));
                _cache.Set(request.CacheKey + "_cols",
                    JsonSerializer.Serialize(request.PartNumberColumns),
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(30)));

                TempData["ResultKey"] = request.CacheKey;
                TempData["ReportId"] = report.Id;

                return View("Results", result);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Processing failed: " + ex.Message;
                return View("Index");
            }
        }

        // ── GET: /Bom/History ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var reports = await _db.BomReports
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View("History", reports);
        }

        // ── GET: /Bom/ViewReport/5 ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> ViewReport(int id)
        {
            var report = await _db.BomReports
                .Include(r => r.Rows)
                    .ThenInclude(row => row.PartResults)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                TempData["Error"] = "Report not found.";
                return RedirectToAction("History");
            }

            // Get all unique column names from this report
            var partNumberColumns = report.Rows
                .SelectMany(r => r.PartResults)
                .Select(pr => pr.ColumnName)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Map back to BomVerifyResult view model
            var result = new BomVerifyResult
            {
                FileName = report.FileName,
                TotalRows = report.TotalRows,
                ProcessedRows = report.ProcessedRows,
                PartNumberColumns = partNumberColumns,
                Rows = report.Rows.Select(row => new BomRow
                {
                    RowNumber = row.RowNumber,
                    OriginalDescription = row.OriginalDescription,
                    BestMatchPartNumber = row.BestMatchPartNumber,
                    BestMatchScore = row.BestMatchScore,
                    PartResults = row.PartResults.Select(pr => new PartResult
                    {
                        ColumnName = pr.ColumnName,
                        PartNumber = pr.PartNumber,
                        ApiDescription = pr.ApiDescription,
                        Manufacturer = pr.Manufacturer,
                        Package = pr.Package,
                        MslLevel = pr.MslLevel,
                        MountType = pr.MountType,
                        MatchScore = pr.MatchScore,
                        MatchVerdict = pr.MatchVerdict,
                        Source = pr.Source,
                        ProductUrl = pr.ProductUrl,
                        Found = pr.Found
                    }).ToList()
                }).ToList()
            };

            // Cache for export
            var cacheKey = $"report_{id}";
            _cache.Set(cacheKey + "_result",
                JsonSerializer.Serialize(result),
                new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(30)));

            TempData["ResultKey"] = cacheKey;
            TempData["ReportId"] = id;

            return View("Results", result);
        }

        // ── GET: /Bom/DeleteReport/5 ──────────────────────────────
        [HttpPost]
        public async Task<IActionResult> DeleteReport(int id)
        {
            var report = await _db.BomReports.FindAsync(id);
            if (report != null)
            {
                _db.BomReports.Remove(report);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("History");
        }

        // ── Verify a single part number ───────────────────────────
        private async Task<PartResult> VerifyPartAsync(
            string columnName, string partNumber, string userDesc)
        {
            var result = new PartResult
            {
                ColumnName = columnName,
                PartNumber = partNumber
            };

            if (string.IsNullOrWhiteSpace(partNumber))
            {
                result.MatchVerdict = "—";
                return result;
            }

            PartDetails? data = await _digikey.GetPartDetails(partNumber);
            if (data == null) data = await _mouser.GetPartDetails(partNumber);
            if (data == null) data = await _lcsc.GetPartDetails(partNumber);

            if (data == null)
            {
                result.MatchVerdict = "❌ Not found";
                result.Found = false;
                return result;
            }

            result.Found = true;
            result.Source = data.Source;
            result.ApiDescription = data.Description;
            result.Manufacturer = data.Manufacturer;
            result.ProductUrl = data.ProductUrl;
            result.Package = !string.IsNullOrEmpty(data.Package)
                ? data.Package : _msl.ExtractPackage(data.Specs);
            result.MountType = _msl.ExtractMountType(data.Specs, data.Description);
            result.MslLevel = ExtractMslFromSpecs(data.Specs, data.Description);

            //if (result.MslLevel == "N/A")
            //{
            //    var enrichedDesc = string.IsNullOrWhiteSpace(data.Description)
            //        ? data.Category : data.Description + " " + data.Category;
            //    result.MslLevel = await _msl.InferMsl(partNumber, enrichedDesc, result.Package);
            //}


            if (result.MslLevel == "N/A")
            {
                var enrichedDesc = string.IsNullOrWhiteSpace(data.Description)
                    ? data.Category : data.Description + " " + data.Category;

                System.Diagnostics.Debug.WriteLine($"[MSL DEBUG] Calling InferMsl | PN={partNumber} | Pkg={result.Package} | Desc={enrichedDesc}");

                result.MslLevel = await _msl.InferMsl(partNumber, enrichedDesc, result.Package);

                System.Diagnostics.Debug.WriteLine($"[MSL DEBUG] Result = {result.MslLevel}");
            }

            if (!string.IsNullOrWhiteSpace(userDesc) && !string.IsNullOrWhiteSpace(data.Description))
            {
                result.MatchScore = CalculateMatchScore(userDesc, data.Description);
                result.MatchVerdict = GetMatchVerdict(result.MatchScore);
            }
            else if (string.IsNullOrWhiteSpace(userDesc))
                result.MatchVerdict = "⚠️ No description";
            else
                result.MatchVerdict = "⚠️ No API description";

            return result;
        }

        private static string ExtractMslFromSpecs(
            Dictionary<string, string> specs, string description)
        {
            var specsCI = new Dictionary<string, string>(specs, StringComparer.OrdinalIgnoreCase);
            string[] mslKeys = {
                "Moisture Sensitivity Level (MSL)",
                "Moisture Sensitivity Level", "MSL", "Moisture Sensitivity"
            };
            foreach (var key in mslKeys)
                if (specsCI.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v;

            if (!string.IsNullOrWhiteSpace(description))
            {
                var d = description.ToLower();
                if (d.Contains("msl 1") || d.Contains("msl1")) return "MSL 1 (Unlimited)";
                if (d.Contains("msl 2a") || d.Contains("msl2a")) return "MSL 2a (4 Weeks)";
                if (d.Contains("msl 2") || d.Contains("msl2")) return "MSL 2 (1 Year)";
                if (d.Contains("msl 3") || d.Contains("msl3")) return "MSL 3 (168 Hours)";
                if (d.Contains("msl 4") || d.Contains("msl4")) return "MSL 4 (72 Hours)";
                if (d.Contains("msl 5") || d.Contains("msl5")) return "MSL 5 (48 Hours)";
            }
            return "N/A";
        }

        private static double CalculateMatchScore(string userDesc, string apiDesc)
        {
            if (string.IsNullOrWhiteSpace(apiDesc)) return 0;
            var words = userDesc.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var apiLower = apiDesc.ToLower();
            int matched = words.Count(w => apiLower.Contains(w));
            return Math.Round((double)matched / words.Length * 100, 1);
        }

        private static string GetMatchVerdict(double score)
        {
            if (score >= 80) return $"✅ Strong ({score}%)";
            if (score >= 50) return $"⚠️ Partial ({score}%)";
            if (score >= 20) return $"⚠️ Weak ({score}%)";
            return $"❌ No match ({score}%)";
        }

        // ── GET: /Bom/Export ──────────────────────────────────────
        [HttpGet]
        public IActionResult Export(string key)
        {
            if (string.IsNullOrEmpty(key) ||
                !_cache.TryGetValue(key + "_result", out string resultJson))
            {
                TempData["Error"] = "Export session expired. Please re-open the report.";
                return RedirectToAction("History");
            }

            var result = JsonSerializer.Deserialize<BomVerifyResult>(resultJson);
            if (result == null) return RedirectToAction("Index");

            ExcelPackage.License.SetNonCommercialPersonal("BomChecker");
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("BOM Verify Results");

            var headers = new List<string> { "Row #", "Original Description" };
            foreach (var col in result.PartNumberColumns)
            {
                headers.Add($"{col} — Part No.");
                headers.Add($"{col} — API Description");
                headers.Add($"{col} — Match %");
                headers.Add($"{col} — Package");
                headers.Add($"{col} — MSL Level");
                headers.Add($"{col} — Mount Type");
            }
            headers.Add("Best Match Part No.");
            headers.Add("Best Match Score");

            for (int i = 0; i < headers.Count; i++)
            {
                var cell = sheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(28, 57, 107));
                cell.Style.WrapText = true;
            }
            sheet.Row(1).Height = 32;

            for (int i = 0; i < result.Rows.Count; i++)
            {
                var row = result.Rows[i];
                int excelRow = i + 2;
                int col = 1;

                sheet.Cells[excelRow, col++].Value = row.RowNumber;
                sheet.Cells[excelRow, col++].Value = row.OriginalDescription;

                foreach (var pnCol in result.PartNumberColumns)
                {
                    var pr = row.PartResults.FirstOrDefault(p => p.ColumnName == pnCol);

                    if (pr == null || string.IsNullOrWhiteSpace(pr.PartNumber))
                    {
                        for (int j = 0; j < 6; j++) sheet.Cells[excelRow, col++].Value = "—";
                        continue;
                    }

                    sheet.Cells[excelRow, col++].Value = pr.PartNumber;
                    sheet.Cells[excelRow, col++].Value = pr.ApiDescription;

                    var matchCell = sheet.Cells[excelRow, col++];
                    matchCell.Value = pr.Found ? $"{pr.MatchScore}%" : "—";
                    if (pr.Found)
                    {
                        matchCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        matchCell.Style.Fill.BackgroundColor.SetColor(
                            pr.MatchScore >= 80 ? Color.FromArgb(198, 239, 206) :
                            pr.MatchScore >= 50 ? Color.FromArgb(255, 235, 156) :
                                                  Color.FromArgb(255, 199, 206));
                    }

                    sheet.Cells[excelRow, col++].Value = pr.Package;
                    sheet.Cells[excelRow, col++].Value = pr.MslLevel;

                    var mountCell = sheet.Cells[excelRow, col++];
                    mountCell.Value = pr.MountType;
                    if (pr.MountType == "SMT")
                    {
                        mountCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        mountCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(189, 215, 238));
                    }
                    else if (pr.MountType == "Through-Hole")
                    {
                        mountCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        mountCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(198, 239, 206));
                    }
                }

                var bestCell = sheet.Cells[excelRow, col++];
                bestCell.Value = row.BestMatchPartNumber;
                if (!string.IsNullOrEmpty(row.BestMatchPartNumber))
                {
                    bestCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    bestCell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(198, 239, 206));
                    bestCell.Style.Font.Bold = true;
                }
                sheet.Cells[excelRow, col].Value =
                    row.BestMatchScore > 0 ? $"{row.BestMatchScore}%" : "—";
            }

            sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
            sheet.View.FreezePanes(2, 3);
            sheet.View.ZoomScale = 90;

            return File(package.GetAsByteArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BOM_Verify_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        // ── TEMP DEBUG: /Bom/TestMsl?partNumber=LM358 ────────────
        [HttpGet]
        public async Task<IActionResult> TestMsl(string partNumber = "LM358")
        {
            var apiKey = _config["Anthropic:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                return Content("❌ API key is NULL — check appsettings.json");

            try
            {
                var client = _httpClientFactory.CreateClient();

                var requestBody = new
                {
                    model = "claude-sonnet-4-5",
                    max_tokens = 20,
                    messages = new[]
                    {
                new { role = "user", content = $"What is the MSL level of {partNumber} in SOIC-8? Reply with only: MSL 1 (Unlimited), MSL 2 (1 Year), or N/A" }
            }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE] API key present: {!string.IsNullOrEmpty(apiKey)}");
                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE] Sending request for: {partNumber}");

                var response = await client.SendAsync(request);

                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE] Response status: {response.StatusCode}");
                var responseBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[MSL SERVICE] Response body: {responseBody}");

                var content = await response.Content.ReadAsStringAsync();

                return Content($"Status: {response.StatusCode}\nKey (first 10): {apiKey[..10]}...\n\nResponse:\n{content}");
            }
            catch (Exception ex)
            {
                return Content($"❌ Exception: {ex.Message}");
            }
        }
    }
}
