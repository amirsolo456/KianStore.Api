# KianStore.Api — Windows Server Production

این شاخه فقط برای اجرای Backend روی Windows Server نگهداری می‌شود.

## Runtime

- ASP.NET Core / .NET 10
- Windows x64
- SQL Server instance: `.\SQL2025`
- Database: `KianStore_2`
- Default production API port: `5000`

## اجرای مستقیم روی Server

1. بسته Release ساخته‌شده توسط GitHub Actions را دریافت و Extract کنید.
2. تنظیمات `appsettings.Production.json` را بررسی کنید.
3. API را با `KianStore.Api.exe` اجرا کنید.
4. برای تست:

```powershell
Invoke-WebRequest http://127.0.0.1:5000/api/health
```

## نکته مهم

منطق ثبت `Sanad` از Stored Procedureهای اصلی دیتابیس استفاده می‌کند و نباید با `MAX()+1` در API بازنویسی شود.

- `AndCrFaktor`
- `AndCrFaktorKala`
- `SetFaktorFinal`

## APIهای اصلی

- `GET /api/health`
- `GET /api/products`
- `GET /api/products/barcode/{barcode}`
- `GET /api/customers/{mobile}`
- `GET /api/customers/search`
- `GET /api/stock`
- `GET /api/stock/{kalaId}`
- `GET /api/stock/{kalaId}/check`
- `GET /api/reference/warehouses`
- `GET /api/reference/accounts`
- `GET /api/reference/parties`
- `GET /api/reference/users`
- `GET /api/reference/document-types`
- `POST /api/documents`
- `POST /api/documents/purchase?sanadType={type}`
- `GET /api/documents/history`
- `GET /api/documents/{idSal}/{id}`
- `GET /api/discount-codes`
- `POST /api/discount-codes`
- `POST /api/discount-codes/validate`
- `POST /api/discount-codes/consume`
- `GET /api/sms/logs`
- `POST /api/sms/send`
