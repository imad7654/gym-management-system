# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A gym management system for **The Fit Bear Gym** in Lebanon, built to actually run the
business rather than to demo. .NET 8 Web API (Clean Architecture) + React 18/TypeScript/MUI
+ MySQL 8.

`README.md` is accurate and current; prefer it for setup detail. The live build plan — what
is done, what is next, and why — is the "Fit Bear Build Plan" artifact:
https://claude.ai/code/artifact/1eec4cc9-7cd0-4090-bfeb-9e31130bac64

## Commands

All backend commands run from `backend/GymManagement`.

```bash
dotnet build
dotnet test                                              # ~130 tests
dotnet test --filter "FullyQualifiedName~DailyTakings"   # one class
dotnet test --filter "FullyQualifiedName~PhoneNumberKeyTests.Normalize_WhenThereIsNoUsableNumber_ReturnsNull"
```

Migrations run from `src/GymManagement.Api`:

```bash
dotnet ef migrations add <Name> --project ../GymManagement.Infrastructure
dotnet ef database update --project ../GymManagement.Infrastructure
```

Running the stack — start these in order, since each depends on the one before:

```bash
cd docker && docker compose up -d
cd backend/GymManagement && dotnet run --project src/GymManagement.Api/GymManagement.Api.csproj
cd frontend/gym-management-ui && npm run dev     # also: npm run build, npm run lint
```

Frontend on 5173, API on 5001, Swagger at `/swagger`. CORS is bound to 5173 only, so use
that dev server rather than starting another on a different port.

**MySQL is published on 3307, not 3306**, via the gitignored
`docker/docker-compose.override.yml` — a `MySQL80` Windows service already owns 3306 on this
machine. The override uses `ports: !override` because Compose otherwise appends to the base
port list and still tries to bind 3306.

**The API will not start without user-secrets.** `Jwt:SecretKey` and
`ConnectionStrings:DefaultConnection` are never in a tracked file; `SecurityStartupChecks`
refuses to boot if they are missing, short, or set to a previously committed value. There
are no default credentials — the seeded admin's password is printed to the console once on
first run.

**Debugging tip that has paid off repeatedly:** endpoints that respond correctly to `curl`
can still fail with real data. Check the API console for `ERR` lines after exercising a
page. Browser console plus API log together beat either alone.

## The money model

This is the part that needs reading several files to understand, and the part where a
plausible-looking change does real damage.

**Payment rows are append-only.** A mistake is corrected by adding a row, never by editing
or deleting one. If a payment can be quietly changed afterwards, the owner cannot use this
system to check the till against the drawer — which is most of why they wanted it.

**A reversal is a second row** with a negative `Amount`, `ReversesPaymentId` pointing at the
original, and `Status = Completed`. It stays Completed deliberately: every revenue query
filters on Completed, so marking reversals otherwise would drop them from the sums and a
reversed payment would still read as income. A reversal is recognised by
`ReversesPaymentId`, never by its status. The original row is left exactly as it was.

**Partial payments.** A payment below the package price is recorded but does not extend the
membership. A later payment is credited against what the member already put down, and when
the total reaches the price the membership extends. Two markers make this work:

- `PeriodStartDate`/`PeriodEndDate` are stamped **only on the payment that completed the
  purchase**, because that is what a reversal reads to decide how many days to take back.
  Stamping several rows would wind the membership back several times.
- `SettledByPaymentId` marks earlier part payments as spent, so the member's next payment is
  not discounted by money already used.

`PaymentQueries.OutstandingCredit()` is the single definition of "money put down that has
not bought anything yet", shared by the payment desk and the who-owes-money report. Keep it
that way: if the two ever disagreed, the report would chase members for money the desk had
already taken. Its subtlest clause excludes a reversal whose original bought a period — the
days were already taken back by winding the membership down, so counting it again would
subtract the same money twice.

**Money is always summed in USD** (`Payment.Amount`), whatever was handed over.
`AmountReceived` + `Currency` + `ExchangeRate` record the actual transaction and are never
recalculated — a payment taken at last month's rate must still read the same next year.

## Dates and the clock

`IMembershipClock` separates two kinds of time, and mixing them is the classic bug here.

- **Instants** (when a payment happened) are UTC.
- **Membership dates** are calendar dates on the gym's own wall, in `Asia/Beirut`. Beirut
  runs ahead of UTC, so for part of every day `DateTime.UtcNow.Date` is still yesterday as
  far as the gym is concerned. Comparing an end date against it expires memberships a day
  early.

Use `_clock.Today` for every membership comparison and `DayBoundsUtc(date)` for day-scoped
reports. `DayBoundsUtc` also handles Lebanon moving its clocks *at midnight*, which makes
local midnight a time that does not exist twice a year.

**Membership end dates are inclusive**: `end = start + DurationDays - 1`, so a 30-day
package gives exactly 30 days and reversing a payment is the exact inverse of taking one.
This deviates from the blueprint's 6.5 pseudocode, deliberately.

**`MembershipStatus` is not stored.** It is computed from the end date every time it is
asked for — by `Client.StatusFrom` in memory and by `ClientQueries` in SQL. The only stored
piece is `Client.IsSuspended`, the freeze a person sets by hand, which wins over the dates.

It used to be a column refreshed by a nightly job. The job was never written, so expired
members read `Active` forever. Do not reintroduce a stored copy: if you need the status in a
query use `ClientQueries.AllowedIn/ExpiringWithin/WithStatus/StatusRank`, and if you need it
in memory use `client.MembershipStatusOn(today)`.

The rule therefore exists twice, in C# and in SQL. `ClientQueriesTests` drives every case
through both and fails if they disagree; `ClientQueriesTranslateToSqlTests` proves the query
side still translates to MySQL, which the in-memory test provider cannot catch. Change one
side, change the other, and both suites must stay green.

Compare against `MembershipStatuses.AllowedIn`, not `Active` alone: a member in their last
week is `Expiring` and still entitled to train.

## Layering and conventions

Dependency flow is API → Infrastructure → Application → Domain.

Keep third-party format/IO concerns out of Application. The member import shows the pattern:
`IMemberImportFileReader` is declared in Application, and the ClosedXML implementation lives
in Infrastructure.

- **Soft delete everywhere.** Entities implement `ISoftDeletable`; global query filters hide
  inactive `User`, `Client`, `Package`. Watch the consequence: `Include`ing a soft-deleted
  principal yields `null`, so navigation properties need null checks in reports.
- **Audit entries are queued onto the caller's `UnitOfWork`** and committed by the same
  `SaveChangesAsync` as the change they describe. A trail with holes is worse than none — it
  looks complete.
- **One `SaveChangesAsync` is one transaction.** Prefer it to explicit
  `BeginTransactionAsync`, which the configured `MySqlRetryingExecutionStrategy` refuses.
- `PhoneNumberKey.Normalize` is the rule for "are these the same person" — it strips
  formatting and the `961` country code. Used by the import's duplicate check, and intended
  for Phase 3's sign-up-by-phone match. Never compare raw phone text.
- Endpoints are `/api/v1/...`, `[Authorize(Policy = "AdminOnly")]`, and return
  `ApiResponse<T>`. Exceptions (`NotFoundException`, `BusinessException`,
  `ValidationException`) are translated by `GlobalExceptionMiddleware` — throw rather than
  hand-rolling error responses.

## Frontend

- **The wire format is enum *names*, not numbers.** `PaymentMethodString` and friends in
  `types/index.ts` are what actually travel; the numeric enums are legacy helpers. Keep the
  string unions identical to `GymManagement.Domain/Enums`.
- TanStack Query for server state, Zustand (`authStore`) for auth, persisted to
  localStorage. Axios interceptors refresh tokens on 401 automatically.
- Path aliases in `vite.config.ts`: `@components`, `@services`, `@pages`, `@lib`, `@store`,
  and `@app-types` (named to avoid colliding with `@types/*`).
- The server owns pricing and membership periods. Forms send only what reception can
  observe — who, what package, how they paid, how much changed hands. Figures shown before
  submitting are a preview of the server's calculation, never the source of it.

## Domain decisions worth knowing

These are settled, and reversing them by accident is easy.

- **The gym takes cash and Whish Money only.** `PaymentMethod` is `Cash, Whish, Other`. Card
  and bank transfer were removed: the gym is not a registered business and has no merchant
  account, so they were options reception could pick but the gym could not honour.
- **Whish money is never in the drawer.** The daily takings report keeps it separate from
  cash, or the owner's count stops reconciling and the report stops being trusted.
- **LBP is supported but the gym trades in USD.** The currency toggle, per-payment rate, and
  the owner's daily rate in Settings all stay. Do not strip them.
- **Imported members get no payment history.** They arrive with the end date they already
  had and no `Payment` rows — inventing the money that must have produced those dates would
  corrupt every revenue report from day one.
- **Members will sign up by matching an existing record by phone** (Phase 3), never by free
  self-signup, which would put strangers in the member list.
- The build order is an 8-phase plan; Phase 1 is complete. The authoritative spec is the
  "Gym System Blueprint" artifact, not the markdown in this repo.
