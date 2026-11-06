using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class TraderEntity : ITableEntity
{
    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    public string PartitionKey { get; set; } = "";

    public string RowKey { get; set; } = "";


    // =====================================
    // EVENT
    // =====================================

    // Links this trader to the existing Events table.
    // For COP Carnival this will be COP-CARN.
    public string EventId { get; set; } = "";

    public string EventName { get; set; } = "";


    // =====================================
    // TRADER PASS
    // =====================================

    public string TraderPassNumber { get; set; } = "";

    public string TraderType { get; set; } = "Trader";


    // =====================================
    // WOOCOMMERCE
    // =====================================

    public string OrderId { get; set; } = "";

    public string OrderItemId { get; set; } = "";

    public string VariationId { get; set; } = "";

    public string VariationName { get; set; } = "";

    public double Price { get; set; }


    // =====================================
    // TRADER / CONTACT DETAILS
    // =====================================

    public string ContactName { get; set; } = "";

    public string CompanyName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string VehicleRegistration { get; set; } = "";


    // =====================================
    // STALL DETAILS
    // =====================================

    public string StallDescription { get; set; } = "";

    public string CharityNumber { get; set; } = "";

    public string PitchType { get; set; } = "";

    public string PitchSize { get; set; } = "";

    public int NumberOfPeople { get; set; }

    // Number of people from this pass currently on site.
    public int PeopleOnSite { get; set; } = 0;


    // =====================================
    // TERMS & CONDITIONS
    // =====================================

    public bool TermsAccepted { get; set; } = false;

    public string TermsAcceptedBy { get; set; } = "";

    public string TermsAcceptedDate { get; set; } = "";


    // =====================================
    // PASS / DOCUMENTS
    // =====================================

    // Reference to the generated Trader Pass PDF.
    public string PassPdfUrl { get; set; } = "";

    // Reference to the Stall Terms & Conditions PDF.
    public string TermsPdfUrl { get; set; } = "";


    // =====================================
    // DOCUMENT UPLOAD
    // =====================================

    // Secure token used by the Trader Stall
    // document upload page.
    public string DocumentUploadToken { get; set; } = "";

    // Expiry time for the secure document
    // upload token.
    public DateTimeOffset? DocumentUploadTokenExpiresAt { get; set; }

    // Stored reference/path for the uploaded
    // Risk Assessment document.
    public string RiskAssessment { get; set; } = "";

    // Stored reference/path for the uploaded
    // Public Liability Insurance document.
    public string PublicLiabilityInsurance { get; set; } = "";

    // Stored reference/path for the uploaded
    // Food Handling Certificate.
    public string FoodHandlingCertificate { get; set; } = "";


    // =====================================
    // EMAIL
    // =====================================

    public bool EmailSent { get; set; } = false;

    public DateTime? EmailSentAt { get; set; }


    // =====================================
    // ATTENDANCE
    // =====================================

    public bool CheckedIn { get; set; } = false;

    public DateTime? CheckedInAt { get; set; }

    public bool CheckedOut { get; set; } = false;

    public DateTime? CheckedOutAt { get; set; }


    // =====================================
    // AUDIT
    // =====================================

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Updated { get; set; } = DateTime.UtcNow;


    // =====================================
    // AZURE
    // =====================================

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}