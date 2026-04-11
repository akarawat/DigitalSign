using System.Text.Json;
using DigitalSign.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DigitalSign.Web.Controllers;

public class SignatureRegistryController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
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
        _logger = logger;
    }

    // ── Named Client "DigitalSignApi" มี X-Api-Key ใน Header อยู่แล้ว ─────────
    private HttpClient GetApiClient()
        => _httpFactory.CreateClient("DigitalSignApi");

    // ── สร้าง HttpRequestMessage พร้อม X-Sam-Account ─────────────────────────
    // ใช้ request-level header แทน client-level เพื่อ thread-safe
    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Sam-Account", GetSam());
        return req;
    }

    private async Task<HttpResponseMessage> GetAsync(string url)
        => await GetApiClient().SendAsync(CreateRequest(HttpMethod.Get, url));

    private async Task<HttpResponseMessage> PostAsync(string url, HttpContent? content = null)
    {
        var req = CreateRequest(HttpMethod.Post, url);
        req.Content = content;
        return await GetApiClient().SendAsync(req);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string url)
        => await GetApiClient().SendAsync(CreateRequest(HttpMethod.Delete, url));

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
            var resp = await GetAsync("api/signature-registry/me");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryDto>>(body, _json);
                ViewBag.Existing = result?.Data;
            }
            else
            {
                _logger.LogWarning("API /me: {S} {B}", resp.StatusCode,
                    body[..Math.Min(200, body.Length)]);
                ViewBag.Existing = null;
            }
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
        byte[]? imageBytes = null;
        string imageMimeType = "image/png";
        string imageFileName = "signature.png";

        // Draw mode
        if (!string.IsNullOrEmpty(CanvasData) && CanvasData.StartsWith("data:image/"))
        {
            imageBytes = Convert.FromBase64String(CanvasData.Split(',').Last());
            imageFileName = $"signature_{GetSam()}.png";
        }
        // Upload mode
        else if (model.SignatureFile != null && model.SignatureFile.Length > 0)
        {
            if (model.SignatureFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError("SignatureFile", "Max 2 MB.");
                return await ReturnViewWithExisting(model);
            }

            var allowed = new[] { "image/png", "image/jpeg", "image/jpg" };
            if (!allowed.Contains(model.SignatureFile.ContentType.ToLower()))
            {
                ModelState.AddModelError("SignatureFile", "PNG or JPG only.");
                return await ReturnViewWithExisting(model);
            }

            using var ms = new MemoryStream();
            await model.SignatureFile.CopyToAsync(ms);
            imageBytes = ms.ToArray();
            imageMimeType = model.SignatureFile.ContentType;
            imageFileName = model.SignatureFile.FileName;
        }
        else
        {
            ModelState.AddModelError("", "Please draw or upload your signature.");
            return await ReturnViewWithExisting(model);
        }

        if (string.IsNullOrWhiteSpace(model.FullNameTH) ||
            string.IsNullOrWhiteSpace(model.FullNameEN))
        {
            ModelState.AddModelError("", "Full Name (TH) and (EN) are required.");
            return await ReturnViewWithExisting(model);
        }

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(model.FullNameTH), "FullNameTH");
            form.Add(new StringContent(model.FullNameEN), "FullNameEN");
            form.Add(new StringContent(model.Position ?? ""), "Position");
            form.Add(new StringContent(model.Department ?? ""), "Department");
            form.Add(new StringContent(model.Email ?? ""), "Email");

            var fc = new ByteArrayContent(imageBytes);
            fc.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(imageMimeType);
            form.Add(fc, "SignatureFile", imageFileName);

            var resp = await PostAsync("api/signature-registry/register", form);
            var body = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("Register API: {S} | {B}",
                resp.StatusCode, body[..Math.Min(300, body.Length)]);

            if (resp.IsSuccessStatusCode)
            {
                TempData["SuccessMsg"] = "Signature registered. Pending admin approval.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var err = JsonSerializer.Deserialize<ApiResponse<object>>(body, _json);
                ModelState.AddModelError("", err?.Message ?? "Registration failed.");
            }
            catch
            {
                ModelState.AddModelError("", $"API returned {(int)resp.StatusCode}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register failed");
            ModelState.AddModelError("", $"Error: {ex.Message}");
        }

        return await ReturnViewWithExisting(model);
    }

    // ── GET /SignatureRegistry/Image/{sam} ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Image(string id)
    {
        try
        {
            var client = _httpFactory.CreateClient("DigitalSignApi");
            var resp = await client.GetAsync($"api/signature-registry/image/{id}");
            if (!resp.IsSuccessStatusCode) return NotFound();
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return File(bytes, resp.Content.Headers.ContentType?.ToString() ?? "image/png");
        }
        catch { return NotFound(); }
    }

    // ── GET /SignatureRegistry/Admin ──────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Admin(int page = 1)
    {
        try
        {
            var resp = await GetAsync($"api/signature-registry/admin/list?page={page}&pageSize=20");
            var body = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryListDto>>(body, _json);
            return View(result?.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin list failed");
            return View(null as SignatureRegistryListDto);
        }
    }

    // ── POST /SignatureRegistry/Approve/{id} ──────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id)
    {
        var resp = await PostAsync($"api/signature-registry/admin/{id}/approve");
        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature approved." : "Approval failed.";
        return RedirectToAction(nameof(Admin));
    }

    // ── POST /SignatureRegistry/Deactivate/{id} ───────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(long id)
    {
        var resp = await DeleteAsync($"api/signature-registry/admin/{id}");
        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature removed." : "Remove failed.";
        return RedirectToAction(nameof(Admin));
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private async Task<IActionResult> ReturnViewWithExisting(RegisterSignatureViewModel model)
    {
        try
        {
            var resp = await GetAsync("api/signature-registry/me");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryDto>>(body, _json);
                ViewBag.Existing = result?.Data;
            }
            else ViewBag.Existing = null;
        }
        catch { ViewBag.Existing = null; }

        ViewBag.Sam = GetSam();
        return View(model);
    }
}
