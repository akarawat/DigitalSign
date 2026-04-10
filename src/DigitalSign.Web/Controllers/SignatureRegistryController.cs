using System.Net.Http.Json;
using System.Text.Json;
using DigitalSign.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DigitalSign.Web.Controllers;

public class SignatureRegistryController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration     _config;
    private readonly ILogger<SignatureRegistryController> _logger;

    public SignatureRegistryController(
        IHttpClientFactory httpFactory,
        IConfiguration     config,
        ILogger<SignatureRegistryController> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient("DigitalSignApi");
        return client;
    }

    private string GetShortUsername()
    {
        var name = User.Identity?.Name ?? "unknown";
        return name.Contains('\\') ? name.Split('\\').Last() : name;
    }

    // ── GET /SignatureRegistry ────────────────────────────────────────────────
    // หน้า ลงทะเบียน / แก้ไข ลายเซ็นของตัวเอง
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var client = CreateClient();
            var resp   = await client.GetAsync("/api/signature-registry/me");
            var body   = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryDto>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            ViewBag.Existing  = result?.Data;
            ViewBag.Sam       = GetShortUsername();
            ViewBag.FullUser  = User.Identity?.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load signature registry");
            ViewBag.Existing = null;
            ViewBag.Sam      = GetShortUsername();
        }

        return View();
    }

    // ── POST /SignatureRegistry ───────────────────────────────────────────────
    // บันทึกลายเซ็น
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RegisterSignatureViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sam = GetShortUsername();
            return View(model);
        }

        if (model.SignatureFile == null || model.SignatureFile.Length == 0)
        {
            ModelState.AddModelError("SignatureFile", "Please upload your signature image.");
            ViewBag.Sam = GetShortUsername();
            return View(model);
        }

        try
        {
            var client  = CreateClient();
            using var form = new MultipartFormDataContent();

            form.Add(new StringContent(model.FullNameTH),              "FullNameTH");
            form.Add(new StringContent(model.FullNameEN),              "FullNameEN");
            form.Add(new StringContent(model.Position   ?? ""),        "Position");
            form.Add(new StringContent(model.Department ?? ""),        "Department");
            form.Add(new StringContent(model.Email      ?? ""),        "Email");

            using var ms = new MemoryStream();
            await model.SignatureFile.CopyToAsync(ms);
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(model.SignatureFile.ContentType);
            form.Add(fileContent, "SignatureFile", model.SignatureFile.FileName);

            var resp = await client.PostAsync("/api/signature-registry/register", form);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                TempData["SuccessMsg"] = "Signature registered successfully. Pending admin approval.";
                return RedirectToAction(nameof(Index));
            }

            var err = JsonSerializer.Deserialize<ApiResponse<object>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            ModelState.AddModelError("", err?.Message ?? "Registration failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature registration failed");
            ModelState.AddModelError("", "An error occurred. Please try again.");
        }

        ViewBag.Sam = GetShortUsername();
        return View(model);
    }

    // ── GET /SignatureRegistry/Admin ──────────────────────────────────────────
    // Admin: จัดการลายเซ็นทั้งหมด
    [HttpGet]
    public async Task<IActionResult> Admin(int page = 1)
    {
        try
        {
            var client = CreateClient();
            var resp   = await client.GetAsync($"/api/signature-registry/admin/list?page={page}&pageSize=20");
            var body   = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ApiResponse<SignatureRegistryListDto>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
        var client = CreateClient();
        var resp   = await client.PostAsync($"/api/signature-registry/admin/{id}/approve", null);

        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature approved successfully." : "Approval failed.";

        return RedirectToAction(nameof(Admin));
    }

    // ── POST /SignatureRegistry/Deactivate/{id} ───────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(long id)
    {
        var client = CreateClient();
        var resp   = await client.DeleteAsync($"/api/signature-registry/admin/{id}");

        TempData[resp.IsSuccessStatusCode ? "SuccessMsg" : "ErrorMsg"] =
            resp.IsSuccessStatusCode ? "Signature removed." : "Failed to remove.";

        return RedirectToAction(nameof(Admin));
    }
}
