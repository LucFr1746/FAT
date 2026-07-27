# Team assignments & workflow — Academic Tracker

## 1. Process roles

These are roles about *how we work*, independent of which module each person writes.

| Person | Role | Responsibility |
|---|---|---|
| **@Nlonggg** | Git lead | Merges branches into `master`, resolves conflicts, confirms features are complete before merging |
| **@Ngoc Anh** | QA | Checks that flows match the agreed design; signs off on the UI |
| **@truonghieu11233** | Tester + Data | Collects real data from FLM (courses, grade components, materials); writes unit and integration tests |
| **@Anhoa123** | Tester + Data | As above; verifies that implemented functions behave correctly |
| **@LucFr1746** | Code review | Reviews for clean code; makes sure every member understands the module they wrote |

> `.github/CODEOWNERS` assigns **@LucFr1746** to every PR automatically, and adds
> **@Nlonggg** on shared files (`db/`, `Directory.Packages.props`, `Services/Abstractions/`,
> `FAT_DBContext.cs`) — the places where one bad change breaks the build for all five people.

---

## 2. Module assignments & branches

Five features each, on a dedicated branch. Nobody commits directly to `master`.

### Member 1 — Authentication & User
**Branch:** `feature/m1-auth-user`

| # | Feature | Service | Status |
|---|---|---|---|
| 1 | Login | `IAuthService.LoginAsync` | Implemented |
| 2 | Logout | `ICurrentUserContext.Clear` | Implemented |
| 3 | Profile | `IUserService.GetProfileAsync` / `UpdateProfileAsync` | Interface ready |
| 4 | Change Password | `IAuthService.ChangePasswordAsync` | Implemented |
| 5 | User Management | `IUserService` (list, create, lock, reset password) | Interface ready |

**Views to build:** `LoginView`, `ProfileView`, `ChangePasswordView`, `UserManagementView`

---

### Member 2 — Catalog Admin (the WRITE side)
**Branch:** `feature/m2-catalog-admin`

| # | Feature | Service |
|---|---|---|
| 1 | Manage Major | `ICatalogAdminService.CreateMajorAsync` / `UpdateMajorAsync` / `DeactivateMajorAsync` |
| 2 | Manage Semester | `CreateSemesterAsync` / `UpdateSemesterAsync` / `SetCurrentSemesterAsync` |
| 3 | Manage Subject | `CreateCourseAsync` / `UpdateCourseAsync` / `DeactivateCourseAsync` |
| 4 | Assign Subject to Major | `AssignCourseToMajorAsync` / `RemoveCourseFromMajorAsync` |
| 5 | Curriculum Management | `UpdateCurriculumItemAsync` + `SyncMajorRequiredCreditsAsync` |

**Views to build:** `MajorAdminView`, `SemesterAdminView`, `SubjectAdminView`, `CurriculumAdminView`

> **Call `SyncMajorRequiredCreditsAsync` after every add or remove from a curriculum.**
> If `Major.RequiredCredits` drifts away from the curriculum total, the graduation
> percentage is wrong for **every** student in that major.

---

### Member 3 — Catalog & Progress (the READ side)
**Branch:** `feature/m3-catalog-progress`

| # | Feature | Service |
|---|---|---|
| 1 | Select Major | `ICourseService.GetMajorsAsync` |
| 2 | View Subjects | `ICourseService.SearchAsync` |
| 3 | View Semester | `ICourseService.GetSemestersAsync` |
| 4 | Subject Detail | `ICourseService.GetByIdAsync` + `IPrerequisiteService.GetPrerequisiteTreeAsync` |
| 5 | Curriculum Progress | `IGraduationService.GetProgressAsync` |

**Views to build:** `MajorSelectView`, `SubjectListView`, `SemesterListView`, `SubjectDetailView`, `CurriculumProgressView`

> Members 2 and 3 work on the **same data but different files**: M2 owns the write side
> (`ICatalogAdminService`), M3 owns the read side (`ICourseService`). That split is what
> keeps them out of each other's merges.

---

### Member 4 — Grade & GPA
**Branch:** `feature/m4-grade-gpa`

| # | Feature | Service |
|---|---|---|
| 1 | View Grades | `IGradeService.GetGradesAsync` |
| 2 | Manage Grades | `IGradeService.UpsertGradeAsync` |
| 3 | GPA Calculator | `IGpaService.GetCumulativeGpaAsync` / `GetGpaSummaryAsync` |
| 4 | Transcript | `IGradeService.GetTranscriptAsync` |
| 5 | Statistics | `IAnalyticsService` (LiveCharts2 charts) |

**Views to build:** `GradeListView`, `GradeEntryView`, `GpaCalculatorView`, `TranscriptView`, `StatisticsView`

> **`IGpaService` is the most depended-on service in the project** — Member 3 needs it for
> Curriculum Progress. Ship a working version **first, before anything else**.
>
> While waiting, Member 3 should code against the interface with a `FakeGpaService`
> returning fixed numbers.

---

### Member 5 — Materials
**Branch:** `feature/m5-materials`

| # | Feature | Service |
|---|---|---|
| 1 | View Materials | `IMaterialService.SearchAsync` / `GetByCourseAsync` |
| 2 | Search Materials | `IMaterialService.SearchAsync` (keyword, course, category) |
| 3 | Upload | `IMaterialService.UploadAsync` |
| 4 | Download | `IMaterialService.DownloadAsync` |
| 5 | Manage Materials | `IMaterialService.UpdateAsync` / `DeactivateAsync` |

**Views to build:** `MaterialListView`, `MaterialUploadView`, `MaterialDetailView`

> The data is split across two tables: `Material` (metadata) and `MaterialFile` (bytes).
> **Never `Include(m => m.File)` in a list query** — that drags the full contents of every
> file across the wire just to render their names. Only `DownloadAsync` may touch
> `MaterialFile`. There is a test enforcing this.

---

## 3. Git workflow

```
master                    <- only @Nlonggg merges into this
  |-- feature/m1-auth-user
  |-- feature/m2-catalog-admin
  |-- feature/m3-catalog-progress
  |-- feature/m4-grade-gpa
  |-- feature/m5-materials
```

### Day to day

```bash
git checkout feature/m4-grade-gpa
git pull --rebase origin master     # REQUIRED every morning and before every PR
# ... write code ...
dotnet format Project.sln           # otherwise CI blocks the PR
git add -A
git commit -m "feat: add grade entry screen"
git push
```

### Commit messages

```
<type>: <short description>
```
`feat` · `fix` · `refactor` · `docs` · `test` · `chore`

### When a group of features is done

1. `git pull --rebase origin master` and resolve every conflict **on your own branch**
2. Open a PR into `master` and fill in the template
3. Wait for CI to go green (3 jobs: Build & Test, Code Format, Database Scripts)
4. @LucFr1746 reviews
5. @Nlonggg merges with **Squash and merge**

> Resolve conflicts on your own branch before opening the PR rather than handing them to
> whoever merges. The person merging does not know what your code was meant to do, so it
> is very easy for them to pick the wrong side.

---

## 4. Conflict-avoidance rules

These three areas are where five people collide most often. The architecture is designed
to keep them apart:

| Area | How it is avoided |
|---|---|
| DI registration | Each module gets its own `App/Startup/<Module>Registration.cs`. **Do not edit `App.xaml.cs`.** |
| Sidebar menu | Built from the `NavigationItem` entries each module registers. **Do not edit `MainWindow.xaml`.** |
| Views / ViewModels | Each person owns their own `Views/<Module>/` and `ViewModels/<Module>/` folder |

### FROZEN areas — tell the team before changing these

- `db/*.sql` — after changing, everyone must re-run `.\db\setup-db.ps1`
- `src/Domain/Entities/` — entities must always match the SQL schema
- `src/Services/Abstractions/` — changing an interface breaks other people's code
- `src/Data/FAT_DBContext.cs`
- `Directory.Packages.props` — add package versions here, **never** in a `.csproj`

---

## 5. CI/CD

There is no deployment. Only automated checks and packaging.

| Workflow | Trigger | What it does |
|---|---|---|
| **CI** | Every push, every PR | Build + test (Windows), verify `dotnet format`, rebuild the database on a real SQL Server and run the scripts twice to prove idempotency |
| **Package** | Manual, or a `v*` tag | Produces a runnable `.zip` with the database scripts and a README |

To get a package: **Actions** tab → **Package** → **Run workflow** → download from **Artifacts**.

> Integration tests need SQL Server, so they **skip themselves** on the Windows CI runner
> (`SkippableFact`). To compensate, the **Database Scripts** job stands up a real SQL Server
> on Linux and exercises the scripts — since the project does not use EF Migrations, that
> is the only safety net for schema changes.
