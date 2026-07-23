# FAT — FPT Academic Tracker

A WPF desktop application for tracking academic progress: grades, GPA, credits,
prerequisites, curriculum progress and learning materials.

Built with **.NET 8**, **WPF**, **MVVM**, **EF Core 8** and **SQL Server**.

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (Express is enough) with `sqlcmd` on PATH
- Windows 10 version 2004 or later

### Setup

```bash
git clone https://github.com/LucFr1746/FAT.git
cd FAT
```

Build the database — this **drops and recreates** the `FAT` database:

```bash
.\db\setup-db.ps1
```

If your SQL Server is not the default instance on `localhost`:

```bash
.\db\setup-db.ps1 -Server ".\SQLEXPRESS"
```

…then copy `src/FAT.App/appsettings.Local.json.example` to
`appsettings.Local.json` and adjust the connection string. That file is
git-ignored, so everyone keeps their own settings.

Run it:

```bash
dotnet run --project src/FAT.App
```

### Demo accounts

| Username | Password | Notes |
|---|---|---|
| `admin` | `Admin@123` | Administrator |
| `student01` | `Student@123` | Final year, ~78% progress, includes a retaken course |
| `student02` | `Student@123` | Second year, ~53% progress |
| `student03` | `Student@123` | First year, ~14% progress |

---

## Project layout

```
FAT.sln
├── db/                      SQL scripts - THE source of truth for the schema
├── docs/TEAM.md             Team assignments, branches, Git workflow
└── src/
    ├── FAT.Domain/          Entities, enums, academic rules (no dependencies)
    ├── FAT.Data/            EF Core: FatDbContext, repositories
    ├── FAT.Services/        Business logic (no WPF reference, so it is testable)
    ├── FAT.App/             WPF presentation layer
    └── FAT.Tests/           xUnit tests
```

## Common commands

```bash
dotnet build FAT.sln          # build
dotnet test FAT.sln           # run tests
dotnet format FAT.sln         # apply formatting (CI blocks PRs that skip this)
.\db\setup-db.ps1             # rebuild the database from scratch
```

---

## Notes for contributors

**This project does not use EF Core Migrations.** The scripts under `db/` are the
source of truth for the schema, and the entities in `FAT.Domain/Entities` are written
to match them. With five people working in parallel, generated migrations break the
snapshot chain and cost more time than they save. After changing `db/01_schema.sql`,
update the matching entity and tell the team to re-run `setup-db.ps1`.

See [docs/TEAM.md](docs/TEAM.md) for module ownership, branch names and the
conflict-avoidance rules.
