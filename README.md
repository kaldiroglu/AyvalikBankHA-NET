# Ayvalık Bank HA-NET

A banking application built as a learning project to demonstrate **Hexagonal Architecture (Ports & Adapters)** in **.NET 9 / ASP.NET Core**. .NET counterpart to `AyvalikBankHA1` (Java/Spring Boot).

## Tech Stack

| Concern | Technology |
|---------|-----------|
| Runtime | .NET 9 |
| Framework | ASP.NET Core 9 (Web API) |
| Persistence | EF Core 9 + Npgsql (PostgreSQL) |
| Security | Custom Basic Auth handler |
| Validation | DataAnnotations |
| Testing | xUnit · FluentAssertions · NSubstitute |
| Password hashing | BCrypt.Net-Next |
| Infrastructure | Docker Compose (PostgreSQL on port 5434) |

## Quick Start

```bash
docker compose up -d
dotnet run --project AyvalikBankHA.Api
```

Default admin: `admin@ayvalikbank.dev` / `Admin@123!` (seeded on first startup)

## Project Layout (hexagonal)

```
AyvalikBankHA.Api/
  Domain/
    Model/              — rich entities (Account, Customer, Transaction)
                          + value object Money + enums
    Service/            — TransferDomainService, PasswordValidationService
                          (zero framework imports)
    Port/
      In/               — use-case interfaces (ICreateAccountUseCase, etc.)
      Out/              — repository + hasher port interfaces
  Application/
    Service/            — Customer/AccountApplicationService — implement
                          all use-case interfaces; orchestration only
    Exception/          — typed application exceptions
  Adapter/
    In/Web/             — controllers + DTOs + GlobalExceptionHandler
    Out/Persistence/    — JpaEntities + BankDbContext + Mappers + Adapters
                          (implement the outbound ports)
    Out/Security/       — BCryptPasswordHasherAdapter
  Config/               — BasicAuthHandler, AdminSeeder
  Program.cs            — DI wiring (the composition root)
AyvalikBankHA.Tests/
  *.cs                  — xUnit tests
```

## Architectural notes

- **Rich domain entities** — `Account` owns its invariants (state machine, balance math, currency-mismatch guards) via business methods, not setters
- **Value object `Money`** — `decimal` + `Currency`, with arithmetic and same-currency guard
- **Ports at every boundary** — controllers depend on `ICreateAccountUseCase`, application services on `IAccountRepositoryPort`/`IPasswordHasherPort`
- **Persistence adapter has its own `JpaEntities`** that map to/from domain entities; the EF Core types never cross the persistence boundary
- **Composition root in `Program.cs`** — every concrete implementation is wired here, where the architecture meets the framework

## Endpoints

(Same surface as `AyvalikBankHA1`. Account types, customer tiers, and the State pattern for AccountStatus are not yet ported — see "Next steps" below.)

| Method | Path | Role |
|---|---|---|
| POST | `/api/admin/customers` | ADMIN |
| DELETE | `/api/admin/customers/{id}` | ADMIN |
| GET | `/api/admin/customers` | ADMIN |
| PUT | `/api/admin/settings/transfer-fee` | ADMIN |
| PUT | `/api/admin/accounts/{id}/freeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/unfreeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/close` | ADMIN |
| PUT | `/api/customers/{id}/password` | CUSTOMER |
| POST | `/api/accounts?ownerId=` | CUSTOMER |
| GET | `/api/customers/{id}/accounts` | CUSTOMER |
| GET | `/api/accounts/{id}/balance` | CUSTOMER |
| POST | `/api/accounts/{id}/deposit` | CUSTOMER |
| POST | `/api/accounts/{id}/withdraw` | CUSTOMER |
| POST | `/api/accounts/{id}/transfer` | CUSTOMER |
| GET | `/api/accounts/{id}/transactions` | CUSTOMER |

## Next steps (not yet ported from `AyvalikBankHA1`)

- **Account types** sealed hierarchy (CHECKING / SAVINGS / TIME_DEPOSIT) with overdraft, monthly interest accrual, time-deposit maturation
- **State pattern** for `AccountStatus` (`AccountState` interface + `ActiveState`/`FrozenState`/`ClosedState` singletons)
- **Customer tiers** (STANDARD / PREMIUM / PRIVATE) with fee multiplier and per-transaction caps
- More tests (currently 19; Java sibling has 176)
- Integration tests with `WebApplicationFactory`
