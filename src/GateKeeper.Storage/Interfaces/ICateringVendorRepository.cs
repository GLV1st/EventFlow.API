using GateKeeper.Storage.Entities;

namespace GateKeeper.Storage.Interfaces;

public interface ICateringVendorRepository
{
    Task AddCateringVendorAsync(
        CateringVendorEntity cateringVendor
    );

    Task<CateringVendorEntity?> GetCateringVendorAsync(
        string eventId,
        string cateringPassNumber
    );

    Task<List<CateringVendorEntity>> GetCateringVendorsByEventAsync(
        string eventId
    );

    Task UpdateCateringVendorAsync(
        CateringVendorEntity cateringVendor
    );
}