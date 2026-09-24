namespace KianStore.Api.DTOs.StockTransfers;

public sealed class StockTransferRequest
{
    public int IdSal { get; init; } = 1405;
    public int SourceAnbarId { get; init; }
    public int DestinationAnbarId { get; init; }
    public string SabtDate { get; init; } = string.Empty;
    public string? Note { get; init; }
    public List<StockTransferItemRequest> Items { get; init; } = [];
}


public sealed class UpdateStockTransferRequest
{
    public int IdSal { get; init; } = 1405;
    public int SourceAnbarId { get; init; }
    public int DestinationAnbarId { get; init; }
    public string SabtDate { get; init; } = string.Empty;
    public string? Note { get; init; }
    public List<StockTransferItemRequest> Items { get; init; } = [];
}

public sealed class StockTransferItemRequest
{
    public string IdKala { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

public sealed class StockTransferWarehouseResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class StockTransferInventoryResponse
{
    public string IdKala { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Stock { get; init; }
}

public sealed class StockTransferResponse
{
    public int IdSal { get; init; }
    public string Id { get; init; } = string.Empty;
    public int SourceAnbarId { get; init; }
    public int DestinationAnbarId { get; init; }
    public int ItemCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class StockTransferHistoryItemResponse
{
    public string IdKala { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

public sealed class StockTransferHistoryResponse
{
    public int IdSal { get; init; }
    public string Id { get; init; } = string.Empty;
    public bool IsBookmarked { get; init; }
    public int IdFaktor { get; init; }
    public string SabtDate { get; init; } = string.Empty;
    public string? Note { get; init; }
    public int SourceAnbarId { get; init; }
    public string SourceAnbarName { get; init; } = string.Empty;
    public int DestinationAnbarId { get; init; }
    public string DestinationAnbarName { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public List<StockTransferHistoryItemResponse> Items { get; init; } = [];
}

public sealed class SetStockTransferBookmarkRequest
{
    public bool IsBookmarked { get; init; }
}


public sealed class StockTransferProductWarehouseInventoryResponse
{
    public int IdAnbar { get; init; }
    public string AnbarName { get; init; } = string.Empty;
    public decimal Stock { get; init; }
}
