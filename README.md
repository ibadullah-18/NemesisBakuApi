# nemesisbaku — sifariş 500 düzəlişi

Bu paket yalnız aşağıdakı faylı yeniləyir:

`NemesisBakuApi/Controllers/OrdersController.cs`

## Quraşdırma

ZIP-i `NemesisBakuApi.slnx` faylının yerləşdiyi backend qovluğuna çıxarın və mövcud faylın əvəzlənməsinə icazə verin.

PowerShell-də backend-in solution qovluğunda işlədin:

```powershell
dotnet restore .\NemesisBakuApi\NemesisBakuApi.csproj
dotnet build .\NemesisBakuApi\NemesisBakuApi.csproj
dotnet run --project .\NemesisBakuApi\NemesisBakuApi.csproj
```

Sonra Swagger-də yenidən login olun, yeni token daxil edin və `POST /api/Orders` sorğusunu təkrar göndərin.

## Düzəldilənlər

- `EnableRetryOnFailure` ilə manual transaction toqquşması aradan qaldırıldı.
- Sifariş transaction-u layihədə mövcud olan `ExecuteResilientTransactionAsync` mexanizminə keçirildi.
- Retry zamanı köhnə tracked entity-lərin təkrar yazılması önləndi.
- Silinmiş və ya əlaqəsi pozulmuş səbət məhsulu artıq generic 500 yox, aydın 400 mesajı qaytarır.
