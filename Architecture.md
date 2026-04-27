# Architecture — Ayvalık Bank HA-NET

A .NET 9 / ASP.NET Core port of `AyvalikBankHA-JAVA`, organized as **Hexagonal Architecture (Ports & Adapters)**. Every dependency points inward toward the domain.

---

## Dependency Rule

```
Adapter/In/Web ──▶ Domain/Port/In ◀── Application/Service ──▶ Domain/Port/Out ◀── Adapter/Out/Persistence
                          ▲                       │                                          │
                          │                       ▼                                          │
                          └───────────────── Domain/Model + Domain/Service ◀─────────────────┘
```

- **Adapters** depend on **ports** (interfaces).
- **Application services** orchestrate; they depend only on ports and the domain.
- **Domain** has no `EntityFrameworkCore`, `AspNetCore`, or NuGet attributes — pure C#.

---

## Project Layout

```
AyvalikBankHA.Api/
  Domain/
    Model/                  — sealed Account hierarchy, AccountState (State pattern),
                              Money, Customer, Transaction, enums (CustomerTier,
                              AccountStatus, AccountType, TransactionType, Currency)
    Service/                — TransferDomainService, PasswordValidationService
                              (zero framework imports)
    Port/
      In/IUseCases.cs       — 21 driving use-case interfaces
      Out/IRepositoryPorts  — driven repository + hasher interfaces
  Application/
    Service/                — CustomerApplicationService, AccountApplicationService
                              (each implements multiple use-case interfaces)
    Exception/AppExceptions — InvalidAccountOperationException,
                              LimitExceededException, NotFoundException, etc.
  Adapter/
    In/Web/                 — Customer/Account/AdminController, DTOs,
                              GlobalExceptionHandler (IExceptionHandler)
    Out/Persistence/
      Entity/JpaEntities    — CustomerJpaEntity, AccountJpaEntity (with
                              Type discriminator + nullable type-specific
                              columns), TransactionJpaEntity, SettingsJpaEntity
      BankDbContext         — EF Core DbContext, OnModelCreating column maps
      Mapper/Mappers        — Domain ⇄ JpaEntity (subtype-aware switch)
      Adapter/RepositoryAdapters — implement domain Out ports
    Out/Security/           — BCryptPasswordHasherAdapter
  Config/                   — BasicAuthHandler, AdminSeeder
  Program.cs                — composition root: DI wiring lives here
AyvalikBankHA.Tests/        — xUnit + FluentAssertions + NSubstitute (66 tests)
```

---

## Key Design Decisions

### Sealed `Account` hierarchy (mirroring Java's `sealed permits`)

```csharp
public abstract class Account { /* base invariants */ }
public sealed class CheckingAccount : Account { /* overdraft logic */ }
public sealed class SavingsAccount : Account { /* AccrueInterest */ }
public sealed class TimeDepositAccount : Account { /* Mature */ }
```

Each subtype overrides `Deposit / Withdraw / TransferOut` with its own rules. `TransferIn` is sealed on the base. C# lacks Java's `sealed permits` keyword, but `abstract class` + `sealed class` derivatives express the same invariant: the persistence mapper's `switch` is exhaustive over the three known subtypes.

### State pattern for `AccountStatus`

```csharp
public abstract class AccountState {
    public abstract AccountStatus Status { get; }
    public abstract AccountState Freeze();
    public abstract AccountState Unfreeze();
    public abstract AccountState Close();
    public abstract void RequireOperable();
    public virtual bool IsTerminal => false;
}

public sealed class ActiveState : AccountState {
    public static readonly ActiveState Instance = new();
    private ActiveState() { }
    /* ... */
}
// FrozenState, ClosedState analogous
```

Each state owns its own valid transitions and operability check. Stateless singletons via private ctor + public `Instance`. `Account.Status` delegates to `State`. `CLOSED` is terminal — `IsTerminal` returns `true`.

### `CustomerTier` with extension-method policy data

```csharp
public enum CustomerTier { STANDARD, PREMIUM, PRIVATE }

public static class CustomerTierPolicy
{
    public static decimal FeeMultiplier(this CustomerTier t) => /* 1.0/0.5/0.0 */;
    public static decimal? MaxPerTransfer(this CustomerTier t) => /* 5k/50k/null */;
    public static decimal? MaxPerWithdrawal(this CustomerTier t) => /* 5k/25k/null */;
}
```

Policy data lives next to the enum, but the enum stays a value type that crosses the persistence boundary cleanly (mapped as `string` in the column).

### Ports at every boundary

- **Driving (in) ports** — one interface per use case (e.g., `IOpenCheckingAccountUseCase`, `IDepositMoneyUseCase`, `IChangeCustomerTierUseCase`). Controllers depend on these interfaces, not on the application service classes. 21 use-case interfaces total.
- **Driven (out) ports** — `ICustomerRepositoryPort`, `IAccountRepositoryPort`, `ITransactionRepositoryPort`, `ISettingsRepositoryPort`, `IPasswordHasherPort`. The persistence + security adapters implement them.

### Application services implement multiple use-case interfaces

```csharp
public class AccountApplicationService :
    IOpenCheckingAccountUseCase, IOpenSavingsAccountUseCase,
    IOpenTimeDepositAccountUseCase, IDepositMoneyUseCase,
    IWithdrawMoneyUseCase, ITransferMoneyUseCase,
    IGetBalanceUseCase, IGetTransactionsUseCase, IListAccountsUseCase,
    IFreezeAccountUseCase, IUnfreezeAccountUseCase, ICloseAccountUseCase,
    IAccrueInterestUseCase, IMatureTimeDepositUseCase,
    ISetTransferFeeUseCase
{ /* ... */ }
```

DI registers each application service once concretely, then once per use-case interface using `as IXxxUseCase`:

```csharp
builder.Services.AddScoped<AccountApplicationService>();
builder.Services.AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IDepositMoneyUseCase);
// ... and so on for each use-case interface
```

### Persistence adapter has its own JPA-style entities

`CustomerJpaEntity`, `AccountJpaEntity`, `TransactionJpaEntity`, `SettingsJpaEntity`, `PasswordHistoryJpaEntity` live inside the persistence adapter. **They never cross the persistence boundary.** `AccountMapper.ToDomain` switches on `AccountType` to construct `CheckingAccount` / `SavingsAccount` / `TimeDepositAccount`; `ToJpa` uses a pattern-matching switch expression to write the right type-specific columns.

### Account table schema (single-table inheritance)

```
accounts
  id              uuid PK
  owner_id        uuid FK
  currency        text
  balance         numeric(19,2)
  status          text                 -- ACTIVE | FROZEN | CLOSED
  type            text                 -- CHECKING | SAVINGS | TIME_DEPOSIT (discriminator)
  overdraft_limit numeric(19,2) NULL   -- CHECKING only
  interest_rate   numeric(19,2) NULL   -- SAVINGS, TIME_DEPOSIT
  last_accrual    date          NULL   -- SAVINGS only
  principal       numeric(19,2) NULL   -- TIME_DEPOSIT only
  opened_on       date          NULL   -- TIME_DEPOSIT only
  maturity_date   date          NULL   -- TIME_DEPOSIT only
  matured         boolean       NULL   -- TIME_DEPOSIT only
```

### Cross-cutting

- **Authentication** — HTTP Basic via custom `BasicAuthHandler : AuthenticationHandler<BasicAuthOptions>` (the `idunno.Authentication.Basic` package is incompatible with `net9.0`).
- **Error handling** — `GlobalExceptionHandler : IExceptionHandler` (.NET 8+ idiom) maps domain/application exceptions to `ProblemDetails` with the right HTTP status: 404 not-found, 401 invalid-credentials, 422 invalid-account-operation / limit-exceeded / insufficient-funds, 400 fallback.
- **Composition root** — `Program.cs`. Every concrete is wired here; nothing else uses `AddScoped` / `AddSingleton`.

---

## Request Flow Examples

### `POST /api/accounts/checking?ownerId={id}`

```
HTTP request
  → AccountController.CreateChecking
      → IOpenCheckingAccountUseCase.OpenCheckingAsync          (port)
        → AccountApplicationService.OpenCheckingAsync          (orchestration)
          → ICustomerRepositoryPort.FindByIdAsync              (out port)
            → CustomerPersistenceAdapter                       (adapter)
              → BankDbContext                                  (EF Core)
          → CheckingAccount.Open(ownerId, currency, overdraft) (domain factory)
          → IAccountRepositoryPort.SaveAsync                   (out port)
            → AccountPersistenceAdapter
              → AccountMapper.ToJpa(domain) ─── pattern match
              → BankDbContext.SaveChangesAsync
      ← AccountResponse.From(account) (DTO)
HTTP 201 Created + JSON
```

### `POST /api/accounts/{id}/transfer` (cross-customer, with fee)

```
HTTP request
  → AccountController.Transfer
      → ITransferMoneyUseCase.TransferAsync
        → AccountApplicationService.TransferAsync
          → IAccountRepositoryPort.FindByIdAsync (source + target)
          → ICustomerRepositoryPort.FindByIdAsync (source customer for tier)
          → ISettingsRepositoryPort.GetTransferFeePercentAsync
          → TransferDomainService.RequireTransferWithinLimit (caps by tier)
          → TransferDomainService.CalculateFee(amount, sameCustomer, feePct, tier)
          → source.TransferOut(amount, fee, target.Id) — domain method
          → target.TransferIn(amount, source.Id)
          → IAccountRepositoryPort.SaveAsync (both)
          → ITransactionRepositoryPort.SaveAsync (both transactions)
HTTP 200 OK
```

---

## Tech Stack

| Concern          | Technology                                  |
|------------------|---------------------------------------------|
| Runtime          | .NET 9                                      |
| Web              | ASP.NET Core 9 Web API                      |
| Persistence      | EF Core 9 + Npgsql (PostgreSQL)             |
| Auth             | Custom `AuthenticationHandler<>` (Basic)    |
| Validation       | DataAnnotations on request records          |
| Testing          | xUnit · FluentAssertions · NSubstitute      |
| Password hashing | BCrypt.Net-Next                             |
| Local infra      | Docker Compose (Postgres on `5434`)         |

---

## Comparison to the Java Sibling (HA1)

| Aspect | Java HA1 | .NET HA-NET |
|---|---|---|
| Sealed account hierarchy | `sealed abstract class Account permits ...` | `abstract class Account` + `sealed class CheckingAccount : Account` etc. |
| State pattern | `sealed interface AccountState permits ...` | `abstract class AccountState` + `sealed` derivatives with private ctor |
| Money | `record Money(BigDecimal, Currency)` | `readonly record struct Money(decimal, Currency)` |
| Use-case interfaces | `interface CreateCustomerUseCase` etc. | `interface ICreateCustomerUseCase` etc. |
| Persistence adapter | JPA entities + Spring Data | JpaEntities + EF Core DbContext |
| Domain services as beans | `@Bean` in `SecurityConfig` | `AddSingleton<>` in `Program.cs` |
| Auth | Spring Security HTTP Basic | `AuthenticationHandler<>` HTTP Basic |
| Global error handler | `@ControllerAdvice` | `IExceptionHandler` (.NET 8+) |
