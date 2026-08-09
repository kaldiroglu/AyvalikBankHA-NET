# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Ayvalık Bank HA-NET** — .NET 9 / ASP.NET Core port of `AyvalikBankHA-JAVA` (the Java/Spring Boot hexagonal project). Same use cases, same architectural discipline.

## Cross-repository invariants

This repo is one of six (hexagonal + layered × Java/.NET/Python) that must stay **functionally
identical**. `AyvalikBankContractTests` is one black-box HTTP suite run against all six, and CI runs
it on every push. Before changing any endpoint, status code, field name or JSON shape, check whether
the change belongs in all six.

- Wire format is **camelCase**; validation failures are **400** (not FastAPI's default 422).
- Enums travel as **strings** (`"USD"`), never numbers.
- Refactoring write-ups live in `Refactorings.md`; the Java hexagonal repo is the reference.
- The suite is 29 tests; all six implementations currently pass 29/29.

## Commands

```bash
# Browsable API docs once the app is running: /swagger
# Shared contract suite (from AyvalikBankContractTests):
#   BANK_BASE_URL=http://localhost:5080 pytest tests/

docker compose up -d                         # Postgres on port 5434, database ayvalikbank_ha_net
/Users/akin/.dotnet/dotnet build
/Users/akin/.dotnet/dotnet test
# No launchSettings.json here — name the port explicitly: --urls http://localhost:5080
/Users/akin/.dotnet/dotnet run --project AyvalikBankHA.Api --urls http://localhost:5080
```

## Environment gotchas

- **`dotnet` on PATH is SDK 9.0.202** and fails `NETSDK1045` on net10.0 — build and run with
  `/Users/akin/.dotnet/dotnet`.
- **No in-memory run configuration:** EF Core's InMemory provider is test-only, so the app needs a
  real PostgreSQL (`docker compose up -d`). Override the connection string with the
  `ConnectionStrings__Default` environment variable.
- **`[Range(typeof(decimal), "0.01", ...)]` parses its bounds with the current culture** — always pass
  `ParseLimitsInInvariantCulture = true`, or every request fails on a comma-decimal locale.
- **Docker Desktop** stops on its own; if compose fails with a socket error, `open -a Docker` and wait.

## Ports and databases

This repo: app **5080**, PostgreSQL **5434**, database `ayvalikbank_ha_net`.

All six repos take distinct application and PostgreSQL ports so every one can run at the same
time; `README.md` carries the full table. **5432 is deliberately unused** — it is the default for
a native PostgreSQL (Postgres.app, Homebrew), and an application pointed at it connects to that
server instead of its own container, with no error to say so. Every compose service sets an
explicit `container_name`: without one Compose derives a name from the directory, and a container
can outlive the checkout that defined it while still holding its port.

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

## Design Decisions (2026-08 hardening pass)

- **Ownership authorization**: every customer-facing command carries the caller's id, taken from the authenticated principal — never from a route or query parameter. Transfers check the **source only**; the target is deliberately unchecked. Opening an account takes no owner id: the caller is the owner. See `Refactorings.md`.
- **Optimistic locking**: accounts carry a version token. A conflict surfaces at commit and maps to HTTP 409.
- **Domain refusal vocabulary**: the domain refuses through `AccountRuleViolation` and four subtypes; the application layer translates by **type**, never by matching on the exception message.
- **`TransactionAmount` vs `Money`**: `Money` is signed (overdraft), so it cannot enforce positivity. `TransactionAmount` is strictly positive by construction and types the command surface. Balances, fees and recorded transaction amounts stay `Money` — zero is legal for all three.
- **Actor-shaped ports**: driving ports are grouped by *actor × subject*, not one per method. A port is one conversation with one kind of outside actor.

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
