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
- **Sealed `Account` hierarchy.** `abstract class Account` + `sealed class CheckingAccount / SavingsAccount / TimeDepositAccount` mirror Java's sealed permits. Each subtype owns its own `Deposit / Withdraw / TransferOut`. `TransferIn` is sealed on the base.
- **State pattern for account status.** `AccountState` abstract + `ActiveState / FrozenState / ClosedState` sealed singletons (private ctor + public static `Instance`). `Account.Status` delegates to `State`; transitions return the next state. `CLOSED` is terminal.
- **`CustomerTier` enum + extension-method policy data.** `STANDARD / PREMIUM / PRIVATE` with `FeeMultiplier()` (1.0×/0.5×/0.0×) and per-transaction caps (5k/50k/unlimited transfer; 5k/25k/unlimited withdrawal). Same-customer transfers are always free.
- **Ports at every boundary.** `IOpenCheckingAccountUseCase` etc. (in), `IAccountRepositoryPort` (out), `IPasswordHasherPort` (out). Application services implement multiple use-case interfaces; controllers depend on those interfaces, not on the service classes.
- **Persistence adapter has its own `JpaEntity` types** + mappers. EF Core types do not cross the persistence boundary. Account JpaEntity carries an `AccountType` discriminator + 7 nullable type-specific columns; the mapper switches on `AccountType` to construct the right subtype.

## Domain Operations

- **Checking:** overdraft-aware `Withdraw / TransferOut` (negative balance allowed up to `OverdraftLimit`).
- **Savings:** `AccrueInterest(year, month)` adds monthly interest = `Balance * AnnualRate / 12`; rejects double-accrual; works on `FROZEN`, rejected on `CLOSED`.
- **Time deposit:** principal locked — `Deposit / TransferOut` always throw; `Withdraw` rejected until `Mature(today)` credits maturity interest.

## Default Admin

`admin@ayvalikbank.dev` / `Admin@123!` (seeded by `AdminSeeder` on first startup, with `Tier = STANDARD`)
