# Description

<!-- What this changes, and WHY. The reviewer cannot read your mind. -->

## Module

<!-- Tick your own box -->

- [ ] Member 1 — Authentication & User (Login, Logout, Profile, Change Password, User Management)
- [ ] Member 2 — Catalog Admin (Manage Major/Semester/Subject, Assign Subject to Major, Curriculum Management)
- [ ] Member 3 — Catalog & Progress (Select Major, View Subjects, View Semester, Subject Detail, Curriculum Progress)
- [ ] Member 4 — Grade & GPA (View/Manage Grades, GPA Calculator, Transcript, Statistics)
- [ ] Member 5 — Materials (Manage, Upload, Download, View, Search Materials)
- [ ] Shared / infrastructure (also needs @Nlonggg's approval)

## Features included in this PR

<!-- List each completed feature so it can be checked against the assignment table -->

-
-

## How to test

<!-- What does the reviewer have to click to see this working? -->

1.
2.

---

## Checklist before opening the PR

- [ ] `dotnet build Project.sln` succeeds with no errors
- [ ] `dotnet test Project.sln` is green
- [ ] `dotnet format Project.sln` has been run (CI blocks the PR otherwise)
- [ ] `git pull --rebase origin master` done and all conflicts resolved
- [ ] No `appsettings.Local.json`, `bin/`, `obj/` or `publish/` committed
- [ ] No passwords or personal connection strings in the code

## If this PR touches shared areas

<!-- Skip this section if you only changed files inside your own module -->

- [ ] Changed `db/*.sql` → the team has been told to re-run `.\db\setup-db.ps1`
- [ ] Changed an interface in `Services/Abstractions/` → whoever depends on it has been told
- [ ] Added a package → the version is in `Directory.Packages.props`, not in the `.csproj`

## Screenshots

<!-- Required for any UI change - @Ngoc Anh signs off on the UI from these -->
