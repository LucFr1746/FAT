# Prompt Log

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
| Ngày cập nhật gần nhất | 2026-06-11 |

---

## 2. Mục đích của file Prompt Log

File này dùng để ghi lại các prompt quan trọng đã sử dụng trong quá trình thực hiện bài tập, lab, assignment hoặc project.

---

## 3. Công cụ AI đã sử dụng

- [x] ChatGPT
- [ ] Gemini
- [ ] Claude
- [ ] GitHub Copilot
- [ ] Cursor
- [ ] Antigravity
- [ ] Microsoft Copilot
- [ ] Perplexity
- [ ] Công cụ khác: ....................................

---

## 4. Bảng tổng hợp prompt đã sử dụng

| STT | Ngày | Công cụ AI | Mục đích | Prompt tóm tắt | Kết quả chính | Có sử dụng vào bài không? | Minh chứng |
|---:|---|---|---|---|---|---|---|
| 1 | 2026-06-11 | ChatGPT |  | ... | ... | Không |   |

---

## 5. Prompt chi tiết

### Prompt số 1

| Nội dung | Thông tin |
|---|---|
| Ngày sử dụng | 2026-06-11 |
| Công cụ AI | ChatGPT |
| Mục đích |  |
| Phần việc liên quan | Coding |
| Mức độ sử dụng | Hỏi sinh code |

#### 5.1. Prompt nguyên văn

```text
 
```

#### 5.2. Bối cảnh khi viết prompt

```text
 
```

#### 5.3. Kết quả AI trả về

```text
 
```

#### 5.4. Kết quả đã áp dụng vào bài

```text
 
```

#### 5.5. Phần sinh viên/nhóm đã chỉnh sửa hoặc cải tiến

```text
 
```

#### 5.6. Đánh giá chất lượng prompt

- [ ] Prompt rõ ràng
- [ ] Prompt có đủ bối cảnh
- [ ] Prompt còn thiếu thông tin
- [ ] Prompt tạo ra kết quả tốt
- [ ] Prompt tạo ra kết quả chưa phù hợp
- [ ] Cần hỏi lại AI nhiều lần
- [ ] Cần tự kiểm tra và chỉnh sửa nhiều

#### 5.7. Minh chứng liên quan

| Loại minh chứng | Nội dung |
|---|---|
| File/Link |   |

#### 5.8. Ghi chú thêm

```text
 
```

---

## 6. Prompt quan trọng nhất

### 6.1. Prompt được chọn

```text
 
```

### 6.2. Vì sao prompt này quan trọng?

```text
 
```

### 6.3. Kết quả prompt này mang lại

```text
 
```

### 6.4. Sinh viên/nhóm đã kiểm tra kết quả như thế nào?

```text
 
```

### 6.5. Sinh viên/nhóm đã cải tiến gì từ kết quả AI?

```text
 
```

---

## 7. Prompt chưa hiệu quả

```text
Chưa có prompt chưa hiệu quả được ghi nhận.
```

---

## 8. Bài học về cách viết prompt

### 8.1. Khi viết prompt, em/nhóm cần cung cấp thông tin gì để AI trả lời tốt hơn?

```text
Cần cung cấp đầy đủ và chi tiết ngữ cảnh của dự án (hệ thống CVerify làm về mảng định danh và quản lý doanh nghiệp), đối tượng người dùng chính (B2B - Doanh nghiệp), các ràng buộc về mặt quy trình nghiệp vụ pháp lý tại Việt Nam, và giới hạn hạ tầng thực tế (hệ thống chạy trên môi trường VPS cá nhân). Càng đưa ra mục tiêu đầu ra cụ thể (ví dụ: cần danh sách các mối quan hệ kỹ thuật hay cần xuất mã nguồn dạng text như PlantUML), AI sẽ càng trả về kết quả chính xác, tránh bị chung chung.
```

### 8.2. Em/nhóm đã học được gì về cách đặt câu hỏi cho AI?

```text
Em học được cách áp dụng kỹ thuật "Context-Setting" (Thiết lập bối cảnh) và "Role-Playing" (Đóng vai) khi tương tác với AI. Thay vì đặt câu hỏi đơn giản, việc yêu cầu AI đóng vai một Business Analyst (BA) lão luyện hoặc Chuyên gia Kiến trúc Hệ thống giúp câu trả lời trả về có cấu trúc mạch lạc hơn, sử dụng chuẩn các thuật ngữ chuyên ngành công nghệ thông tin và đưa ra được các phản biện logic có chiều sâu về cấu trúc Use Case.
```

### 8.3. Lần sau em/nhóm sẽ cải thiện prompt như thế nào?

```text
Lần tới khi triển khai các bước kỹ thuật sâu hơn như thiết kế Database chi tiết hoặc code các API chức năng, em sẽ chủ động cung cấp trực tiếp sơ đồ Use Case tổng thể đã được chốt này dưới dạng văn bản cấu trúc cho AI đọc trước. Việc này giúp AI hiểu toàn diện bức tranh lớn của hệ thống ngay từ đầu, từ đó hỗ trợ sinh code backend (Java/C#) hoặc cấu hình VPS chính xác, giảm thiểu tối đa thời gian sửa lỗi (debug) do lệch logic nghiệp vụ.
```

---

## 9. Phân loại prompt đã sử dụng

| Loại prompt | Số lượng | Ví dụ prompt tiêu biểu |
|---|---:|---|
| Prompt Coding | 1 |  |

---

## 10. Checklist chất lượng prompt

| Tiêu chí | Đã đạt? | Ghi chú |
|---|:---:|---|
| Prompt có mục tiêu rõ ràng | x | |
| Prompt có đủ bối cảnh | x | |
| Tự kiểm tra và chỉnh sửa | x | |

---

## 11. Cam kết sử dụng prompt minh bạch

| Đại diện sinh viên/nhóm | Ngày xác nhận |
|---|---|
| Nguyễn Hoàng Ngọc Ánh | 11/6/2026 |
