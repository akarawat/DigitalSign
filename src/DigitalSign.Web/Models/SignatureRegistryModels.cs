using System.ComponentModel.DataAnnotations;

namespace DigitalSign.Web.Models;

// ── ViewModel สำหรับ Form ─────────────────────────────────────────────────────
public class RegisterSignatureViewModel
{
    [Required, Display(Name = "Full Name (Thai)")]
    public string FullNameTH { get; set; } = string.Empty;

    [Required, Display(Name = "Full Name (English)")]
    public string FullNameEN { get; set; } = string.Empty;

    [Display(Name = "Position")]
    public string? Position { get; set; }

    [Display(Name = "Department")]
    public string? Department { get; set; }

    [Display(Name = "Email")]
    [EmailAddress]
    public string? Email { get; set; }

    [Required, Display(Name = "Signature Image")]
    public IFormFile? SignatureFile { get; set; }
}

// ── DTOs จาก API ─────────────────────────────────────────────────────────────
public class SignatureRegistryDto
{
    public long      Id                   { get; set; }
    public string    SamAccountName       { get; set; } = string.Empty;
    public string    FullNameTH           { get; set; } = string.Empty;
    public string    FullNameEN           { get; set; } = string.Empty;
    public string?   Position             { get; set; }
    public string?   Department           { get; set; }
    public string?   Email                { get; set; }
    public string    ImageMimeType        { get; set; } = "image/png";
    public string?   ImageFileName        { get; set; }
    public int?      ImageSizeBytes       { get; set; }
    public bool      IsApproved           { get; set; }
    public string?   ApprovedBy           { get; set; }
    public DateTime? ApprovedAt           { get; set; }
    public DateTime  RegisteredAt         { get; set; }
    public DateTime? UpdatedAt            { get; set; }
    public string?   SignatureImageBase64 { get; set; }
}

public class SignatureRegistryListDto
{
    public List<SignatureRegistryDto> Records  { get; set; } = [];
    public int                        Total    { get; set; }
    public int                        Page     { get; set; }
    public int                        PageSize { get; set; }
    public int                        Pages    { get; set; }
}
