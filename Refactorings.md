# Refactorings

Claude Opus 5 (1M context) — created 2026-08-08

A log of significant refactorings applied to Ayvalık Bank HA-NET. Each entry records what the code
looked like before, what it looks like after, and — most importantly — *why* the change was worth
making.

For further enquiry please contact Akin Kaldiroglu at akin@kaldiroglu.dev

**Relationship to the other implementations.** This repository is one of six: hexagonal and layered,
in Java, .NET and Python. The refactorings below were designed in `AyvalikBankHA-JAVA` and ported
here, and each entry cross-references the Java write-up. Where C# gave a *different* answer — and it
did, three times — the entry says so rather than pretending the ports were identical. All six are
held to one HTTP contract by `AyvalikBankContractTests`.

---

## Entry 1 — Ownership authorization: a rule that could not be said

**Baseline:** `595bf35` · **Commit:** `43dce0c`

### The symptom

Any authenticated customer could operate on any other customer's data:

- `CustomerController.ChangePassword(Guid id, ...)` took its target from the route, and
  `[Authorize(Roles = "CUSTOMER")]` was the only gate. **Any customer could set any other
  customer's password, then log in as them.**
- Given an account id, any customer could deposit to it, withdraw from it, transfer out of it, and
  read its balance and full transaction history.
- `CreateChecking([FromQuery] Guid ownerId, ...)` let a customer open accounts owned by anyone.

### The tell

`UnauthorizedAccessException` existed. `GlobalExceptionHandler` mapped it to 403. **No production
code threw it.**

> **An exception nothing throws is a rule nothing enforces.** Grepping for exception types that
> appear only in a handler and a test is a two-minute audit that finds real holes. It found three in
> this codebase across the six repositories.

### The root cause

Not one customer-facing `Command` carried the caller:

```csharp
record Command(Guid AccountId, Money Amount);
```

There was no caller to compare against, so "the caller must own this account" was not merely
unenforced — it was **inexpressible**.

### What made this port easy

`BasicAuthHandler` already put the customer id in `ClaimTypes.NameIdentifier` at authentication
time and then nobody read it. Unlike `AyvalikBankHA-JAVA`, which needed a new `BankUserPrincipal`
*and* a `@WithBankUser` test fixture, .NET needed **no new infrastructure** — the identity was
already in the request, unused.

### Three enforcement shapes

| Situation | Technique |
|---|---|
| The resource *is* the caller's | **Delete the parameter** — `?ownerId=` is gone; the caller is the owner |
| The route names a customer | **Require self** — the route id must equal the caller |
| The route names an account | **Require ownership** — load, compare `OwnerId` |

**Prefer deleting a parameter to validating it.** A validated parameter must be validated everywhere,
forever; a deleted one is gone.

### The transfer asymmetry

The caller must own the **source**. The target is deliberately unchecked — sending money to other
people is the entire product. `The_transfer_target_is_deliberately_not_ownership_checked` exists
solely to pin that: the obvious-looking hardening ("the caller must own both") reads as correct in
review and breaks transfers entirely.

### A C#-specific hazard

The project's own `UnauthorizedAccessException` collides with `System.UnauthorizedAccessException`,
so every throw site must be fully qualified. `AyvalikBankHA-JAVA` avoided this class of problem by
giving its domain and application exceptions deliberately different names.

---

## Entry 2 — Optimistic locking: a token that would have caught nothing

**Baseline:** `43dce0c` · **Commit:** `4181786`

### The symptom

| Step | Transaction A | Transaction B |
|---|---|---|
| 1 | read balance 100 | |
| 2 | | read balance 100 |
| 3 | withdraw 50 → 50 in memory | |
| 4 | | withdraw 50 → 50 in memory |
| 5 | save → row = 50 | |
| 6 | | save → row = 50 (overwrites) |

Balance ends at **50** where it should be **0**, and **both** `Transaction` rows are written. Money
is created from nothing and the ledger contradicts the account.

### Why `[ConcurrencyCheck]` alone would not have fixed it

This is the part worth the entry, and it is where .NET differed from every other implementation.

```csharp
public async Task<Account?> FindByIdAsync(Guid id)
{
    var jpa = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
```

`AsNoTracking()` meant the service's read never entered the change tracker. `SaveAsync` then issued
its **own** lookup, fetching the *current* row — and compared the version against itself. The token
would have incremented forever and never once detected the conflict it exists for.

> **An ORM can only protect a row you actually loaded.** The version is not a property of your
> in-memory object; it is a claim about *which revision you read*. Re-read the row and you throw
> that claim away.

The read path is now tracked, so the version checked at save is the one the business decision was
based on.

Compare: `AyvalikBankHA-JAVA` had to restructure its **write** path (it rebuilt a detached entity);
`AyvalikBankHA-Python` and all three layered repos needed neither change, because they already read
and wrote through the same tracked instance.

### The test needs no threads

Two `DbContext` instances committing in a fixed order reproduce the bug deterministically — no
sleeps, no races, no flakiness. **A lost update is a stale-read problem, not a timing problem.**
Anyone writing a `Thread.Sleep`-and-hope test for this has misdiagnosed it.

`DbUpdateConcurrencyException` maps to **409 Conflict**, with a fixed message rather than EF's, which
names the entity and key.

---

## Entry 3 — Two API defects the contract suite found

**Baseline:** `4181786` · **Commit:** `a820542`

`AyvalikBankContractTests` is a black-box HTTP suite run against all six implementations. Its first
run against this repository failed 20 of 29 cases. Neither defect was a flaw in the suite, and
neither could have been caught by any test in this repository — because **there are no controller
tests here**; the domain tests never construct an HTTP request.

### Money movement was broken on any comma-decimal locale

```csharp
[Range(typeof(decimal), "0.01", "999999999")]
```

`RangeAttribute` parses its string bounds using the **current culture**. On the author's machine
(`en_TR`, decimal separator `,`) `"0.01"` failed to parse, and **every deposit, withdrawal and
transfer returned 400** with `"0.01 is not a valid value for Decimal"`.

Not an edge case — the core operation of a bank did not work. It would have passed in most CI
(`en-US`) and failed for every user in Europe. Six attributes were affected;
`ParseLimitsInInvariantCulture = true` fixes them.

> **Bounds written in source should never be locale-sensitive.**

### Enums had to be sent as numbers

`System.Text.Json` deserializes enums numerically unless `JsonStringEnumConverter` is registered, so
this API wanted `{"currency": 0}` while Java, Python and the documented API use `{"currency": "USD"}`.
The *response* DTOs already emitted strings — the API was asymmetric with itself.

---

## Entry 4 — A refusal vocabulary: when the HTTP status depends on a message

**Baseline:** `dc12b29` · **Commit:** `2ebad68`

### The symptom

The domain threw raw `InvalidOperationException` from **23** places, all meaning different things,
and the application layer recovered the meaning by matching on the message:

```csharp
catch (InvalidOperationException e) when (e.Message.Contains("frozen")
    || e.Message.Contains("closed") || e.Message.Contains("matured"))
{ throw new AccountNotOperableException(e.Message); }
```

**Rewording a domain message silently changed the response status.** The strings live in the domain,
the filters live in the application layer, and nothing tied them together. Catching the BCL type also
converted genuine defects into 4xx business responses.

This is a worse variant than the Java original, which at least inferred meaning from *which call it
wrapped* — deterministic, if fragile. Here the rule was enforced by coincidence.

### The change

`AccountRuleViolation` with four subtypes — `AccountNotActiveException`,
`InsufficientBalanceException`, `OperationNotPermittedException`,
`TransactionLimitExceededException` — and one typed translation. All 23 sites retyped; **zero
message matching remains**.

It extends `InvalidOperationException` so the migration stayed incremental: `catch
(AccountRuleViolation)` does **not** catch a plain `InvalidOperationException`, so precision is
gained at the catch site without invalidating tests that assert on the base type.

> **When introducing a type hierarchy over an existing exception, inherit from the type callers
> already catch.** The new hierarchy becomes additive and the migration stops being a big bang.

### What C# cannot do

Java seals the hierarchy and the compiler *proves* the translation switch total — a fifth refusal
type breaks the build. C# has no `permits` clause, so the switch carries a discard arm that throws:
a missing case fails loudly at runtime rather than silently falling through. That is the strongest
guarantee this language offers, and it is weaker than Java's.

---

## Entry 5 — TransactionAmount, and the struct that would have defeated it

**Baseline:** `2ebad68` · **Commit:** `8949d37`

### The problem

`Money` deliberately allows negatives — a `CheckingAccount` balance goes negative under overdraft —
so it cannot enforce positivity. Every money-moving method re-asserted the rule by hand.

A single type serving both *balance* (signed position) and *amount* (unsigned magnitude) can enforce
**neither**. The duplication was the symptom; the conflated type was the disease.

### The change

`TransactionAmount` wraps `Money` and is strictly positive by construction. **Zero is rejected as
well as negative** — direction is carried by which operation was called, and a zero-value transfer
would write two ledger rows recording no movement of money. The compiler enumerated all 54 call
sites.

### The trap

The idiomatic C# choice is a `readonly record struct` — which is exactly what `Money` already is.
That would have been **silently broken**:

```csharp
default(TransactionAmount)   // legal, bypasses the constructor, yields amount 0
```

Every struct in C# has an implicit parameterless default no constructor can intercept. A value object
whose whole purpose is "cannot be constructed invalid" would have had a permanent back door — and
following the surrounding style would have led straight into it.

`TransactionAmount` is therefore a **class**, with a test asserting exactly that and explaining why.

> The same idea needs a different mechanism in each language, and the idiomatic-looking choice can
> quietly defeat it.

### Where the type stops

Balances, transfer fees and recorded transaction amounts keep using `Money`, because zero is legal
for all three — so the persistence layer was untouched.

---

## Entry 6 — Actor-shaped ports

**Baseline:** `8949d37` · **Commit:** `b7b8266`

### The symptom

`IUseCases.cs` held **20 single-method interfaces**. `AccountController` took nine constructor
parameters, `AdminController` ten, and `Program.cs` carried twenty DI registrations.

### "But isn't one interface per method better Interface Segregation?"

No, and this is the most common misreading of ISP. The principle says *clients should not be forced
to depend on methods they do not use*. `AccountController` uses **all nine** customer-facing methods
— it depends on nothing it does not call, so ISP was already satisfied. Splitting them bought no
segregation whatsoever.

Where ISP genuinely bites is the **actor boundary**: `AdminController` must not depend on `Deposit`
and `Withdraw`. That is the split the new ports make and the old ones blurred.

### The principle

Cockburn: **a port is one conversation with one kind of outside actor.** Two actors × three subjects
gives five ports, and the count falls out of the principle rather than being chosen.

| Port | Actor × subject | Methods |
|---|---|---|
| `ICustomerAccountPort` | customer × accounts | 9 |
| `IAccountAdministrationPort` | admin × accounts | 5 |
| `ICustomerAdministrationPort` | admin × customers | 4 |
| `ICustomerSelfServicePort` | customer × self | 1 |
| `IBankSettingsPort` | admin × bank config | 1 |

`AccountController` 9 → **1** parameter, `AdminController` 10 → **3**, DI registrations 20 → **5**.

### Verified by booting the app

DI rewiring is exactly what unit tests cannot check — the 103 tests passed throughout. The change was
confirmed by starting the application and running the shared contract suite against it.

---

## Deliberate non-goals

- **`CustomerJpaEntity` has the same lost-update exposure** as accounts. The same fix applies; left
  out to keep entry 2 reviewable.
- **No retry-on-conflict.** A 409 tells the client to retry; automatic server-side retry is a
  separate design with its own idempotency questions.
- **`ChangePassword` does not verify the current password.** Defensible under HTTP Basic, where the
  caller has already proven it on the same request; not defensible once sessions arrive.
- **No controller tests.** The web layer is covered by `AyvalikBankContractTests` instead. That suite
  is what found entry 3, and it needs a running instance — so nothing here catches a wiring defect
  offline.

## Discussion questions

1. Entry 3's culture bug passed every test in this repository. What class of defect can *only* be
   found by a test that speaks HTTP?
2. Entry 4 says C# cannot prove the translation exhaustive. Design a way to get closer, and say what
   it costs.
3. Entry 5 rejected `readonly record struct`. `Money` still is one. Is that inconsistent, or correct?
