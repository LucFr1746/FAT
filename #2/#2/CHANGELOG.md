# Changelog

## 1. Quy định ghi Changelog

File này dùng để ghi lại các thay đổi quan trọng trong quá trình thực hiện bài tập, lab, assignment hoặc project.

Nguyên tắc ghi changelog:

- Chỉ ghi những gì đã hoàn thành thật sự.
- Không ghi kế hoạch nếu chưa thực hiện.
- Mỗi thay đổi nên có ngày, nội dung, người thực hiện và minh chứng.
- Nếu có AI hỗ trợ, cần ghi rõ AI đã hỗ trợ phần nào.
- Nếu có commit GitHub, cần ghi link commit.
- Nếu có lỗi đã sửa, cần ghi rõ lỗi, nguyên nhân và cách xử lý.

---

## 2. Thông tin project

| Thông tin | Nội dung |
|---|---|
| Môn học | Software Development Project |
| Mã môn học | SWP391 |
| Lớp | SE20A02 |
| Học kỳ | SU26 |
| Tên bài tập / Project | TripGenie |
| Tên sinh viên / Nhóm | Nguyễn Hoàng Ngọc Ánh, Đoàn Thế Lực, Trương Văn Hiếu, Nguyễn La Hòa An, Trần Nhất Long |
| MSSV / Danh sách MSSV | DE200147, DE200523, DE190105, DE201043, DE200160 |
| Giảng viên hướng dẫn | QuangLTN3 |
| Repository URL | https://github.com/fptu-se-su26/swp391-su26-ai-audit-project-swp391_se20a02_group-05 |
| Ngày bắt đầu | 2026-05-11T00:00:00.000Z |
| Ngày hoàn thành | 2026-07-19T00:00:00.000Z |

---

## 3. Tổng quan các phiên bản/giai đoạn

| Phiên bản/Giai đoạn | Thời gian | Nội dung chính | Trạng thái |
|---|---|---|---|
| Phase 01 | 2026-06-11 ~ 2026-06-11 | Khởi tạo project | In Progress |
| Phase 02 |  |  | Not Started |
| Phase 03 |  |  | Not Started |
| Phase 04 |  |  | Not Started |
| Phase 05 |  |  | Not Started |
| Phase 06 |  |  | Not Started |

---

# [Phase 01] 

## Thông tin giai đoạn

- **Thời gian thực hiện:** 2026-06-11 ~ 2026-06-11
- **Mô tả giai đoạn:** Khởi tạo project
- **Trạng thái hiện tại:** In Progress

## Thay đổi chi tiết

| STT | Nội dung thay đổi | Người thực hiện | File/Module liên quan | Minh chứng |
|---:|---|---|---|---|
| 1 |  |  |  |  |

## AI có hỗ trợ không?

- [ ] Có
- [x] Không

## Minh chứng liên quan

| Loại minh chứng | Nhãn | Nội dung |
|---|---|---|
| File/Commit |  |  |

## Ghi chú

```text
 
```

---

# 4. Tổng kết thay đổi cuối project

## 4.1. Các chức năng đã hoàn thành

```text
Phân tích và định nghĩa thành công tất cả các Tác nhân (Actor) cốt lõi của hệ thống bao gồm: Người dùng Doanh nghiệp (Business User), Quản trị viên hệ thống (Admin), và Dịch vụ xác thực bên thứ ba.

Xây dựng và hoàn thiện toàn bộ sơ đồ Use Case tổng thể cho nền tảng CVerify, tập trung vào luồng xử lý định danh doanh nghiệp và quy trình quản lý nghiệp vụ của Admin.

Xác định rõ ràng các mối quan hệ kỹ thuật (<<include>> và <<extend>>) cho các tính năng thiết yếu như tải lên tài liệu pháp lý, xác thực OTP và ghi nhận nhật ký hệ thống.
```

---

## 4.2. Các chức năng chưa hoàn thành

```text
Các Use Case chuyên sâu liên quan đến việc liên kết trực tiếp với API của Cổng thông tin đăng ký doanh nghiệp quốc gia hiện tại mới chỉ dừng ở mức giả lập (Mockup) do giới hạn quyền truy cập trong giai đoạn thiết kế.

Sơ đồ tuần tự (Sequence Diagram) mô tả chi tiết các trạng thái xác thực phức tạp được hoãn lại để thực hiện trong phase tiếp theo.
```

---

## 4.3. Cải thiện chính

```text
Tinh gọn luồng quy trình xác thực bằng cách loại bỏ và gộp các bước phê duyệt trung gian rườm rà, giúp giảm thiểu số lượng truy vấn database khi chạy hệ thống log trên VPS sau này.

Tách biệt thành công tính năng lưu nháp hồ sơ doanh nghiệp ra khỏi luồng bắt buộc xác thực ngay lập tức, giúp sửa đổi logic nghiệp vụ hợp lý hơn và tối ưu hóa trải nghiệm người dùng.
```

---

## 4.4. Tổng kết project

```text
Giai đoạn thiết kế Use Case cho dự án CVerify đã hoàn thành xuất sắc, đạt độ bao phủ trên 90% yêu cầu nghiệp vụ đặt ra. Sơ đồ này đóng vai trò như một bản thiết kế kỹ thuật giúp toàn đội thống nhất logic, xóa bỏ các điểm mơ hồ và sẵn sàng cho việc phân rã task code Backend/Frontend tiếp theo.
```

---

## 4.5. Hướng cải thiện tiếp theo

```text
Mở rộng và viết tài liệu đặc tả chi tiết (Detail Use Case) cho tính năng sử dụng AI để phân tích và phát hiện bất thường trong nhật ký kiểm toán (AI Audit Log).

Thiết kế bổ sung các Use Case chuyên sâu về bảo mật hệ thống, quản lý phân quyền dựa trên vai trò (RBAC) và xử lý các kịch bản lỗi khi deploy thực tế trên môi trường VPS.
```

---

# 5. Cam kết cập nhật Changelog

Sinh viên/nhóm cam kết rằng nội dung changelog phản ánh đúng các thay đổi đã thực hiện trong quá trình làm bài tập/project.

| Đại diện sinh viên/nhóm | Ngày xác nhận |
|---|---|
| Nguyễn Hoàng Ngọc Ánh | 11/6/2026 |
