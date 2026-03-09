using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IContactService
{
    Task<ApiResponse<PagedResult<Contact>>> GetContactsAsync(int companyId, ContactQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<Contact>> GetContactByIdAsync(int companyId, long contactId, CancellationToken ct = default);
    Task<ApiResponse<Contact>> CreateContactAsync(int companyId, ContactUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<Contact>> UpdateContactAsync(int companyId, long contactId, ContactUpsertRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteContactAsync(int companyId, long contactId, CancellationToken ct = default);
    Task<ApiResponse<int>> ImportContactsAsync(int companyId, ContactImportRequest request, CancellationToken ct = default);
}
