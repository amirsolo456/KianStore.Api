using KianStore.Api.DTOs.Documents;

namespace KianStore.Api.Services.Interfaces;

public interface ISanadAuditService
{
    Task RecordAsync(DocumentResponse document, int? currentUserId, string action, string description, CancellationToken cancellationToken = default);
}
