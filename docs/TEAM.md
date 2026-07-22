# Phân công & quy trình làm việc — SAT

## 1. Vai trò trong quy trình

Đây là vai trò về **cách làm việc**, độc lập với module code mà mỗi người viết.

| Người | Vai trò | Trách nhiệm |
|---|---|---|
| **@Nlonggg** | Git lead | Merge các nhánh vào `master`, xử lý conflict, bảo đảm đủ tính năng trước khi merge |
| **@Ngọc Ánh** | QA | Kiểm tra luồng chạy có đúng thiết kế không; nghiệm thu UI |
| **@truonghieu11233** | Tester + Data | Lấy dữ liệu thật từ FLM (môn học, cột điểm, tài liệu); viết unit test & integration test |
| **@Anhoa123** | Tester + Data | Như trên; kiểm tra lại các hàm đã hoạt động đúng chưa |
| **@LucFr1746** | Code review | Review sạch code, bảo đảm mọi thành viên hiểu module mình viết |

> `.github/CODEOWNERS` đã cấu hình để **@LucFr1746** tự động được gán review mọi PR,
> và **@Nlonggg** được gán thêm ở các file dùng chung (`db/`, `Directory.Packages.props`,
> `Abstractions/`, `SatDbContext.cs`) — nơi một thay đổi sai làm hỏng build của cả 5 người.

---

## 2. Phân công module & nhánh Git

Mỗi người **5 chức năng**, làm trên **nhánh riêng**, không ai commit thẳng vào `master`.

### Member 1 — Authentication & User
**Nhánh:** `feature/m1-auth-user`

| # | Chức năng | Service | Trạng thái |
|---|---|---|---|
| 1 | Login | `IAuthService.LoginAsync` | ✅ đã cài đặt |
| 2 | Logout | `ICurrentUserContext.Clear` | ✅ đã cài đặt |
| 3 | Profile | `IUserService.GetProfileAsync` / `UpdateProfileAsync` | 🔲 interface sẵn sàng |
| 4 | Change Password | `IAuthService.ChangePasswordAsync` | ✅ đã cài đặt |
| 5 | User Management | `IUserService` (list, tạo, khóa, reset mật khẩu) | 🔲 interface sẵn sàng |

**View cần viết:** `LoginView`, `ProfileView`, `ChangePasswordView`, `UserManagementView`

---

### Member 2 — Catalog Admin (phần GHI)
**Nhánh:** `feature/m2-catalog-admin`

| # | Chức năng | Service |
|---|---|---|
| 1 | Manage Major | `ICatalogAdminService.CreateMajorAsync` / `UpdateMajorAsync` / `DeactivateMajorAsync` |
| 2 | Manage Semester | `CreateSemesterAsync` / `UpdateSemesterAsync` / `SetCurrentSemesterAsync` |
| 3 | Manage Subject | `CreateCourseAsync` / `UpdateCourseAsync` / `DeactivateCourseAsync` |
| 4 | Assign Subject to Major | `AssignCourseToMajorAsync` / `RemoveCourseFromMajorAsync` |
| 5 | Curriculum Management | `UpdateCurriculumItemAsync` + `SyncMajorRequiredCreditsAsync` |

**View cần viết:** `MajorAdminView`, `SemesterAdminView`, `SubjectAdminView`, `CurriculumAdminView`

> ⚠️ Sau mỗi lần thêm/bớt môn khỏi khung chương trình **phải gọi `SyncMajorRequiredCreditsAsync`**.
> `Major.RequiredCredits` lệch với tổng tín chỉ khung sẽ làm sai % tiến độ tốt nghiệp của
> **mọi** sinh viên ngành đó.

---

### Member 3 — Catalog & Progress (phần ĐỌC)
**Nhánh:** `feature/m3-catalog-progress`

| # | Chức năng | Service |
|---|---|---|
| 1 | Select Major | `ICourseService.GetMajorsAsync` |
| 2 | View Subjects | `ICourseService.SearchAsync` |
| 3 | View Semester | `ICourseService.GetSemestersAsync` |
| 4 | Subject Detail | `ICourseService.GetByIdAsync` + `IPrerequisiteService.GetPrerequisiteTreeAsync` |
| 5 | Curriculum Progress | `IGraduationService.GetProgressAsync` |

**View cần viết:** `MajorSelectView`, `SubjectListView`, `SemesterListView`, `SubjectDetailView`, `CurriculumProgressView`

> Member 2 và Member 3 làm trên **cùng vùng dữ liệu nhưng khác file**: M2 giữ phần ghi
> (`ICatalogAdminService`), M3 giữ phần đọc (`ICourseService`). Tách vậy để hai người
> gần như không đụng nhau khi merge.

---

### Member 4 — Grade & GPA
**Nhánh:** `feature/m4-grade-gpa`

| # | Chức năng | Service |
|---|---|---|
| 1 | View Grades | `IGradeService.GetGradesAsync` |
| 2 | Manage Grades | `IGradeService.UpsertGradeAsync` |
| 3 | GPA Calculator | `IGpaService.GetCumulativeGpaAsync` / `GetGpaSummaryAsync` |
| 4 | Transcript | `IGradeService.GetTranscriptAsync` |
| 5 | Statistics | `IAnalyticsService` (biểu đồ LiveCharts2) |

**View cần viết:** `GradeListView`, `GradeEntryView`, `GpaCalculatorView`, `TranscriptView`, `StatisticsView`

> ⚠️ **`IGpaService` là service nhiều người phụ thuộc nhất** — Member 3 cần nó cho
> Curriculum Progress. Hãy giao bản chạy được **sớm nhất trong nhóm**.
>
> Trong lúc chờ, Member 3 cứ code trên interface + một class `FakeGpaService` trả số cứng.

---

### Member 5 — Materials
**Nhánh:** `feature/m5-materials`

| # | Chức năng | Service |
|---|---|---|
| 1 | View Materials | `IMaterialService.SearchAsync` / `GetByCourseAsync` |
| 2 | Search Materials | `IMaterialService.SearchAsync` (lọc theo từ khóa, môn, nhóm) |
| 3 | Upload | `IMaterialService.UploadAsync` |
| 4 | Download | `IMaterialService.DownloadAsync` |
| 5 | Manage Materials | `IMaterialService.UpdateAsync` / `DeactivateAsync` |

**View cần viết:** `MaterialListView`, `MaterialUploadView`, `MaterialDetailView`

> ⚠️ Bảng tách làm đôi: `Material` (mô tả) và `MaterialFile` (nội dung nhị phân).
> **Tuyệt đối không `Include(m => m.File)` trong truy vấn danh sách** — làm vậy là kéo
> toàn bộ byte của mọi file về máy chỉ để hiển thị cái tên. Chỉ `DownloadAsync` mới
> được đụng tới `MaterialFile`. Đã có test bảo vệ điều này.

---

## 3. Quy trình Git

```
master                    ← chỉ @Nlonggg merge vào
  ├── feature/m1-auth-user
  ├── feature/m2-catalog-admin
  ├── feature/m3-catalog-progress
  ├── feature/m4-grade-gpa
  └── feature/m5-materials
```

### Hằng ngày

```bash
git checkout feature/m4-grade-gpa
git pull --rebase origin master     # BẮT BUỘC mỗi sáng và trước mỗi PR
# ... code ...
dotnet format FAT.sln               # nếu không, CI sẽ chặn PR
git add -A
git commit -m "feat: them man hinh nhap diem"
git push
```

### Commit message

```
<type>: <mô tả ngắn>
```
`feat` · `fix` · `refactor` · `docs` · `test` · `chore`

### Khi xong một nhóm chức năng

1. `git pull --rebase origin master` và xử lý hết conflict **trên nhánh của mình**
2. Mở PR vào `master`, điền đầy đủ template
3. Chờ CI xanh (3 job: Build & Test, Code Format, Database Scripts)
4. @LucFr1746 review
5. @Nlonggg merge bằng **Squash and merge**

> Xử lý conflict trên nhánh của mình trước khi mở PR, chứ đừng đẩy conflict sang
> cho người merge. Người merge không biết ý đồ code của bạn nên rất dễ chọn nhầm.

---

## 4. Quy tắc chống conflict

Ba vùng dưới đây là nơi 5 người dễ đụng nhau nhất. Kiến trúc đã được thiết kế để tránh:

| Vùng | Cách tránh |
|---|---|
| Đăng ký DI | Mỗi module có file riêng `SAT.App/Startup/<Module>Registration.cs`. **Không sửa `App.xaml.cs`.** |
| Menu sidebar | Sinh từ `NavigationItem` do mỗi module tự đăng ký. **Không sửa `MainWindow.xaml`.** |
| View / ViewModel | Mỗi người một thư mục riêng `Views/<Module>/`, `ViewModels/<Module>/` |

### Vùng ĐÓNG BĂNG — muốn sửa phải báo cả nhóm

- `db/*.sql` — sửa xong phải báo mọi người chạy lại `.\db\setup-db.ps1`
- `src/SAT.Domain/Entities/` — entity phải luôn khớp với schema SQL
- `src/SAT.Services/Abstractions/` — đổi interface là gãy code người khác
- `src/SAT.Data/SatDbContext.cs`
- `Directory.Packages.props` — thêm package thì thêm version ở đây, **không** ghi version trong `.csproj`

---

## 5. CI/CD

Không có deploy. Chỉ kiểm tra tự động và đóng gói.

| Workflow | Khi nào chạy | Làm gì |
|---|---|---|
| **CI** | Mọi push, mọi PR | Build + test (Windows), kiểm tra `dotnet format`, dựng lại DB trên SQL Server thật rồi chạy 2 lần để kiểm tra idempotent |
| **Package** | Chạy tay, hoặc đẩy tag `v*` | Đóng gói app thành 1 file `.zip` chạy được, kèm script DB và README |

Lấy gói: tab **Actions** → **Package** → **Run workflow** → tải ở mục **Artifacts**.

> Test tích hợp cần SQL Server nên **tự bỏ qua** khi chạy trên CI Windows
> (`SkippableFact`). Đổi lại, job **Database Scripts** dựng SQL Server thật trên
> Linux để kiểm tra script — vì dự án không dùng EF Migrations, đây là lưới an
> toàn duy nhất cho thay đổi schema.
