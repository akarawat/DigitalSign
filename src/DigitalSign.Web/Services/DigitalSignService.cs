using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DigitalSign.Web.Models;
using Microsoft.Extensions.Logging;

namespace DigitalSign.Web.Services;

public interface IDigitalSignService
{
    Task<CertificateHealth?>      GetHealthAsync();
    Task<SignResult?>              SignDataAsync(SignRequest request);
    Task<VerifyResult?>           VerifySignatureAsync(VerifyRequest request);
    Task<PdfSignResult?>          SignPdfAsync(string pdfBase64, string docName, string refId, string reason, string location);
    Task<AuditPagedResult?>       GetMyAuditAsync(int page = 1, int pageSize = 20);
    Task<List<AuditRecord>?>      GetAuditByReferenceAsync(string referenceId);
}

public class DigitalSignService : IDigitalSignService
{
    private readonly HttpClient            _http;
    private readonly ILogger<DigitalSignService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DigitalSignService(HttpClient http, ILogger<DigitalSignService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ── Certificate Health ────────────────────────────────────────────────────
    public async Task<CertificateHealth?> GetHealthAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<CertificateHealth>("/api/certificate/health", _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetHealth failed");
            return null;
        }
    }

    // ── Sign Data ─────────────────────────────────────────────────────────────
    public async Task<SignResult?> SignDataAsync(SignRequest request)
    {
        try
        {
            var payload = new
            {
                request.DataToSign,
                request.ReferenceId,
                request.CertThumbprint,
                request.Purpose,
                request.Department,
                request.Remarks
            };

            var resp = await _http.PostAsJsonAsync("/api/sign", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<SignResult>>(body, _jsonOpts);
            return wrapped?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignData failed");
            return null;
        }
    }

    // ── Verify ────────────────────────────────────────────────────────────────
    public async Task<VerifyResult?> VerifySignatureAsync(VerifyRequest request)
    {
        try
        {
            var resp    = await _http.PostAsJsonAsync("/api/sign/verify", request);
            var body    = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<VerifyResult>>(body, _jsonOpts);
            return wrapped?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verify failed");
            return null;
        }
    }

    // ── PDF Sign ──────────────────────────────────────────────────────────────
    public async Task<PdfSignResult?> SignPdfAsync(
        string pdfBase64, string docName, string refId,
        string reason, string location)
    {
        try
        {
            var payload = new
            {
                pdfBase64,
                documentName    = docName,
                referenceId     = refId,
                certThumbprint  = (string?)null,
                reason,
                location,
                signaturePage   = 1,
                signatureX      = 36f,
                signatureY      = 36f,
                signatureWidth  = 200f,
                signatureHeight = 60f
            };

            var resp    = await _http.PostAsJsonAsync("/api/pdf/sign", payload);
            var body    = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<PdfSignResult>>(body, _jsonOpts);
            return wrapped?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignPdf failed");
            return null;
        }
    }

    // ── Audit ─────────────────────────────────────────────────────────────────
    public async Task<AuditPagedResult?> GetMyAuditAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var resp    = await _http.GetAsync($"/api/audit/my?page={page}&pageSize={pageSize}");
            var body    = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<AuditPagedResult>>(body, _jsonOpts);
            return wrapped?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyAudit failed");
            return null;
        }
    }

    public async Task<List<AuditRecord>?> GetAuditByReferenceAsync(string referenceId)
    {
        try
        {
            var resp    = await _http.GetAsync($"/api/audit/reference/{referenceId}");
            var body    = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<AuditRecord>>>(body, _jsonOpts);
            return wrapped?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAuditByReference failed");
            return null;
        }
    }
}
