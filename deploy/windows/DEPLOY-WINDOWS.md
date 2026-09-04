# KianStore.Api — Windows Server deployment

این پوشه برای استقرار `KianStore.Api` روی Windows Server + IIS آماده شده است.

## معماری نهایی

`Flutter -> HTTPS 443 -> IIS -> KianStore.Api -> SQL Server`

SQL Server نباید روی اینترنت عمومی expose شود. فقط IIS/API باید از اینترنت قابل دسترسی باشد.

## پیش‌نیازها

1. Windows Server 2022 یا 2025
2. IIS
3. ASP.NET Core/.NET 10 Hosting Bundle متناسب با TargetFramework پروژه
4. SQL Server و دیتابیس `KianStore_2`
5. دامنه مثل `api.example.ir`
6. گواهی TLS/SSL معتبر برای دامنه

## 1) نصب IIS

PowerShell را با Administrator اجرا کنید:

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
.\deploy\windows\install-iis.ps1
```

سپس Hosting Bundle نسخه متناسب با .NET 10 را نصب کنید و بعد از نصب در صورت درخواست سرور را Restart کنید.

## 2) آماده‌سازی SQL Server

دیتابیس موجود `KianStore_2` را Restore کنید. سپس اسکریپت زیر را یک بار روی همان دیتابیس اجرا کنید:

```text
Database/DiscountCodes.sql
```

این اسکریپت جدول‌های کد تخفیف و SMS را می‌سازد و ستون‌های اضافه‌شده کد تخفیف را برای نصب‌های قبلی migrate می‌کند.

## 3) Connection String و Secrets

Connection String و مشخصات سرویس SMS را داخل Git قرار ندهید. روی سرور، PowerShell را با Administrator اجرا کنید:

```powershell
.\deploy\windows\set-production-env.ps1 `
  -ConnectionString 'Server=localhost;Database=KianStore_2;Trusted_Connection=True;TrustServerCertificate=True;' `
  -SmsSendUrl 'https://YOUR-SMS-PROVIDER-URL' `
  -SmsApiKey 'YOUR-API-KEY' `
  -SmsSender 'YOUR-SENDER' `
  -SmsProvider 'HttpSmsProvider'
```

در صورت استفاده از SQL Server instance متفاوت، مقدار `Server` را مطابق همان instance تنظیم کنید.

## 4) Publish

روی سیستم توسعه یا Build Machine که .NET 10 SDK دارد:

```powershell
.\deploy\windows\publish.ps1
```

خروجی در `deploy/windows/publish` قرار می‌گیرد. این پوشه را به سرور منتقل کنید، مثلاً:

```text
C:\Apps\KianStore.Api\
```

## 5) IIS Application Pool

یک Application Pool با این تنظیمات بسازید:

- Name: `KianStore.Api`
- .NET CLR Version: `No Managed Code`
- Managed pipeline: `Integrated`
- Start Mode: `AlwaysRunning`
- Idle Time-out: `0`
- Enable 32-Bit Applications: `False`

## 6) IIS Site

یک Site با نام `KianStore.Api` بسازید و Physical Path را روی پوشه publish قرار دهید.

برای تست اولیه می‌توانید HTTP را روی یک پورت داخلی تنظیم کنید؛ در حالت نهایی، دامنه را روی HTTPS/443 قرار دهید.

`web.config` موجود در ریشه پروژه برای ASP.NET Core Module تنظیم شده است و در publish همراه برنامه قرار می‌گیرد.

## 7) HTTPS و دامنه

DNS دامنه را به IP عمومی سرور اشاره دهید:

```text
api.example.ir -> SERVER_PUBLIC_IP
```

در IIS گواهی SSL را روی Site با Binding زیر تنظیم کنید:

```text
https / 443 / api.example.ir
```

پورت 443 باید در Firewall باز باشد. پورت 5069 نباید برای اینترنت عمومی باز شود.

## 8) تست سلامت

بعد از راه‌اندازی:

```text
https://api.example.ir/api/health
```

در حالت سالم باید HTTP 200 و پیام آماده بودن API و دیتابیس را دریافت کنید.

## 9) Flutter Release

برنامه Flutter از `--dart-define=API_BASE_URL` پشتیبانی می‌کند. برای Build نهایی:

```powershell
flutter build apk --release --dart-define=API_BASE_URL=https://api.example.ir
```

برای Windows:

```powershell
flutter build windows --release --dart-define=API_BASE_URL=https://api.example.ir
```

تنظیمات دستی داخل برنامه همچنان برای تغییر/تست آدرس API در دسترس است.

## 10) Backup

برای دیتابیس، اسکریپت زیر آماده شده است:

```powershell
.\deploy\windows\backup-kianstore.ps1
```

نمونه:

```powershell
.\deploy\windows\backup-kianstore.ps1 -SqlServer 'localhost' -BackupDirectory 'D:\KianStoreBackups'
```

این Backup نباید تنها نسخه پشتیبان باشد. حداقل یک کپی خارج از همان VPS نگه دارید.

## 11) بعد از Restart سرور

این موارد باید خودکار بالا بیایند:

- SQL Server Service
- IIS
- KianStore.Api Application Pool/Site

یک Restart واقعی سرور را قبل از تحویل نهایی انجام دهید و دوباره `/api/health` را تست کنید.

## نکات امنیتی Production

- SQL Server روی اینترنت عمومی expose نشود.
- SSL certificate معتبر استفاده شود.
- Secrets فقط در Environment Variables یا Secret Store نگهداری شوند.
- Swagger در Production نمایش داده نمی‌شود.
- جزئیات Exception از Health endpoint به کاربر عمومی برگردانده نمی‌شود.
- CORS برای Flutter Web را می‌توان با `Cors:AllowedOrigins` محدود کرد؛ Flutter Native به Origin متکی نیست.
- برای API عمومی، احراز هویت/Authorization و rate limiting باید قبل از استفاده گسترده عملیاتی شوند.
