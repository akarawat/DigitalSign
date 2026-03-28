using DigitalSign.Web.Models;
using DigitalSign.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalSign.Web.Controllers;

public class SignController : Controller
{
    private readonly IDigitalSignService _signService;

    public SignController(IDigitalSignService signService)
    {
        _signService = signService;
    }

    [HttpGet]
    public IActionResult Index() => View(new SignRequest());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignRequest model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signService.SignDataAsync(model, GetShortUsername());
        if (result == null || !result.IsSuccess)
        {
            ModelState.AddModelError("", result?.ErrorMessage ?? "Unable to sign. Please try again.");
            return View(model);
        }

        TempData["SignResult"] = System.Text.Json.JsonSerializer.Serialize(result);
        TempData["SuccessMsg"] = "Signature applied successfully.";
        return RedirectToAction(nameof(SignResult));
    }

    [HttpGet]
    public IActionResult SignResult()
    {
        var json = TempData["SignResult"] as string;
        if (string.IsNullOrEmpty(json)) return RedirectToAction(nameof(Index));
        var result = System.Text.Json.JsonSerializer.Deserialize<SignResult>(json);
        return View(result);
    }

    [HttpGet]
    public IActionResult Verify() => View(new VerifyRequest());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(VerifyRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _signService.VerifySignatureAsync(model);
        ViewBag.VerifyResult = result;
        return View(model);
    }

    [HttpGet]
    public IActionResult PdfSign() => View(new PdfSignRequest());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PdfSign(PdfSignRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.PdfFile == null || model.PdfFile.Length == 0)
        {
            ModelState.AddModelError("PdfFile", "Please select a PDF file.");
            return View(model);
        }

        using var ms = new MemoryStream();
        await model.PdfFile.CopyToAsync(ms);
        var pdfBase64 = Convert.ToBase64String(ms.ToArray());

        var result = await _signService.SignPdfAsync(
            pdfBase64, model.DocumentName, model.ReferenceId,
            model.Reason, model.Location,
            signerUsername: GetFullUsername());

        if (result == null || !result.IsSuccess)
        {
            ModelState.AddModelError("", result?.ErrorMessage ?? "Unable to sign PDF.");
            return View(model);
        }

        var signedBytes = Convert.FromBase64String(result.PdfBase64);
        var fileName = $"signed_{model.DocumentName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
        return File(signedBytes, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Audit(int page = 1)
    {
        var result = await _signService.GetMyAuditAsync(page, 20);
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> AuditDetail(string referenceId)
    {
        var records = await _signService.GetAuditByReferenceAsync(referenceId);
        ViewBag.ReferenceId = referenceId;
        return View(records);
    }

    // ── username สั้น: BERNINATHAILAND\sakulchai.p → sakulchai.p
    private string GetShortUsername()
    {
        var name = User.Identity?.Name ?? "unknown";
        return name.Contains('\\') ? name.Split('\\').Last() : name;
    }

    // ── full username: BERNINATHAILAND\sakulchai.p
    private string GetFullUsername()
        => User.Identity?.Name ?? "unknown";
}
