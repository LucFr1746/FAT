# AI Learning Reflection

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
| Ngày hoàn thành reflection | 2026-06-11 |

---

## 2. Mục đích Reflection

File này dùng để sinh viên/nhóm tự đánh giá quá trình sử dụng AI trong học tập...

---

## 3. Tóm tắt quá trình sử dụng AI

```text
Em sử dụng AI ở giai đoạn đầu của dự án nhằm mục đích Brainstorming ý tưởng và chuẩn hóa logic hệ thống. AI giúp nhóm nhanh chóng liệt kê đầy đủ các trường hợp tương tác của người dùng B2B, đảm bảo sơ đồ Use Case bao phủ được toàn bộ các luồng nghiệp vụ đặc thù của hệ thống CVerify mà không bị bỏ sót.
```

---

## 4. Công cụ AI đã sử dụng

- [x] ChatGPT
- [x] Gemini
- [ ] Claude
- [ ] GitHub Copilot
- [ ] Cursor
- [ ] Antigravity
- [ ] Microsoft Copilot
- [ ] Perplexity
- [ ] Công cụ khác: ....................................

### Công cụ được sử dụng nhiều nhất

```text
ChatGPT
```

### Lý do sử dụng công cụ đó

```text
Tiết kiệm thời gian, Hỗ trợ brainstorming
```

---

## 5. AI đã hỗ trợ em/nhóm ở điểm nào?

- [x] Hiểu yêu cầu đề bài
- [ ] Phân tích bài toán
- [x] Tìm ý tưởng giải pháp
- [ ] Thiết kế database
- [ ] Thiết kế giao diện
- [ ] Thiết kế kiến trúc hệ thống
- [ ] Viết code mẫu
- [ ] Debug lỗi
- [ ] Viết test case
- [ ] Review code
- [ ] Tối ưu code
- [ ] Kiểm tra bảo mật
- [ ] Viết báo cáo
- [ ] Chuẩn bị thuyết trình
- [ ] Tìm hiểu công nghệ mới

### Mô tả chi tiết

```text
 
```

---

## 6. AI có giúp em/nhóm học tốt hơn không?

### 6.1. Những điểm AI giúp em/nhóm học tốt hơn

```text
AI giúp em phân biệt rõ ràng giữa một "chức năng phần mềm" thông thường và một "Use Case" chuẩn kỹ thuật. Qua việc tương tác và phản biện luồng đi của dự án CVerify, em hiểu sâu hơn cách áp dụng đúng các mối quan hệ include và extend dựa trên các điều kiện ràng buộc thực tế của hệ thống.
```

### 6.2. Những điểm AI chưa giúp tốt hoặc gây khó khăn

```text
AI thường gợi ý quy trình dựa theo các hệ thống quản lý doanh nghiệp chung chung của nước ngoài, chưa sát với quy định pháp lý và quy trình xác thực doanh nghiệp thực tế tại Việt Nam, bắt buộc nhóm phải mất thời gian ngồi chọn lọc và điều chỉnh thủ công lại sơ đồ.
```

### 6.3. Em/nhóm có bị phụ thuộc vào AI không?

- [ ] Không phụ thuộc
- [x] Phụ thuộc ít
- [ ] Phụ thuộc trung bình
- [ ] Phụ thuộc nhiều

Giải thích:

```text
Sử dụng AI để tối ưu hóa thời gian nghiên cứu và tạo cấu trúc ban đầu.
```

---

## 7. Em/nhóm đã kiểm tra kết quả AI như thế nào?

- [ ] Chạy thử chương trình
- [ ] Kiểm tra output
- [ ] Viết test case
- [ ] So sánh với yêu cầu đề bài
- [ ] Đối chiếu với tài liệu môn học
- [ ] Review code
- [ ] Hỏi lại giảng viên
- [ ] Tra cứu tài liệu chính thống
- [ ] Thảo luận với thành viên nhóm
- [ ] Kiểm tra bằng dữ liệu mẫu
- [ ] So sánh trước và sau khi dùng AI

### Mô tả quá trình kiểm chứng

```text
Sau khi nhận danh sách Use Case , em đã đối chiếu lại trực tiếp với file đặc tả yêu cầu hệ thống ban đầu của dự án CVerify. Tiếp theo, em đem danh sách này ra thảo luận cùng cả nhóm làm Frontend và Backend để kiểm tra tính khả thi, phân tích luồng đi của giao diện và tính logic của các mối quan hệ cấu thành sơ đồ.
```

### Ví dụ cụ thể về một lần kiểm chứng

| Nội dung | Mô tả |
|---|---|
| AI đã gợi ý gì? | AI gợi ý thiết lập mối quan hệ bắt buộc (<<include>>) giữa Use Case "Tải hồ sơ doanh nghiệp lên hệ thống" và Use Case "Xác thực OTP qua số điện thoại/Chữ ký số". |
| Em/nhóm đã kiểm tra bằng cách nào? | Nhóm đã cùng nhau thảo luận, rà soát lại tài liệu SRS và chạy thử nghiệm tư duy luồng đi của giao diện (UI Flow) từ góc độ trải nghiệm người dùng thực tế. |
| Kết quả kiểm tra | Sai một phần về mặt logic nghiệp vụ và trải nghiệm người dùng. Nếu bắt buộc bao gồm (<<include>>), người dùng sẽ không thể nhấn lưu nháp thông tin hồ sơ doanh nghiệp khi chưa hoàn thành bước nhập OTP, gây bất tiện lớn trong quá trình đăng ký. |
| Em/nhóm đã xử lý tiếp như thế nào? | Nhóm quyết định chủ động sửa lại mối quan hệ trong sơ đồ thành mở rộng (<<extend>>). Việc xác thực OTP/Chữ ký số sẽ chỉ chính thức kích hoạt làm điều kiện ràng buộc khi người dùng thực hiện hành động gửi duyệt hồ sơ cuối cùng. |

---

## 8. Ví dụ AI gợi ý sai hoặc chưa phù hợp

```text
Trong quá trình thực hiện, em/nhóm chưa ghi nhận trường hợp AI gợi ý sai nghiêm trọng. Tuy nhiên, em/nhóm vẫn kiểm tra lại kết quả AI trước khi sử dụng.
```

---

## 9. Phần đóng góp thật sự của sinh viên/nhóm

Chưa có thông tin đóng góp.

---

## 10. So sánh trước và sau khi dùng AI

| Nội dung | Trước khi dùng AI | Sau khi dùng AI | Cải thiện đạt được |
|---|---|---|---|

---

## 11. Bài học về môn học

- Tầm quan trọng của làm việc nhóm

---

## 12. Bài học về sử dụng AI có trách nhiệm

- Cần kiểm chứng nội dung AI tạo ra
- Tránh sao chép mù quáng kết quả từ AI

---

## 13. Điều em/nhóm sẽ không làm khi sử dụng AI

- [x] Không dùng AI để làm toàn bộ bài mà không hiểu nội dung.
- [x] Không nộp nguyên văn kết quả AI nếu chưa kiểm tra.
- [x] Không che giấu việc sử dụng AI trong các phần quan trọng.
- [x] Không dùng AI để tạo nội dung sai lệch hoặc gian lận.
- [x] Không dùng AI thay thế hoàn toàn quá trình học.
- [x] Không bỏ qua yêu cầu, rubric hoặc hướng dẫn của giảng viên.

### Giải thích thêm nếu có

```text
 
```

---

## 14. Kế hoạch cải thiện lần sau

- Cải thiện quy trình làm việc với Git
- Tìm hiểu sâu hơn về thiết kế hệ thống

---

## 15. Tự đánh giá mức độ hoàn thành

| Tiêu chí | Điểm tự đánh giá 1-5 | Ghi chú |
|---|:---:|---|
| Ghi nhận việc dùng AI trung thực | 5 |   |
| Prompt có mục tiêu rõ ràng | 5 |   |
| Kiểm chứng kết quả AI | 5 |   |
| Tự chỉnh sửa/cải tiến | 5 |   |
| Hiểu nội dung đã nộp | 5 |   |
| Reflection có chiều sâu | 5 |   |
| Sử dụng AI có trách nhiệm | 5 |   |

---

## 16. Câu hỏi tự vấn cuối bài

### 16.1. Nếu giảng viên hỏi về phần AI đã hỗ trợ, em/nhóm có giải thích lại được không?

```text
Có, nhóm đã đọc, kiểm tra và hiểu nội dung trước khi sử dụng.
```

### 16.2. Nếu không có AI, em/nhóm có thể tự làm lại phần quan trọng nhất không?

```text
Có, nhưng sẽ mất nhiều thời gian hơn để nghiên cứu và triển khai.
```

### 16.3. Phần nào trong bài thể hiện rõ nhất năng lực thật sự của em/nhóm?

```text
Phần thiết kế workflow, chỉnh sửa logic và xử lý lỗi thực tế.
```

### 16.4. Em/nhóm muốn cải thiện kỹ năng nào sau bài này?

```text
Kỹ năng thiết kế hệ thống, viết prompt và kiểm thử phần mềm.
```

---

## 17. Cam kết Reflection

| Đại diện sinh viên/nhóm | Ngày xác nhận |
|---|---|
| Nguyễn Hoàng Ngọc Ánh | 11/6/2026 |
