using System.ComponentModel.DataAnnotations;

namespace DigitalSign.Web.Models;

// ── API Response Wrapper ───────────────────────────────────────────────────────
public class ApiResponse<T>
{
    public bool     Success   { get; set; }
    public T?       Data      { get; set; }
    public string?  Message   { get; set; }
    public DateTime Timestamp { get; set; }
}

// ── Sign Data ─────────────────────────────────────────────────────────────────
public class SignRequest
{
    [Required, Display(Name = "ข้อมูลที่ต้องการ Sign")]
    public string DataToSign { get; set; } = string.Empty;

    [Required, Display(Name = "Reference ID")]
    public string ReferenceId { get; set; } = string.Empty;

    public string? CertThumbprint { get; set; }

    [Required, Display(Name = "วัตถุประสงค์")]
    public string Purpose { get; set; } = string.Empty;

    [Display(Name = "แผนก")]
    public string? Department { get; set; }

    [Display(Name = "หมายเหตุ")]
    public string? Remarks { get; set; }
}

public class SignResult
{
    public bool     IsSuccess       { get; set; }
    public string   SignatureBase64 { get; set; } = string.Empty;
    public string   SignedBy        { get; set; } = string.Empty;
    public DateTime SignedAt        { get; set; }
    public string   CertThumbprint  { get; set; } = string.Empty;
    public DateTime CertExpiry      { get; set; }
    public string   DataHash        { get; set; } = string.Empty;
    public string   ReferenceId     { get; set; } = string.Empty;
    public string?  ErrorMessage    { get; set; }
}

// ── Verify ────────────────────────────────────────────────────────────────────
public class VerifyRequest
{
    [Required, Display(Name = "ข้อมูลต้นฉบับ")]
    public string OriginalData    { get; set; } = string.Empty;

    [Required, Display(Name = "Signature (Base64)")]
    public string SignatureBase64 { get; set; } = string.Empty;

    [Required, Display(Name = "Certificate Thumbprint")]
    public string CertThumbprint  { get; set; } = string.Empty;
}

public class VerifyResult
{
    public bool     IsSignatureValid   { get; set; }
    public bool     IsCertificateValid { get; set; }
    public bool     IsOverallValid     { get; set; }
    public string   SignedBy           { get; set; } = string.Empty;
    public DateTime CertExpiry         { get; set; }
    public DateTime VerifiedAt         { get; set; }
    public string?  ErrorMessage       { get; set; }
}

// ── PDF Sign ──────────────────────────────────────────────────────────────────
public class PdfSignRequest
{
    [Required, Display(Name = "ไฟล์ PDF")]
    public IFormFile? PdfFile { get; set; }

    [Required, Display(Name = "ชื่อเอกสาร")]
    public string DocumentName { get; set; } = string.Empty;

    [Required, Display(Name = "Reference ID")]
    public string ReferenceId { get; set; } = string.Empty;

    public string? CertThumbprint { get; set; }

    [Display(Name = "เหตุผล")]
    public string Reason { get; set; } = "Approved";

    [Display(Name = "สถานที่")]
    public string Location { get; set; } = "Bangkok, Thailand";

    public int   SignaturePage   { get; set; } = 1;
    public float SignatureX      { get; set; } = 36f;
    public float SignatureY      { get; set; } = 36f;
    public float SignatureWidth  { get; set; } = 200f;
    public float SignatureHeight { get; set; } = 60f;
}

public class PdfSignResult
{
    public bool     IsSuccess    { get; set; }
    public string   PdfBase64   { get; set; } = string.Empty;
    public string   DocumentName { get; set; } = string.Empty;
    public string   ReferenceId  { get; set; } = string.Empty;
    public string   SignedBy     { get; set; } = string.Empty;
    public DateTime SignedAt     { get; set; }
    public string?  ErrorMessage { get; set; }
}

// ── Certificate ───────────────────────────────────────────────────────────────
public class CertificateHealth
{
    public string   Status         { get; set; } = string.Empty;
    public DateTime Expiry         { get; set; }
    public int      DaysRemaining  { get; set; }
    public string?  Reason         { get; set; }
}

// ── Audit ─────────────────────────────────────────────────────────────────────
public class AuditRecord
{
    public long     Id             { get; set; }
    public string   ReferenceId    { get; set; } = string.Empty;
    public string   SignedByUser   { get; set; } = string.Empty;
    public string   SignedByCert   { get; set; } = string.Empty;
    public DateTime SignedAt       { get; set; }
    public string?  Purpose        { get; set; }
    public string?  Department     { get; set; }
    public string   SignatureType  { get; set; } = string.Empty;
    public bool     IsRevoked      { get; set; }
    public DateTime CertExpiry     { get; set; }
}

public class AuditPagedResult
{
    public List<AuditRecord> Records  { get; set; } = [];
    public int               Total    { get; set; }
    public int               Page     { get; set; }
    public int               PageSize { get; set; }
    public int               Pages    { get; set; }
}
