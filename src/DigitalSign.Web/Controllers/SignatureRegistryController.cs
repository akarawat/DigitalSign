using System.Text.Json;
using DigitalSign.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DigitalSign.Web.Controllers;

public class SignatureRegistryController : Controller
{
    private readonly IHttpClientFactory  _httpFactory;
    private readonly ILogger<SignatureRegistryController> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SignatureRegistryController(
        IHttpClientFactory httpFactory,
        ILogger<SignatureRegistryController> logger)
    {
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    private HttpClient CreateClient() => _httpFactory.CreateClient("DigitalSignApi");

    private string GetSam()
    {
        var name = User.Identity?.Name ?? "unknown";
        return name.Contains('\\') ? name.Split('\\').Last() : name;
    }

    // ── GET /SignatureRegistry ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var resp   = await CreateClient().GetAsync("/api/signature-registry/me");
            var body   = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryDto>>(body, _json);
            ViewBag.Existing = result?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load my signature");
            ViewBag.Existing = null;
        }

        ViewBag.Sam = GetSam();
        return View(new RegisterSignatureViewModel());
    }

    // ── POST /SignatureRegistry ───────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegisterSignatureViewModel model,
        [FromForm] string? CanvasData)
    {
        // ── ตรวจสอบ source: Draw หรือ Upload ──────────────────────────────────
        byte[]? imageBytes    = null;
        string  imageMimeType = "image/png";
        string  imageFileName = "signature.png";

        if (!string.IsNullOrEmpty(CanvasData) && CanvasData.StartsWith("data:image/"))
        {
            // ── Draw mode: แปลง Base64 PNG จาก Canvas ────────────────────────
            var base64 = CanvasData.Split(',').Last();
            imageBytes    = Convert.FromBase64String(base64);
            imageMimeType = "image/png";
            imageFileName = $"signature_{GetSam()}.png";
        }
        else if (model.SignatureFile != null && model.SignatureFile.Length > 0)
        {
            // ── Upload mode: ใช้ไฟล์ที่ upload ──────────────────────────────
            if (model.SignatureFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError("SignatureFile", "Image size must not exceed 2 MB.");
                return await ReturnViewWithExisting(model);
            }

            var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg" };
            if (!allowedTypes.Contains(model.SignatureFile.ContentType.ToLower()))
            {
                ModelState.AddModelError("SignatureFile", "Only PNG and JPG images are supported.");
                return await ReturnViewWithExisting(model);
            }

            using var ms = new MemoryStream();
            await model.SignatureFile.CopyToAsync(ms);
            imageBytes    = ms.ToArray();
            imageMimeType = model.SignatureFile.ContentType;
            imageFileName = model.SignatureFile.FileName;
        }
        else
        {
            ModelState.AddModelError("", "Please draw or upload your signature.");
            return await ReturnViewWithExisting(model);
        }

        // ── Validate required fields ──────────────────────────────────────────
        if (string.IsNullOrEmpty(model.FullNameTH) || string.IsNullOrEmpty(model.FullNameEN))
        {
            ModelState.AddModelError("", "Full Name (TH) and Full Name (EN) are required.");
            return await ReturnViewWithExisting(model);
        }

        try
        {
            // ── ส่งไปที่ API ──────────────────────────────────────────────────
            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(model.FullNameTH),       "FullNameTH");
            form.Add(new StringContent(model.FullNameEN),       "FullNameEN");
            form.Add(new StringContent(model.Position   ?? ""), "Position");
            form.Add(new StringContent(model.Department ?? ""), "Department");
            form.Add(new StringContent(model.Email      ?? ""), "Email");

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(imageMimeType);
            form.Add(fileContent, "SignatureFile", imageFileName);

            var resp = await CreateClient().PostAsync("/api/signature-registry/register", form);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["SuccessMsg"] = "Signature registered successfully. Pending admin approval.";
                return RedirectToAction(nameof(Index));
            }

            var err = JsonSerializer.Deserialize<ApiResponse<object>>(body, _json);
            ModelState.AddModelError("", err?.Message ?? "Registration failed. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature registration failed");
            ModelState.AddModelError("", "An error occurred. Please try again.");
        }

        return await ReturnViewWithExisting(model);
    }

    // ── GET /SignatureRegistry/Image/{sam} ────────────────────────────────────
    // Proxy image จาก API
    [HttpGet]
    public async Task<IActionResult> Image(string samAccount)
    {
        try
        {
            var resp = await CreateClient().GetAsync($"/api/signature-registry/image/{samAccount}");
            if (!resp.IsSuccessStatusCode) return NotFound();

            var bytes       = await resp.Content.ReadAsByteArrayAsync();
            var contentType = resp.Content.Headers.ContentType?.ToString() ?? "image/png";
            return File(bytes, contentType);
        }
        catch { return NotFound(); }
    }

    // ── GET /SignatureRegistry/Admin ──────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Admin(int page = 1)
    {
        try
        {
            var resp   = await CreateClient().GetAsync($"/api/signature-registry/admin/list?page={page}&pageSize=20");
            var body   = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryListDto>>(body, _json);
            return View(result?.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load admin list");
            return View(null as SignatureRegistryListDto);
        }
    }

    // ── POST /SignatureRegistry/Approve/{id} ──────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id)
    {
        var resp = await CreateClient().PostAsync($"/api/signature-registry/admin/{id}/approve", null);
        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature approved." : "Approval failed.";
        return RedirectToAction(nameof(Admin));
    }

    // ── POST /SignatureRegistry/Deactivate/{id} ───────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(long id)
    {
        var resp = await CreateClient().DeleteAsync($"/api/signature-registry/admin/{id}");
        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature removed." : "Failed to remove.";
        return RedirectToAction(nameof(Admin));
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private async Task<IActionResult> ReturnViewWithExisting(RegisterSignatureViewModel model)
    {
        try
        {
            var resp   = await CreateClient().GetAsync("/api/signature-registry/me");
            var body   = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryDto>>(body, _json);
            ViewBag.Existing = result?.Data;
        }
        catch { ViewBag.Existing = null; }

        ViewBag.Sam = GetSam();
        return View(model);
    }
}
