# Mô tả

<!-- Làm gì, và VÌ SAO. Người review không đọc được suy nghĩ trong đầu bạn. -->

## Module

<!-- Đánh dấu x vào ô của mình -->

- [ ] Member 1 — Authentication & User (Login, Logout, Profile, Change Password, User Management)
- [ ] Member 2 — Catalog Admin (Manage Major/Semester/Subject, Assign Subject to Major, Curriculum Management)
- [ ] Member 3 — Catalog & Progress (Select Major, View Subjects, View Semester, Subject Detail, Curriculum Progress)
- [ ] Member 4 — Grade & GPA (View/Manage Grades, GPA Calculator, Transcript, Statistics)
- [ ] Member 5 — Materials (Manage, Upload, Download, View, Search Materials)
- [ ] Dùng chung / hạ tầng (cần thêm duyệt của @Nlonggg)

## Chức năng trong PR này

<!-- Liệt kê từng chức năng đã hoàn thành, để dễ đối chiếu với bảng phân công -->

-
-

## Cách kiểm thử

<!-- Người review phải bấm những gì để thấy nó chạy? -->

1.
2.

---

## Checklist trước khi mở PR

- [ ] `dotnet build FAT.sln` chạy sạch, không lỗi
- [ ] `dotnet test FAT.sln` xanh
- [ ] `dotnet format FAT.sln` đã chạy (CI sẽ chặn nếu sai định dạng)
- [ ] Đã `git pull --rebase origin master` và xử lý hết conflict
- [ ] KHÔNG commit `appsettings.Local.json`, `bin/`, `obj/`, `publish/`
- [ ] KHÔNG có mật khẩu / chuỗi kết nối cá nhân trong code

## Nếu PR có đụng vùng dùng chung

<!-- Bỏ qua phần này nếu chỉ sửa file trong module của mình -->

- [ ] Có sửa `db/*.sql` → đã báo cả nhóm chạy lại `.\db\setup-db.ps1`
- [ ] Có sửa interface trong `SAT.Services/Abstractions/` → đã báo người đang dùng interface đó
- [ ] Có thêm package → đã thêm version vào `Directory.Packages.props`, không ghi version trong `.csproj`

## Ảnh chụp màn hình

<!-- Bắt buộc nếu PR có thay đổi giao diện - @Ngọc Ánh nghiệm thu UI dựa vào đây -->
