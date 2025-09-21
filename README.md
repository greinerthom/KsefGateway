# KSeF Gateway

Minimalne API .NET 8/9 do komunikacji z **KSeF (Krajowy System e-Faktur)**.

Projekt udostępnia prostą bramkę, która:
- zarządza logowaniem do KSeF,
- obsługuje tokeny techniczne,
- pozwala sprawdzić limity,
- udostępnia publiczne klucze,
- umożliwia wysyłanie testowych faktur XML.

---

## 🚀 Wymagania

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Konto i token techniczny w [KSeF](https://ksef-test.mf.gov.pl/)
- Git

---

## 🔧 Konfiguracja

Ustaw dane w pliku `appsettings.Development.json`:

```json
{
  "Ksef": {
    "BaseUrl": "https://ksef-test.mf.gov.pl",
    "TechnicalToken": "TU_WKLEJ_SWÓJ_TOKEN",
    "Tin": "1234567890"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [ { "Name": "Console" } ],
    "Enrich": [ "FromLogContext" ]
  }
}
