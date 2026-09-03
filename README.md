# 🐻 The Fit Bear Gym — Management System

A gym management system built to actually run a small gym: members, packages, money taken
at the desk, and the reports an owner checks the business against. .NET 8 Web API with a
React 18 front end, over MySQL.

![License](https://img.shields.io/badge/license-MIT-green.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![React](https://img.shields.io/badge/React-18-61DAFB)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6)

## 📋 Contents

- [What it does](#-what-it-does)
- [Tech stack](#-tech-stack)
- [Getting it running](#-getting-it-running)
- [Signing in](#-signing-in)
- [Configuration](#-configuration)
- [Cloning it for another gym](#-cloning-it-for-another-gym)
- [Project structure](#-project-structure)
- [API](#-api)
- [Architecture](#-architecture)
- [Testing](#-testing)
- [Building for production](#-building-for-production)
- [Troubleshooting](#-troubleshooting)
- [License](#-license)

## ✨ What it does

### Who signs in

Three kinds of account, and the split between them is the point rather than a formality.

| | Can | Cannot |
|---|---|---|
| **Owner** (Admin) | Everything | — |
| **Reception** (Staff) | Find a member, take a payment, renew, add a member, freeze and unfreeze, work the call sheet, see today's takings and who owes | Reverse a payment, see revenue history, delete a member, read the audit trail, change prices, manage accounts, set the exchange rate, import members |
| **Member** (Client) | Their own membership, days remaining, package and payment history | Anything about anybody else |

Reception adds money and never removes it. Payments are append-only, so a desk that cannot
reverse structurally cannot make money disappear from the till — which is most of the
reason the owner can check this system against the drawer at all.

### The desk

- Members with search that matches a phone number however it is written
- One page per member: status, days remaining, tap-to-call and WhatsApp, renew, freeze
- Payments in USD or LBP, cash or Whish Money, with the day's rate
- **Part payments** — money short of the package price is recorded, credited to the member,
  and extends the membership only once the total reaches the price
- **Refunds are corrections, not deletions** — a reversal is a second row pointing at the
  original, so the history of what happened is never rewritten
- Import an existing member list from a spreadsheet

### The owner

- **Today** — what should be in the drawer, who to ring, who owes money
- **Daily takings** — split into cash and Whish, so the drawer can be counted against it
- **Revenue** — month by month, with member count alongside, and any month opened up
- **Who owes money** — part-paid members, longest outstanding first
- **History** — an audit trail of who did what
- Members, packages, exchange rate, and who can sign in

### Members

Members claim an account by matching the phone number and surname the gym already has —
never free sign-up, so a stranger cannot appear in the member list. A lapsed member can
still sign in, and is told how long ago their membership ended, because they are exactly
the person the gym wants back.

## 🛠️ Tech stack

**Backend** — ASP.NET Core 8.0 Web API, Clean Architecture (Domain / Application /
Infrastructure / API), Entity Framework Core 8, MySQL 8, JWT with refresh tokens, BCrypt,
FluentValidation, Serilog, MailKit, Swagger.

**Frontend** — React 18, TypeScript, Vite 5, Material-UI v5, TanStack Query for server
state, Zustand for auth, React Router v6, Axios, Recharts.

## 🚀 Getting it running

### Prerequisites

| | |
|---|---|
| **.NET SDK 8.0 or later** | [Download](https://dotnet.microsoft.com/download) — the projects target `net8.0`; a newer SDK builds them fine |
| **Node.js 18+** | [Download](https://nodejs.org/) |
| **Docker Desktop** | [Download](https://www.docker.com/products/docker-desktop/) — runs MySQL, so you do not have to install it |
| **Git** | [Download](https://git-scm.com/downloads) |

> **Windows: clone into a short path.** Some files in this repository have paths around 125
> characters. Cloning into a deeply nested folder fails with `Filename too long` unless long
> paths are enabled:
>
> ```bash
> git config --global core.longpaths true
> ```

You do not need MySQL installed locally — step 1 runs it in Docker. If you would rather use
an existing MySQL server, skip step 1 and point the connection string at it instead.

### 1. Clone and start the database

```bash
git clone https://github.com/imad7654/gym-management-system.git
cd gym-management-system
```

Then, **from the repository root**:

```bash
cd docker && docker compose up -d
```

That starts MySQL 8 on **port 3306** with a database called `gymdb` and a user `gymuser`.

> **If port 3306 is already in use** — because you have MySQL installed as a service —
> create `docker/docker-compose.override.yml` (it is gitignored, so it stays yours):
>
> ```yaml
> services:
>   mysql:
>     ports: !override
>       - "3307:3306"
> ```
>
> The `!override` tag matters: without it Compose *appends* to the port list and still tries
> to bind 3306. Then use `Port=3307` in the connection string below.

### 2. Set the secrets

**Do this before anything else touches the database.** There are no secrets in this
repository and no default credentials. The API refuses to start without these, and tells
you exactly what is missing if you forget.

From the repository root, move into the API project — the three `user-secrets` commands
after it run from there:

```bash
cd backend/GymManagement/src/GymManagement.Api
```

```bash
dotnet user-secrets init
```

```bash
dotnet user-secrets set "Jwt:SecretKey" "$(openssl rand -base64 48)"
```

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=gymdb;User=gymuser;Password=gympass123;"
```

> On Windows without `openssl`, any 32+ character random string works. The password above is
> the one in the tracked `docker-compose.yml`, so it is fine for a local container and must
> never be used for a real server — see [Configuration](#-configuration).

### 3. Start the API

From the repository root:

```bash
cd backend/GymManagement && dotnet run --project src/GymManagement.Api/GymManagement.Api.csproj
```

The schema is migrated on startup — no separate migration step, and no need to run
`dotnet ef database update` by hand. In development it is also seeded with a demo gym of 30
members and four months of payments, so every screen has something to show.

Wait for `Now listening on: http://localhost:5001`, and **copy the admin password it prints**
— see [Signing in](#-signing-in).

### 4. Start the front end

From the repository root, in a second terminal:

```bash
cd frontend/gym-management-ui && npm install && npm run dev
```

| | |
|---|---|
| App | http://localhost:5173 |
| API | http://localhost:5001 |
| Swagger | http://localhost:5001/swagger |

> CORS allows `http://localhost:5173` only, so use that port rather than starting the dev
> server somewhere else.

## 🔐 Signing in

**There are no default credentials.** On first run against an empty database an
administrator is created and its randomly generated password is **printed to the API console
once**:

```
=====================================================================
 ADMIN ACCOUNT CREATED - this password is shown once and not saved
   email:    admin@gym.local
   password: ...
=====================================================================
```

Copy it then — it is hashed, and nothing can read it back. If you miss it, either use the
**Forgot your password?** link (needs email configured), or delete the user row and restart
to get a fresh one.

To choose the credentials yourself instead, set them before the first run:

```bash
dotnet user-secrets set "Seed:AdminEmail" "you@example.com"
dotnet user-secrets set "Seed:AdminPassword" "<a long password>"
```

## ⚙️ Configuration

### Secrets

Secrets never live in a tracked file. Locally they go in `dotnet user-secrets`; in
production, environment variables with `__` for the nesting.

| Setting | Purpose | Required |
|---|---|---|
| `Jwt:SecretKey` | Signs login tokens. At least 32 bytes of random data. | Always |
| `ConnectionStrings:DefaultConnection` | Database. Use an account that owns only `gymdb`, never `root`. | Always |
| `Seed:AdminEmail` | First administrator | Outside development |
| `Seed:AdminPassword` | First administrator's password | Outside development |
| `Email:Host` / `Username` / `Password` / `FromAddress` | Sends password reset emails | Outside development |

The API validates these at startup and **refuses to boot** if one is missing, too short, or
set to a value that was once committed to this repository. In development it warns instead
of refusing, so a throwaway container is not a blocker.

> ⚠️ Editing a secret that was already committed does not remove it — the old value stays in
> git history. Rotate to a genuinely new value rather than correcting the file.

### Email

Password reset emails need an SMTP account. For Gmail that means turning on 2-Step
Verification and generating a **16-character App Password** — Gmail refuses an account's
ordinary password over SMTP.

```bash
dotnet user-secrets set "Email:Username" "thegym@gmail.com"
dotnet user-secrets set "Email:Password" "<16-char app password>"
dotnet user-secrets set "Email:FromAddress" "thegym@gmail.com"
```

Without it, **in development** reset links are written to the API console so the flow can
still be used. **Outside development the API will not start**, deliberately: the page says
"a link is on its way" whether or not the address exists, so a silently unsent email would
only be discovered when somebody was already locked out.

### Other settings

| Setting | Default | Purpose |
|---|---|---|
| `Gym:TimeZone` | `Asia/Beirut` | Membership dates are calendar dates on the gym's own wall, not UTC |
| `Seed:DemoData` | `true` in development | The 30-member demo gym. Off everywhere else, and it refuses to run against a database that already has members |
| `Validation:MinPasswordLength` | 12 | Applies to every way a password is set |
| `Cors:AllowedOrigins` | `localhost:5173` | Where the front end is served from |

## 🎨 Cloning it for another gym

Resale is by cloning this repository per gym and editing it by hand — deliberately, rather
than building a theming engine, which under roughly five customers never pays for itself.

**Two files carry the gym's identity.** Change these and the whole system follows:

| File | Holds |
|---|---|
| `frontend/gym-management-ui/src/config/gym.ts` | Name, short name, emoji mark, tagline, every brand colour, and whether to draw the mascot |
| `backend/.../GymManagement.Api/appsettings.json` → `Gym:Name` | The name used in the emails the system sends, and the gym's initial name in Settings |

Two rather than one because a TypeScript constant cannot be read from C#.

Everything else the gym shows — address, phone, opening hours, the homepage copy, social
links — is **content, not identity**, and the owner edits it under **Settings** without a
developer. Where a saved value exists it always beats the config; the config is what the app
shows before the first request returns, and on a fresh install whose owner has not opened
Settings yet.

The mascot is a bear drawn in code. Set `showMascot: false` for a gym that is not this one,
rather than shipping somebody else's animal.

> Colours belong in that config file and nowhere else. If you find yourself typing a hex
> value into a component, it will survive the next rebrand and nothing will find it — that
> is exactly how this app's hero banner stayed green through a rebrand until somebody
> looked at it.

## 📁 Project structure

```
gym-management-system/
├── backend/GymManagement/
│   ├── src/
│   │   ├── GymManagement.Domain/          Entities, enums, domain rules
│   │   ├── GymManagement.Application/     DTOs, services, validators
│   │   ├── GymManagement.Infrastructure/  EF Core, repositories, migrations, SMTP
│   │   └── GymManagement.Api/             Controllers, middleware, startup checks
│   └── tests/GymManagement.UnitTests/
│
├── frontend/gym-management-ui/src/
│   ├── pages/
│   │   ├── home/  login/  register/  password/   Public
│   │   ├── member/                               A member's own area
│   │   ├── dashboard/                            Today
│   │   ├── clients/  import/                     Members
│   │   ├── payments/  packages/  reports/        Money
│   │   └── users/  settings/  account/           Setup
│   ├── components/     Feature components + shared layout
│   ├── services/       One module per API area
│   ├── store/          Zustand auth store
│   ├── routes/         Route guards
│   ├── lib/            Axios, theme, helpers
│   └── types/          Shared TypeScript types
│
└── docker/docker-compose.yml                     MySQL 8
```

## 📚 API

Everything is under `/api/v1`. **Admin** is the owner, **Staff** is reception, **Client** is
a member. Full request and response detail is in Swagger at `/swagger`.

### Auth

| Method | Endpoint | Access | |
|---|---|---|---|
| POST | `/auth/login` | Anyone | Returns access + refresh tokens |
| POST | `/auth/register` | Anyone | Member sign-up, matched to an existing member by phone and surname |
| POST | `/auth/forgot-password` | Anyone | Emails a reset link. Answers identically whether or not the address exists |
| POST | `/auth/reset-password` | Anyone | Spends the emailed token. Single use, one hour |
| POST | `/auth/refresh-token` | Anyone | Rotates the refresh token |
| POST | `/auth/logout` | Signed in | Revokes the refresh token |
| GET | `/auth/me` | Signed in | The signed-in account |
| PUT | `/auth/change-password` | Signed in | Ends every session, this one included |

### A member's own area

| Method | Endpoint | Access | |
|---|---|---|---|
| GET | `/me/membership` | Client | Status, days left, package |
| GET | `/me/payments` | Client | Their own payments |

Resolved from the signed-in user; there is deliberately no id in the URL.

### Members

| Method | Endpoint | Access | |
|---|---|---|---|
| GET | `/clients` | Admin, Staff | Paginated, searchable, filterable |
| GET | `/clients/{id}` · `/summary` · `/payments` · `/outstanding` | Admin, Staff | One member |
| POST | `/clients` | Admin, Staff | Add |
| PUT | `/clients/{id}` | Admin, Staff | Edit |
| POST | `/clients/{id}/suspend` · `/resume` | Admin, Staff | Freeze and unfreeze |
| GET | `/clients/expiring` | Admin, Staff | Running out soon |
| GET | `/clients/{id}/account` | Admin, Staff | Whether they have a login |
| POST | `/clients/{id}/account/reset-password` | Admin, Staff | Set a member's password |
| DELETE | `/clients/{id}` · POST `/{id}/restore` | **Admin** | Soft delete and undelete |
| POST | `/clients/import/preview` · `/commit` | **Admin** | Import from a spreadsheet |

### Money

| Method | Endpoint | Access | |
|---|---|---|---|
| GET | `/payments` · `/payments/{id}` | Admin, Staff | |
| POST | `/payments` | Admin, Staff | Take a payment |
| POST | `/payments/{id}/reverse` | **Admin** | Refund, as a correcting row |
| GET | `/packages` · `/packages/{id}` | Admin, Staff | |
| GET | `/packages/active` | Anyone | For the public homepage |
| POST/PUT/DELETE | `/packages...` | **Admin** | Prices are the owner's |
| GET | `/exchange-rates/current` | Admin, Staff | Today's LBP rate |
| PUT | `/exchange-rates/today` | **Admin** | Set it |

### Reports and dashboard

| Method | Endpoint | Access | |
|---|---|---|---|
| GET | `/dashboard/today` | Admin, Staff | Drawer, call sheet, who owes |
| POST | `/dashboard/chased/{clientId}` | Admin, Staff | Mark a member as called |
| GET | `/dashboard/expiring-memberships` | Admin, Staff | |
| GET | `/dashboard/stats` · `/this-month` | **Admin** | Revenue figures |
| GET | `/reports/who-owes` | Admin, Staff | Part-paid members |
| GET | `/reports/daily-takings` | Admin, Staff | Reception is limited to today |
| GET | `/reports/revenue` · `/revenue/{year}/{month}` | **Admin** | Month by month |
| GET | `/reports/audit` | **Admin** | Who did what |

### Accounts and gym details

| Method | Endpoint | Access | |
|---|---|---|---|
| GET/POST/PUT/DELETE | `/users...` | **Admin** | Who can sign in |
| POST | `/users/{id}/reset-password` · `/restore` | **Admin** | |
| GET | `/gym-info` | Anyone | Name, hours, contact for the homepage |
| PUT | `/gym-info` | **Admin** | |

## 🏗️ Architecture

Dependencies point inward: **API → Infrastructure → Application → Domain**. The Domain layer
depends on nothing.

Third-party format and IO concerns stay out of Application. The member import shows the
pattern — `IMemberImportFileReader` is declared in Application and implemented with ClosedXML
in Infrastructure; `IEmailSender` and MailKit work the same way.

Four decisions worth knowing before changing anything:

- **Payment rows are append-only.** A mistake is corrected by adding a row, never by editing
  or deleting one. If a payment could be quietly changed afterwards, the owner could not use
  this system to check the till against the drawer.
- **Membership status is never stored.** It is computed from the end date every time it is
  asked for, in C# and in SQL. It used to be a column refreshed by a nightly job that was
  never written, so expired members read `Active` forever.
- **Membership dates are calendar dates on the gym's wall**, in `Gym:TimeZone`; payment
  timestamps are UTC instants. Mixing the two expires memberships a day early.
- **Money is always summed in USD.** What was physically handed over, its currency and the
  rate on the day are recorded separately and never recalculated.

## 🧪 Testing

```bash
cd backend/GymManagement && dotnet test
```

236 tests, written from the failure each one prevents rather than from the method it calls.

The front end has no test runner yet; it is checked with the type checker and linter:

```bash
cd frontend/gym-management-ui && npm run build && npm run lint
```

## 🚢 Building for production

```bash
cd backend/GymManagement/src/GymManagement.Api && dotnet publish -c Release -o ./publish
```

```bash
cd frontend/gym-management-ui && npm run build
```

Before deploying, set every value marked *Outside development* in
[Configuration](#-configuration). The API will refuse to start without them, which is
deliberate — it is better than discovering a missing signing key in production.

## 🐛 Troubleshooting

**The API exits immediately with "required secrets are missing or unsafe"**
Working as intended — it lists exactly what to set. See [step 2](#2-set-the-secrets).

**`Filename too long` when cloning**
Windows path limit. `git config --global core.longpaths true`, or clone somewhere shallower.

**Port 3306 already allocated**
You have MySQL running already. Use the override file in
[step 1](#1-clone-and-start-the-database), or point the connection string at your existing
server.

**"Unable to connect to any of the specified MySQL hosts"**
The container is not up yet. `docker compose ps` from `docker/`, and check the port in the
connection string matches the one Compose published.

**A page loads but its data does not**
Check the API console for `ERR` lines after using the page. Endpoints that answer `curl`
correctly can still fail on real data; the browser console and the API log together beat
either alone.

**CORS errors**
Use `http://localhost:5173`. Only that origin is allowed, so a dev server started on another
port will be refused.

**Starting over with a clean database**

```bash
docker exec gym-mysql mysql -uroot -proot123 -e "DROP DATABASE gymdb; CREATE DATABASE gymdb;"
```

Restart the API — it migrates and reseeds, and prints a new admin password.

## 📝 License

MIT — see [LICENSE](LICENSE).

## 👤 Author

_Add your name, GitHub and contact here._

---

Made with 💪 and 🐻 by The Fit Bear Gym Team
