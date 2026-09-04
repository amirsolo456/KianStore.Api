# API Inventory — deployment/windows-server

## Health
- `GET /api/health` — API + DB health check

## Products
- `GET /api/products?search=&page=&pageSize=` — search/paginate products
- `GET /api/products/barcode/{barcode}` — barcode lookup
- `GET /api/products/{id}` — product by id

## Customers / Parties
- `GET /api/customers?search=&page=&pageSize=` — customer search
- `GET /api/customers/by-mobile/{mobile}` — customer by mobile
- `GET /api/customers/{id}` — customer by id
- `POST /api/customers` — create customer
- `GET /api/reference/parties?search=&idType=&isBuyer=&page=&pageSize=` — generic party lookup, including suppliers already stored in `Taraf`

## Reference data
- `GET /api/reference/warehouses` — active warehouses
- `GET /api/reference/accounts?type=` — cash/bank/account definitions from `CheckDef`
- `GET /api/reference/users` — users and default cash account mapping
- `GET /api/reference/document-types?idSal=` — document types already present in the fiscal year

## Stock
- `GET /api/stock?page=&pageSize=&idAnbar=&idSal=&search=` — stock list
- `GET /api/stock/{kalaId}?idAnbar=&idSal=` — stock for one product
- `GET /api/stock/{kalaId}/check?quantity=&idAnbar=&idSal=` — stock availability check

## Documents
- `POST /api/documents` — generic document creation (existing contract)
- `POST /api/documents/purchase?sanadType=` — purchase document; all lines are forced to incoming (`Bed/Bed2`), so a successful purchase increases stock through the existing `SanadDetail` stock model
- `GET /api/documents/history?idSal=&sanadType=&page=&pageSize=` — document history
- `GET /api/documents/{idSal}/{id}` — document detail

## Discount codes
- `GET /api/discount-codes` — list
- `POST /api/discount-codes` — create
- `PUT /api/discount-codes/{id}` — update
- `DELETE /api/discount-codes/{id}` — delete
- `POST /api/discount-codes/validate` — validate without consuming
- `POST /api/discount-codes/consume` — consume/audit usage

## SMS
- `GET /api/sms/status` — provider configuration status
- `POST /api/sms/send` — send + persist log
- `GET /api/sms/logs?personId=` — SMS history
- `GET /api/sms/templates?activeOnly=` — templates
- `POST /api/sms/templates` — create template
- `PUT /api/sms/templates/{id}` — update template

## Dashboard
- `GET /api/dashboard/summary?idSal=&idAnbar=` — dashboard counters and operational health summary

## Deployment smoke test
From PowerShell on the Windows Server:

```powershell
.\deploy\windows\api-smoke-test.ps1 -BaseUrl 'http://localhost:5069'
```

For IIS/HTTPS, replace the base URL with the final API URL.
