using KianStore.Api.Common;
using KianStore.Api.DTOs.Documents;

namespace KianStore.Api.Services.Interfaces;

public interface IDocumentService
{
    Task<ApiResponse<DocumentResponse>> CreateAsync(
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<DocumentResponse>> GetAsync(
        int idSal,
        string id,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<DocumentResponse>>> GetHistoryAsync(
        int idSal,
        int sanadType = 12,
        CancellationToken cancellationToken = default);
}
