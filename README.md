# Ayvalık Bank HA-NET

A banking application built as a learning project to demonstrate **Hexagonal Architecture (Ports & Adapters)** in **.NET 9 / ASP.NET Core**. .NET counterpart to `AyvalikBankHA-JAVA` (Java/Spring Boot).

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

- **Sealed `Account` hierarchy** — `abstract class Account` + `sealed class CheckingAccount / SavingsAccount / TimeDepositAccount` mirror Java's sealed permits. Each subtype owns its own deposit/withdraw/transferOut behavior.
- **State pattern for status** — `AccountState` abstract + `ActiveState / FrozenState / ClosedState` sealed singletons (private ctor + public static `Instance`). `Account.Status` delegates to `State`; `CLOSED` is terminal.
- **Customer tiers** — `STANDARD / PREMIUM / PRIVATE` with extension-method policy data: `FeeMultiplier()` (1.0×/0.5×/0.0×) and per-transaction caps (5k/50k/unlimited transfer; 5k/25k/unlimited withdrawal). Same-customer transfers are always free.
- **Value object `Money`** — `decimal` + `Currency`, with arithmetic and same-currency guard.
- **Ports at every boundary** — controllers depend on use-case interfaces; application services on `IAccountRepositoryPort`/`IPasswordHasherPort`.
- **Persistence adapter has its own `JpaEntities`** that map to/from domain entities; the EF Core types never cross the persistence boundary. Account JpaEntity carries an `AccountType` discriminator + 7 nullable type-specific columns; the mapper switches on `AccountType` to construct the right subtype.
- **Composition root in `Program.cs`** — every concrete implementation is wired here, where the architecture meets the framework.

## Endpoints

| Method | Path | Role |
|---|---|---|
| POST | `/api/admin/customers` | ADMIN |
| DELETE | `/api/admin/customers/{id}` | ADMIN |
| GET | `/api/admin/customers` | ADMIN |
| PUT | `/api/admin/settings/transfer-fee` | ADMIN |
| PUT | `/api/admin/accounts/{id}/freeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/unfreeze` | ADMIN |
| PUT | `/api/admin/accounts/{id}/close` | ADMIN |
| PUT | `/api/admin/customers/{id}/tier` | ADMIN |
| PUT | `/api/admin/accounts/{id}/accrue-interest` | ADMIN |
| PUT | `/api/admin/accounts/{id}/mature` | ADMIN |
| PUT | `/api/customers/{id}/password` | CUSTOMER |
| POST | `/api/accounts/checking?ownerId=` | CUSTOMER |
| POST | `/api/accounts/savings?ownerId=` | CUSTOMER |
| POST | `/api/accounts/time-deposit?ownerId=` | CUSTOMER |
| GET | `/api/customers/{id}/accounts` | CUSTOMER |
| GET | `/api/accounts/{id}/balance` | CUSTOMER |
| POST | `/api/accounts/{id}/deposit` | CUSTOMER |
| POST | `/api/accounts/{id}/withdraw` | CUSTOMER |
| POST | `/api/accounts/{id}/transfer` | CUSTOMER |
| GET | `/api/accounts/{id}/transactions` | CUSTOMER |

## Test coverage

66 unit tests (xUnit + FluentAssertions), covering:
- Money arithmetic and currency guards
- Password validation
- Account state transitions (State pattern)
- Per-subtype invariants: checking overdraft, savings monthly accrual, time-deposit lock + maturation
- TransferDomainService tier-aware fees and per-transaction limits
- Customer tier mutation
