# AI Audit Log

## 1. Thông tin chung

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
| Ngày bắt đầu | 2026-05-11T00:00:00.000Z |
| Ngày hoàn thành | 2026-07-19T00:00:00.000Z |

---

## 2. Công cụ AI đã sử dụng

- [x] ChatGPT
- [x] Gemini
- [ ] Claude
- [ ] GitHub Copilot
- [ ] Cursor
- [ ] Antigravity
- [ ] Perplexity
- [ ] Microsoft Copilot
- [ ] Công cụ khác: ....................................

---

## 3. Mục tiêu sử dụng AI

### Mô tả mục tiêu sử dụng AI

```text
Em sử dụng AI để hỗ trợ phân tích luồng nghiệp vụ của hệ thống CVerify (quản lý và định danh doanh nghiệp), từ đó chuẩn hóa danh sách các Actor (Tác nhân) và xây dựng danh sách Use Case hoàn chỉnh, logic trước khi tiến hành vẽ sơ đồ.
```

## 4. Nhật ký sử dụng AI chi tiết

---

### Lần sử dụng AI số 1

| Nội dung | Thông tin |
|---|---|
| Ngày sử dụng | 2026-06-11 |
| Công cụ AI | ChatGPT |
| Mục đích sử dụng | Nghiên cứu luồng nghiệp vụ hệ thống và Phân rã danh sách Use Case cho nền tảng CVerify. |
| Phần việc liên quan | Coding |
| Mức độ sử dụng | Sinh chính nội dung |

#### 4.1. Prompt đã sử dụng

```text
"Tôi đang xây dựng một hệ thống tên là CVerify chạy trên môi trường VPS, tính năng chính là định danh và quản lý thông tin doanh nghiệp (B2B). Hãy đóng vai một Business Analyst (BA) chuyên nghiệp, phân tích và liệt kê toàn bộ các Tác nhân (Actor) có thể có, đồng thời gợi ý danh sách các Use Case cốt lõi cho luồng Đăng ký/Xác thực hồ sơ doanh nghiệp và luồng phê duyệt nhật ký kiểm toán (Audit Log) phía Admin."
```

#### 4.2. Kết quả AI gợi ý

```text
AI đã bóc tách chính xác 3 tác nhân chính và đề xuất một bộ khung gồm 8 use case cốt lõi (bao gồm luồng tải tài liệu pháp lý, phê duyệt của admin, và hệ thống tự động ghi nhận lịch sử). Đồng thời gợi ý việc sử dụng mã PlantUML để tự động sinh cấu trúc sơ đồ trực qua
```

#### 4.3. Phần sinh viên/nhóm đã sử dụng từ AI

```text
 
```

#### 4.4. Phần sinh viên/nhóm tự chỉnh sửa hoặc cải tiến

```text
 
```

#### 4.5. Minh chứng

| Loại minh chứng | Nhãn | Nội dung |
|---|---|---|
| File/Commit |  |  |

#### 4.6. Nhận xét cá nhân/nhóm

```text
 
```

---

## 5. Bảng tổng hợp mức độ sử dụng AI

| Hạng mục | Không dùng AI | AI hỗ trợ ít | AI hỗ trợ nhiều | AI sinh chính | Ghi chú |
|---|:---:|:---:|:---:|:---:|---|
| Sơ đồ Use Case tổng thể |   |   | x |   | AI đóng vai trò là một người trợ lý để gợi ý khung sườn nghiệp vụ và phản biện lỗi logic, việc chọn lọc và quyết định luồng đi thực tế hoàn toàn do con người thực hiện. |

---

## 6. Các lỗi hoặc hạn chế từ AI

| STT | Lỗi/hạn chế từ AI | Cách phát hiện | Cách xử lý/cải tiến |
|---:|---|---|---|
| 1 | Đưa ra các giải pháp nghiệp vụ quá bao quát theo chuẩn quốc tế, chưa sát với quy định pháp lý và quy trình xác thực doanh nghiệp thực tế tại thị trường Việt Nam. | Phát hiện khi đối chiếu danh sách Use Case do AI đề xuất với tài liệu đặc tả yêu cầu (SRS) ban đầu của dự án CVerify và các văn bản, quy định pháp luật hiện hành về quản lý, đăng ký doanh nghiệp tại Việt Nam. | Chủ động lọc bỏ các use case dư thừa, sửa đổi các mối quan hệ ràng buộc (<<include>> sang <<extend>>) và bổ sung thêm các điều kiện ràng buộc đặc thù của dự án dựa trên sự thống nhất của các thành viên trong nhóm. |

---

## 7. Kiểm chứng kết quả AI

### Nội dung kiểm chứng

```text
Sử dụng phương pháp đối chiếu trực tiếp tài liệu yêu cầu hệ thống (SRS), tổ chức họp thảo luận phản biện chéo giữa các thành viên đảm nhận Frontend và Backend trong nhóm, kết hợp tham khảo thêm ý kiến hướng dẫn chuyên môn từ giảng viên để xác minh tính đúng đắn của sơ đồ.
```

---

## 8. Đóng góp cá nhân hoặc đóng góp nhóm

### 8.1. Đối với bài cá nhân

```text
Em trực tiếp chịu trách nhiệm nghiên cứu luồng nghiệp vụ hệ thống, viết prompt định hướng và tinh lọc kết quả từ AI để lấy ra các Use Case chuẩn. Sau đó, em tự mình sử dụng công cụ thiết kế để vẽ sơ đồ Use Case tổng thể hoàn chỉnh, đồng thời thiết lập sẵn cấu hình môi trường VPS để phục vụ cho các công đoạn triển khai kế tiếp.
```

### 8.2. Đối với bài nhóm

| Thành viên | MSSV | Nhiệm vụ chính | Có sử dụng AI không? | Minh chứng đóng góp |
|---|---|---|---|---|
|  |  |  | Có / Không |  |

---

## 9. Reflection cuối bài

### Xem chi tiết tại REFLECTION.md

---

## 10. Cam kết học thuật

| Đại diện sinh viên/nhóm | Ngày xác nhận |
|---|---|
| Nguyễn Hoàng Ngọc Ánh | 11/6/2026 |
