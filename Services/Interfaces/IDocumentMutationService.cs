using KianStore.Api.Common;
using KianStore.Api.DTOs.Documents;

namespace KianStore.Api.Services.Interfaces;

public interface IDocumentMutationService
{
    Task<ApiResponse<DocumentResponse>> UpdatePartnerSaleAsync(
        int idSal,
        string id,
        CreateDocumentRequest request,
        int? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DocumentResponse>> DeletePartnerSaleAsync(
        int idSal,
        string id,
        int? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DocumentResponse>> DeletePurchaseAsync(
        int idSal,
        string id,
        int? currentUserId,
        CancellationToken cancellationToken = default);
}
