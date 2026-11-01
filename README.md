# PHCIS Clinic Booking

Blazor WASM + ASP.NET Core API for the PHCIS senior practical assessment.
Same layered shape as OZ.TradingServiceTyrone: Domain has no EF, no HTTP, no UI.

Written tasks (stakeholders, requirements, ERD, sequences): **[SOLUTION.md](SOLUTION.md)**.

## Open in Visual Studio

Open `PHCIS.ClinicBookingTyrone.slnx`.

Set **ClinicBooking.Api** as the startup project. F5 uses LocalDB (`(localdb)\MSSQLLocalDB`) and runs EF migrate + seed.

## Solution

| Project | Role |
|---|---|
| `ClinicBooking.Domain` | Entities, enums, role names |
| `ClinicBooking.Application` | MediatR CQRS, FluentValidation, AutoMapper |
| `ClinicBooking.Infrastructure` | EF Core SQL Server + Identity (`AppDbContext`) |
| `ClinicBooking.Api` | HTTP, JWT, Serilog, Swagger, CORS for Blazor |
| `ClinicBooking.Blazor` | WASM + Fluent UI. HTTP client only — no EF |
| `ClinicBooking.Tests` | xUnit, Moq, WebApplicationFactory |

## Run (Visual Studio)

API: `https://localhost:7088/swagger`  
Blazor: `https://localhost:7276` (run **Api** and **Blazor** together)

In Visual Studio: right-click the solution → **Configure Startup Projects** → **Multiple startup projects** → start **ClinicBooking.Api** and **ClinicBooking.Blazor**.

Need LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
```

### Seed logins (password `Password123!`)

| Email | Role |
|---|---|
| `patient@phcis.local` | Patient (can book) |
| `staff@phcis.local` | ClinicStaff |
| `admin@phcis.local` | Admin |

### Blazor booking (guest → sign-in)

Slots are **one hour** (08:00–16:00 SAST, weekdays). The week grid is public. Booked/blocked cells show as unavailable — no patient names.
Select a free cell, then **Book now**. If you are not signed in, the chosen clinic/date/slot is stored in `sessionStorage` and you are sent to sign in. After JWT login the app posts the booking, clears storage, and opens the confirmation page. If someone else took the slot during sign-in, the API returns **409** and the grid asks you to pick another time.

Staff (`staff@phcis.local`) open **Staff diary** to see who booked each hour and to block/unblock free cells. Patient names never appear on the public board.

Open the same clinic schedule in two browsers: book in one, the other cell turns Booked without refresh (SignalR `/hubs/slots`).

### Swagger demo

1. `GET /api/clinics`
2. `GET /api/clinics/{id}/slots?from=yyyy-MM-dd&to=yyyy-MM-dd` (weekdays) — Available and Booked, no PII
3. `POST /api/auth/login` with the patient email
4. Authorize (lock icon) — paste the token
5. `POST /api/appointments` `{ "timeSlotId": "..." }` — confirmation code `PHC-......` (JWT required)
6. Same slot again → **409** (handler re-checks availability; unique index is the race backup)
7. `POST /api/appointments/{id}/cancel` frees the slot. `POST .../reschedule` `{ "newTimeSlotId": "..." }` keeps the old row as Rescheduled and books a new one.

```powershell
dotnet test
```

## Docker

Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows, Linux containers). From the repo root:

```powershell
docker compose up --build
```

The first run downloads SQL Server (~1.5 GB) and waits until it is healthy. Then:

| What | URL |
|---|---|
| Blazor | http://localhost:8081 |
| API Swagger | http://localhost:8080/swagger |

Three containers: `sql` (SQL Server 2022), `api` (this API, migrate + seed on start), `web` (nginx serving published WASM). The browser still talks to `http://localhost:8080` — WASM cannot see the Docker network name `api`.

Stop with `Ctrl+C`, then `docker compose down`. Add `-v` only if you want to wipe the database volume.

LocalDB is a Windows user instance and **cannot** run inside Linux containers. Visual Studio keeps using LocalDB; Compose is the “runs on any machine” story.

Optional password override: copy `.env.example` to `.env`.

## GitHub

After Git for Windows is installed (this machine did not have `git` on PATH):

```powershell
cd C:\Dev\repo\PHCIS.ClinicBookingTyrone
git init
git add .
git commit -m "Clinic booking assessment: API, Blazor, Docker, SOLUTION.md"
gh repo create PHCIS.ClinicBookingTyrone --private --source=. --remote=origin --push
```

Pushing to `main` runs `.github/workflows/ci.yml` (restore, build, test on windows-latest + LocalDB).
