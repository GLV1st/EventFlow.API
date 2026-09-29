using Azure;
using Azure.Data.Tables;

namespace GateKeeper.Storage.Entities;

public class CateringVendorEntity : ITableEntity
{
    // ============================================================
    // AZURE TABLE KEYS
    // ============================================================

    public string PartitionKey { get; set; } = string.Empty;

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }


    // ============================================================
    // GATEKEEPER
    // ============================================================

    public string EventId { get; set; } = string.Empty;

    public string CateringPassNumber { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;

    public string OrderItemId { get; set; } = string.Empty;


    // ============================================================
    // CONTACT DETAILS
    // ============================================================

    public string ContactNames { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;


    // ============================================================
    // ADDRESS
    // ============================================================

    public string StreetAddress { get; set; } = string.Empty;

    public string AddressLine2 { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string CountyRegion { get; set; } = string.Empty;

    public string Postcode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;


    // ============================================================
    // ONLINE PRESENCE
    // ============================================================

    public string FacebookPage { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;


    // ============================================================
    // FOOD / STALL DETAILS
    // ============================================================

    public string FoodDescription { get; set; } = string.Empty;

    public string PitchStyle { get; set; } = string.Empty;

    public decimal UnitWidth { get; set; }

    public decimal UnitLength { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public int NumberOfPeople { get; set; }


    // ============================================================
    // DOCUMENTS
    // ============================================================
    //
    // These will eventually contain the Azure Blob names/paths.
    // The actual files will NOT be stored in the Table.
    //

    public string VehiclePhoto { get; set; } = string.Empty;

    public string PublicLiabilityInsurance { get; set; } = string.Empty;

    public string ProductLiabilityInsurance { get; set; } = string.Empty;

    public string FoodHygieneCertificates { get; set; } = string.Empty;

    public string GasSafetyCertificate { get; set; } = string.Empty;

    public string FoodStandardsCertificate { get; set; } = string.Empty;

    public string GasInstallationCertificate { get; set; } = string.Empty;

    public string Logo { get; set; } = string.Empty;

    public string EventMenu { get; set; } = string.Empty;


    // ============================================================
    // TERMS
    // ============================================================

    public bool TermsAccepted { get; set; }

    public string SignedBy { get; set; } = string.Empty;

    public DateTimeOffset? SignedDate { get; set; }


    // ============================================================
    // SCANNING / EVENT STATUS
    // ============================================================

    public bool CheckedIn { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }

    public bool CheckedOut { get; set; }

    public DateTimeOffset? CheckedOutAt { get; set; }
}