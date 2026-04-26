# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Ayvalık Bank HA-NET** — .NET 9 / ASP.NET Core port of `AyvalikBankHA1` (the Java/Spring Boot hexagonal project). Same use cases, same architectural discipline.

## Commands

```bash
docker compose up -d                         # Postgres on port 5434
dotnet build
dotnet test
dotnet run --project AyvalikBankHA.Api
```

## Architecture

Hexagonal (Ports & Adapters). Every dependency points inward — adapters depend on application, application depends on domain, domain depends on nothing.

```
Domain/Model/                — rich entities, value object Money, enums
Domain/Service/              — TransferDomainService, PasswordValidationService
Domain/Port/In/              — use-case interfaces (driving)
Domain/Port/Out/             — repository + hasher interfaces (driven)

Application/Service/         — implement use-case interfaces;
                               orchestration only (no business rules)
Application/Exception/       — typed application exceptions

Adapter/In/Web/              — controllers (depend on use-case interfaces)
Adapter/Out/Persistence/     — JpaEntities + DbContext + Mappers + Adapters
Adapter/Out/Security/        — BCryptPasswordHasherAdapter

Config/                      — BasicAuthHandler, AdminSeeder
Program.cs                   — composition root: DI wiring lives here
```

## Key Decisions (preserved from the Java sibling)

- **Domain has zero framework imports.** Pure C# only — no EF Core, no ASP.NET Core, no NuGet attributes.
- **`Money` value object.** `record struct Money(decimal Amount, Currency Currency)` with arithmetic + same-currency guards. C#'s `decimal` replaces Java's `BigDecimal`.
- **Rich `Account` entity.** `Deposit`, `Withdraw`, `TransferOut`, `TransferIn`, `Freeze`, `Unfreeze`, `Close` are methods on the entity that enforce invariants and return `Transaction` objects.
- **Ports at every boundary.** `ICreateAccountUseCase` (in), `IAccountRepositoryPort` (out), `IPasswordHasherPort` (out). Application services implement multiple use-case interfaces; controllers depend on those interfaces, not on the service classes.
- **Persistence adapter has its own `JpaEntity` types** + mappers. EF Core types do not cross the persistence boundary.

## Default Admin

`admin@ayvalikbank.dev` / `Admin@123!` (seeded by `AdminSeeder` on first startup)

## Status

Foundational port. Not yet ported from `AyvalikBankHA1`:
- Account types sealed hierarchy (`CheckingAccount`, `SavingsAccount`, `TimeDepositAccount`)
- `AccountState` State pattern
- `CustomerTier` enum + tier-aware fee/limit logic
