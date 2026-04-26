# Tests — Ayvalık Bank HA-NET

**Stack:** xUnit · FluentAssertions · NSubstitute
**Total:** 66 tests · 100% passing
**Run:** `dotnet test`

All tests are unit tests on the domain and the domain service layer. Application and adapter layers are exercised through the domain. (Integration tests with `WebApplicationFactory` + EF Core InMemory are a planned extension.)

---

## Summary by Test Class

| Test class | Tests | Focus |
|---|---:|---|
| `MoneyTests` | 4 | Value object: zero, add, currency-mismatch guard, comparison |
| `PasswordValidationServiceTests` | 5 | Length, digit, uppercase, special-char rules |
| `AccountTests` | 8 | Cross-cutting on the abstract `Account` API (via `CheckingAccount.Open`) |
| `CheckingAccountTests` | 7 | Overdraft happy path + cap rejection + currency / negative-limit guards |
| `SavingsAccountTests` | 7 | `AccrueInterest` math, double-accrual rejection, frozen vs. closed behavior |
| `TimeDepositAccountTests` | 9 | Lock invariants, `Mature(today)` credit, double-mature rejection, invalid construction |
| `AccountStateTests` | 11 | All `AccountState` transitions and operability checks |
| `CustomerTierTests` | 5 | `CustomerTier` policy data + `Customer.ChangeTier` |
| `TransferDomainServiceTests` | 10 | Tier-aware `CalculateFee`; per-transaction `RequireTransferWithinLimit` / `RequireWithdrawalWithinLimit` |

---

## Coverage by Domain Concern

### `Money` (value object)
- `ZeroIsZero` — `Money.Zero(Currency)` returns `0` of the right currency
- `AddsSameCurrency` — addition produces sum within the same currency
- `RejectsAddDifferentCurrency` — adding mismatched currencies throws `ArgumentException`
- `GteWorksWithinSameCurrency` — `IsGreaterThanOrEqualTo` comparison

### `PasswordValidationService`
- `Accepts_valid` — happy-path
- `Rejects_short`, `Rejects_no_digit`, `Rejects_no_upper`, `Rejects_no_special` — failures

### `Account` (abstract base, via `CheckingAccount.Open`)
- `OpensAtZeroAndActive` — new account starts `0` balance, `ACTIVE` status
- `DepositRaisesBalanceAndReturnsTransaction`, `WithdrawDecreasesBalance`
- `WithdrawingMoreThanBalanceThrows`, `DepositInWrongCurrencyThrows`
- `FreezeBlocksDeposit`, `CloseIsTerminal`
- `TransferOutWithFeeDeductsTotal`

### `CheckingAccount` (overdraft semantics)
- `OpensWithoutOverdraftByDefault`, `OpensWithOverdraftLimit`
- `WithdrawWithoutOverdraftRejectsOverdraw` (matches `*Insufficient*`)
- `WithdrawWithinOverdraftAllowsNegativeBalance`
- `WithdrawBeyondOverdraftThrows` (matches `*overdraft*`)
- `OverdraftCurrencyMustMatch`, `NegativeOverdraftRejected`

### `SavingsAccount` (monthly interest accrual)
- `OpensWithGivenInterestRate` — initial state
- `NegativeInterestRateRejected`
- `WithdrawCannotGoNegative` — savings has no overdraft
- `AccrueInterestAddsMonthlyInterest` — 12% annual / 12 = 1% monthly → `1000 → 1010`
- `AccrueInterestForSameMonthRejected` — idempotency check
- `AccrueInterestOnClosedRejected` — closed account
- `AccrueOnFrozenStillWorks` — frozen accounts can still accrue (system action)

### `TimeDepositAccount` (locked principal, maturation)
- `OpensWithPrincipalAsBalance` — initial state, `Matured = false`
- `DepositRejected` — principal locked
- `TransferOutRejected` — transfers not allowed
- `WithdrawBeforeMaturityRejected`
- `MatureBeforeMaturityDateRejected`
- `MatureCreditsInterestAndAllowsWithdraw` — `10000 × 5% × 1y = 500` credit, then withdraw works
- `MatureTwiceRejected`
- `NonPositivePrincipalRejected`, `MaturityDateBeforeOpenedOnRejected`

### `AccountState` (State pattern)
- `NewAccountIsActive`, `FreezeMovesToFrozen`, `UnfreezeMovesFrozenToActive`
- `FreezingFrozenThrows`, `UnfreezingActiveThrows`
- `CloseFromActiveIsTerminal`, `CloseFromFrozenIsTerminal`
- `ClosedRejectsAllTransitions` (Freeze, Unfreeze, Close all throw)
- `FrozenBlocksDeposit`, `FrozenBlocksWithdraw`, `ClosedBlocksDeposit`

### `CustomerTier` (policy + mutation)
- `StandardTierHasFullFeeAndFiveThousandCaps`
- `PremiumTierHasHalfFeeAndHigherCaps` (50k transfer / 25k withdrawal)
- `PrivateTierHasNoFeeAndNoCaps` (transfer + withdrawal both `null`)
- `NewCustomerDefaultsToStandard`, `ChangeTierUpdatesCustomer`

### `TransferDomainService`
- `SameCustomerIsFree` — returns `0` regardless of percent
- `StandardTierAppliesFullPercent` (1.0×), `PremiumTierAppliesHalfPercent` (0.5×), `PrivateTierIsFree` (0.0×)
- `StandardTransferOverCapThrows`, `StandardTransferAtCapPasses`
- `PremiumTransferOverCapThrows`, `PrivateTransferHasNoCap`
- `StandardWithdrawalOverCapThrows`, `PrivateWithdrawalHasNoCap`

---

## Known Gaps

- **No application service tests.** `CustomerApplicationService` / `AccountApplicationService` are exercised indirectly via the domain. Direct tests with mocked ports (NSubstitute) would shore up the orchestration layer.
- **No web/persistence-adapter tests.** Mappers (Domain ⇄ JpaEntity) and controllers are not tested directly. `WebApplicationFactory` + EF Core InMemory is a planned add.
- **No coverage tooling.** No code coverage report is produced. Adding `dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator` would mirror the Java sibling's JaCoCo report.

---

## How to Run

```bash
dotnet test                                                     # all tests
dotnet test --filter "FullyQualifiedName~CheckingAccountTests"  # single class
dotnet test --filter "FullyQualifiedName~AccountStateTests.FrozenBlocksDeposit"  # single test
dotnet test --logger "console;verbosity=normal"                 # show each test
```
