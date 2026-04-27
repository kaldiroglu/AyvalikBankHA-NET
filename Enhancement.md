# Enhancement Walkthrough — Daily Withdrawal Limits

A teaching example: add **per-account, per-calendar-day cumulative withdrawal limits** to the project, then study where the change lands.

This file describes the feature in this codebase (.NET 9 / ASP.NET Core / hexagonal). Sibling files in `AyvalikBankHA-JAVA`, `AyvalikBankLA-JAVA`, `AyvalikBankLA-NET`, `AyvalikBankHA-Python`, `AyvalikBankLA-Python` describe the same feature in their respective stacks so the impact can be compared side by side.

---

## The Feature

- Each `Account` carries a nullable `DailyWithdrawalLimit: Money?`. Null = use a tier-derived default.
- Cumulative withdrawals (direct withdraw + the debit side of transfers) on a single UTC calendar day must not exceed that limit.
- Admin can set/clear the limit per account: `PUT /api/admin/accounts/{id}/daily-limit`.
- Reset at UTC midnight.
- A separate, additive constraint — the existing per-transaction tier caps still apply.

---

## Why this feature is good for teaching

It crosses every layer: model, persistence, business rule, API, validation. It introduces **state that lives across transactions** ("today's running total"), which is the interesting persistence question. And it sits at the intersection of `Customer`, `Account`, and `Transaction` — three aggregates — which forces an architectural decision.

---

## Impact on this project — .NET 9 / ASP.NET Core / Hexagonal

### Files to add or modify

| # | Layer | Path | Change |
|---|---|---|---|
| 1 | Domain model | `Domain/Model/Account.cs` (and the three sealed subtypes) | Add `Money? DailyWithdrawalLimit { get; protected set; }` on the abstract base + ctor parameter |
| 2 | Domain service | `Domain/Service/WithdrawalPolicyService.cs` *(new)* | Pure class with `RequireWithinDailyLimit(Account, Money withdrawnSoFar, Money requested)` — registered as `AddSingleton<>` in `Program.cs` |
| 3 | Domain port (out) | `Domain/Port/Out/IDailyWithdrawalQueryPort.cs` *(new)* | `Task<Money> SumWithdrawalsAsync(Guid accountId, DateOnly utcDay)` |
| 4 | Domain port (in) | `Domain/Port/In/IUseCases.cs` | Add `ISetAccountDailyLimitUseCase` interface + nested `Command` record |
| 5 | Application | `Application/Service/AccountApplicationService.cs` | Inject the new query port + `WithdrawalPolicyService` via constructor. In `WithdrawAsync` and `TransferAsync`, two new lines: query port → policy → wrap any thrown exception as `LimitExceededException` (already mapped to 422). Implement `ISetAccountDailyLimitUseCase`. Use-case interfaces unchanged. |
| 6 | Adapter (out) | `Adapter/Out/Persistence/Entity/JpaEntities.cs` | Add `DailyWithdrawalLimit` column on `AccountJpaEntity` + `BankDbContext.OnModelCreating` map (`HasColumnType("numeric(19,2)")`, nullable) |
| 7 | Adapter (out) | `Adapter/Out/Persistence/Adapter/DailyWithdrawalQueryAdapter.cs` *(new)* | Implements the port via LINQ: `await _db.Transactions.Where(t => t.AccountId == id && t.Type == "WITHDRAWAL" && t.Timestamp >= start && t.Timestamp < end).SumAsync(t => t.Amount)` |
| 8 | Adapter (out) | `Adapter/Out/Persistence/Mapper/Mappers.cs` | Copy the new field both ways in the `AccountType` switch |
| 9 | Adapter (in) | `Adapter/In/Web/AdminController.cs` | New endpoint + DTO record `SetDailyLimitRequest(decimal Amount, Currency Currency)` |
| 10 | Composition | `Program.cs` | Register `IDailyWithdrawalQueryPort → DailyWithdrawalQueryAdapter` (Scoped), `WithdrawalPolicyService` (Singleton), and the new `ISetAccountDailyLimitUseCase` mapping (`AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as ISetAccountDailyLimitUseCase`) |
| 11 | Tests | `AyvalikBankHA.Tests/WithdrawalPolicyServiceTests.cs` *(new)* | 4–5 pure xUnit tests using `FluentAssertions` — no `WebApplicationFactory`, no DB |
| 12 | Tests | Existing controller/service tests | Add tier+limit interaction cases |

### Tech-stack-specific notes (.NET)

- **`Money` value object** — already a `record struct`. Adding a nullable `Money?` field on the abstract `Account` requires updating each sealed subtype's constructor — the C# compiler will catch any miss.
- **EF Core column map** — `entity.Property(a => a.DailyWithdrawalLimit).HasColumnType("numeric(19,2)").IsRequired(false);` in `BankDbContext.OnModelCreating`. EF Core migrations/`Database.EnsureCreated` will add the nullable column.
- **DateOnly + LINQ over EF Core** — convert `DateOnly` → `DateTimeOffset` boundaries before the LINQ `Where` clause; EF Core 9 translates `>= start && < end` to a clean SQL predicate.
- **Async LINQ `SumAsync`** — returns `0` when no rows match; wrap in `Money(value, currency)` before returning from the adapter.
- **DI multi-registration pattern** — the project already uses `AddScoped(sp => sp.GetRequiredService<AccountApplicationService>() as IXxxUseCase)`. The new `ISetAccountDailyLimitUseCase` follows the same idiom — one extra line in `Program.cs`.
- **`IExceptionHandler`** already maps `LimitExceededException → 422`. If you want a more specific `DailyLimitExceededException`, derive from `LimitExceededException` so the existing handler still catches it.
- **Schema isolation** — `Domain/` stays free of `Microsoft.EntityFrameworkCore`; persistence layer adds the column on its own JpaEntity, and the mapper shuttles the value across.

### Test impact

- **WithdrawalPolicyServiceTests**: pure xUnit + FluentAssertions, no `WebApplicationFactory`, no EF Core — exercises the rule with hand-built `Money` values. Sub-second feedback.
- **`DailyWithdrawalQueryAdapter` integration test** (optional): EF Core InMemory provider verifies the LINQ query.
- **Controller tests** (when `WebApplicationFactory` is added later): the auth + DI plumbing already exists; new tests just exercise the new endpoint plus the limit-exceeded path.

---

## Lesson Plan (apply to all six projects)

1. **Show both diffs side by side.** Count files; count *lines where the actual rule lives*.
2. **Change the rule** — "reset at customer's local midnight, not UTC." In HA you change one method on `WithdrawalPolicyService` + one query in the adapter. In LA you edit a long `WithdrawAsync` method that's already doing five other things.
3. **Add a second consumer** — `GET /api/accounts/{id}/today-summary` showing withdrawn-so-far + remaining-limit. In HA: one controller method calling the existing port + policy. In LA: copy the LINQ `SumAsync` + comparison into a new service method.

The moral: **architecture is a bet about which kinds of change are likely.** HA bets on rules changing and being reused — it pays a structural tax up front. LA bets on rules being stable and local — it pays an entanglement tax later.
