using System.Net.Http.Json;
using System.Text.Json;
using DigitalSign.Web.Models;

namespace DigitalSign.Web.Services;

public interface IDigitalSignService
{
    Task<CertificateHealth?> GetHealthAsync();
    Task<SignResult?> SignDataAsync(SignRequest request, string signerUsername);
    Task<VerifyResult?> VerifySignatureAsync(VerifyRequest request);
    Task<PdfSignResult?> SignPdfAsync(string pdfBase64, string docName, string refId,
                                            string reason, string location, string signerUsername);
    Task<AuditPagedResult?> GetMyAuditAsync(int page = 1, int pageSize = 20);
    Task<List<AuditRecord>?> GetAuditByReferenceAsync(string referenceId);
}

public class DigitalSignService : IDigitalSignService
{
    private readonly HttpClient _http;
    private readonly ILogger<DigitalSignService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DigitalSignService(HttpClient http, ILogger<DigitalSignService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<CertificateHealth?> GetHealthAsync()
    {
        try { return await _http.GetFromJsonAsync<CertificateHealth>("/api/certificate/health", _json); }
        catch (Exception ex) { _logger.LogError(ex, "GetHealth failed"); return null; }
    }

    public async Task<SignResult?> SignDataAsync(SignRequest request, string signerUsername)
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
                request.Remarks,
                signerUsername  // ← ส่ง username จาก Web App มาด้วย
            };

            var resp = await _http.PostAsJsonAsync("/api/sign", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<SignResult>>(body, _json);
            return wrapped?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "SignData failed"); return null; }
    }

    public async Task<VerifyResult?> VerifySignatureAsync(VerifyRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/sign/verify", request);
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<VerifyResult>>(body, _json);
            return wrapped?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "Verify failed"); return null; }
    }

    public async Task<PdfSignResult?> SignPdfAsync(
        string pdfBase64, string docName, string refId,
        string reason, string location, string signerUsername)
    {
        try
        {
            var payload = new
            {
                pdfBase64,
                documentName = docName,
                referenceId = refId,
                certThumbprint = (string?)null,
                reason,
                location,
                signerUsername,   // ← ส่ง username จาก Web App มาด้วย
                signaturePage = 1,
                signatureX = 36f,
                signatureY = 36f,
                signatureWidth = 200f,
                signatureHeight = 60f
            };

            var resp = await _http.PostAsJsonAsync("/api/pdf/sign", payload);
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<PdfSignResult>>(body, _json);
            return wrapped?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "SignPdf failed"); return null; }
    }

    public async Task<AuditPagedResult?> GetMyAuditAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/audit/my?page={page}&pageSize={pageSize}");
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<AuditPagedResult>>(body, _json);
            return wrapped?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMyAudit failed"); return null; }
    }

    public async Task<List<AuditRecord>?> GetAuditByReferenceAsync(string referenceId)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/audit/reference/{referenceId}");
            var body = await resp.Content.ReadAsStringAsync();
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<AuditRecord>>>(body, _json);
            return wrapped?.Data;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAuditByRef failed"); return null; }
    }
}
