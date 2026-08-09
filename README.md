# Ayvalık Bank HA-NET

A banking application built as a learning project to demonstrate **Hexagonal Architecture (Ports & Adapters)** in **.NET 10 / ASP.NET Core**. .NET counterpart to `AyvalikBankHA-JAVA` (Java/Spring Boot).

For further enquiry please contact Akin Kaldiroglu at akin@kaldiroglu.dev

## Tech Stack

| Concern | Technology |
|---------|-----------|
| Runtime | .NET 10 |
| Framework | ASP.NET Core 10 (Web API) |
| Persistence | EF Core 9 + Npgsql (PostgreSQL) |
| Security | Custom Basic Auth handler |
| Validation | DataAnnotations |
| Testing | xUnit · FluentAssertions · NSubstitute |
| Password hashing | BCrypt.Net-Next |
| Infrastructure | Docker Compose (PostgreSQL on port 5434) |

## Quick Start

```bash
docker compose up -d
/Users/akin/.dotnet/dotnet run --project AyvalikBankHA.Api --urls http://localhost:5080
```

Then open **http://localhost:5080/swagger**.

Default admin: `admin@ayvalikbank.dev` / `Admin@123!` (seeded on first startup)

See [Run it with](#run-it-with) at the end of this file for the full story — why the
`dotnet` path is spelled out, why `--urls` is required, and how to reach every endpoint.

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
| POST | `/api/accounts/checking` | CUSTOMER |
| POST | `/api/accounts/savings` | CUSTOMER |
| POST | `/api/accounts/time-deposit` | CUSTOMER |
| GET | `/api/customers/{id}/accounts` | CUSTOMER |
| GET | `/api/accounts/{id}/balance` | CUSTOMER |
| POST | `/api/accounts/{id}/deposit` | CUSTOMER |
| POST | `/api/accounts/{id}/withdraw` | CUSTOMER |
| POST | `/api/accounts/{id}/transfer` | CUSTOMER |
| GET | `/api/accounts/{id}/transactions` | CUSTOMER |

## Test coverage

103 unit tests (xUnit + FluentAssertions), covering:
- Money arithmetic and currency guards
- Password validation
- Account state transitions (State pattern)
- Per-subtype invariants: checking overdraft, savings monthly accrual, time-deposit lock + maturation
- TransferDomainService tier-aware fees and per-transaction limits
- Customer tier mutation

## Run it with

### Prerequisites

**Use the full path to the .NET 10 SDK.** This project targets `net10.0`. On this machine
`dotnet` on the `PATH` resolves to `/usr/local/share/dotnet/dotnet`, which carries SDKs 6.0
through 9.0 and no 10.0 — building with it fails:

```
error NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.
```

The .NET 10 SDK (10.0.102) lives in a separate per-user install at `/Users/akin/.dotnet`.
The two installs are independent: each `dotnet` binary finds SDKs in its own `sdk/` folder
and runtimes in its own `shared/` folder, and never sees the other's. Putting `~/.dotnet`
first on the `PATH` is *not* a safe fix — it only carries the 9.0 and 10.0 runtimes, so
projects targeting `net6.0`–`net8.0` would then fail to start.

Docker is also needed for PostgreSQL. If `docker compose` reports a socket error, Docker
Desktop has stopped — `open -a Docker`, wait for it, and retry.

### Run the tests

```bash
/Users/akin/.dotnet/dotnet test
```

103 tests, no database required — the service tests use the EF Core InMemory provider.

### Run the application

```bash
cd AyvalikBankHA-NET
docker compose up -d                                    # PostgreSQL, host port 5434
/Users/akin/.dotnet/dotnet run --project AyvalikBankHA.Api --urls http://localhost:5080
```

Wait for `Now listening on: http://localhost:5080` in the output.

**`--urls` is required.** This project has no `Properties/launchSettings.json`, so nothing
declares a port; without the flag Kestrel falls back to its own default of
`http://localhost:5000`. Port 5080 is simply the convention used here — any free port works,
as long as you use the same one in the URLs below. The missing `launchSettings.json` also
means `ASPNETCORE_ENVIRONMENT` is unset, so the app starts in **Production**, not
Development. Swagger still works because `Program.cs` calls `UseSwagger()` unconditionally
rather than behind an `IsDevelopment()` guard.

### Reach it

| What | Where |
|---|---|
| Swagger UI | http://localhost:5080/swagger |
| OpenAPI document | http://localhost:5080/swagger/v1/swagger.json |
| API root | http://localhost:5080/api/... |
| PostgreSQL | `localhost:5434`, database `ayvalikbank_ha_net`, user `bankuser` / `bankpass` |

There is **no route at `/`** — browsing to http://localhost:5080 returns 404. That is not a
failure; go to `/swagger`.

Every endpoint requires HTTP Basic authentication. The seeded admin is the **full email
address**, not a username:

```
admin@ayvalikbank.dev / Admin@123!
```

In Swagger UI, click **Authorize**, enter those two values, then use *Try it out* on any
endpoint. From the shell:

```bash
# Should return the customer list
curl -u 'admin@ayvalikbank.dev:Admin@123!' http://localhost:5080/api/admin/customers

# Should return 401 — proves authentication is actually enforced
curl -i http://localhost:5080/api/admin/customers
```

### Run the shared contract suite against it

The same black-box HTTP suite runs against all six Ayvalık Bank repos. From the
`AyvalikBankContractTests` checkout, with this app already running:

```bash
BANK_BASE_URL=http://localhost:5080 pytest tests/
```

### Stop it, and start from a clean database

`Ctrl+C` stops the application. To stop PostgreSQL but keep the data:

```bash
docker compose down
```

The schema is created by `EnsureCreatedAsync()` in `AdminSeeder`; there are no EF Core
migrations. `EnsureCreated` is create-or-nothing — it builds the schema when the database is
empty and never alters an existing one. So after changing an entity, or when accumulated
test data gets in the way, drop the volume and let it rebuild:

```bash
docker compose down -v && docker compose up -d
```

The admin is re-seeded automatically on the next startup.

## Ports across the six repos

The six Ayvalık Bank implementations are meant to be compared side by side, so every one
takes its own application port and its own PostgreSQL port. All six can run at once.

| Repo | App | PostgreSQL | Database |
|---|---|---|---|
| `AyvalikBankHA-JAVA` | **8080** | **5437** | `ayvalikbank_ha_java` |
| `AyvalikBankLA-JAVA` | **8081** | **5438** | `ayvalikbank_la_java` |
| `AyvalikBankHA-NET` | **5080** | **5434** | `ayvalikbank_ha_net` |
| `AyvalikBankLA-NET` | **5050** | **5433** | `ayvalikbank_la_net` |
| `AyvalikBankHA-Python` | **8000** | **5436** | `ayvalikbank` |
| `AyvalikBankLA-Python` | **8001** | **5435** | `ayvalikbank` |

**5432 is deliberately left free** for a native PostgreSQL install (Postgres.app, Homebrew).
A container bound to it collides, and — worse — an application pointed at it connects to the
native server instead of its own container without any error to say so.

Each stack pins its port differently, because each offers a different mechanism:

| Repo | Where its port comes from |
|---|---|
| `AyvalikBankHA-JAVA` | Spring Boot's default 8080 — nothing to configure |
| `AyvalikBankLA-JAVA` | `server.port=8081` in `application.properties` |
| `AyvalikBankHA-NET` | no `launchSettings.json`, so `--urls http://localhost:5080` is **required** — without it Kestrel binds 5000 |
| `AyvalikBankLA-NET` | `AyvalikBankLA.Api/Properties/launchSettings.json` |
| `AyvalikBankHA-Python` | `--port 8000` on the uvicorn command line |
| `AyvalikBankLA-Python` | `--port 8001` on the uvicorn command line |

The two Python repos are the fragile pair: uvicorn takes its port as a launch argument and
has no configuration file to default it in, so **omitting `--port` gives both 8000** and the
second one to start fails to bind. The documented commands always pass it explicitly.
