using KianStore.Api.Common;
using KianStore.Api.Models.Orders;

namespace KianStore.Api.Services.Interfaces;

public interface ISanadService
{
    Task<ApiResponse<string>> ConvertToSanadAsync(long mobileOrderId);
}
