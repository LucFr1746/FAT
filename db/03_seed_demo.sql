/* =============================================================================
   SAT - Student Academic Tracker
   03_seed_demo.sql : Tài khoản + dữ liệu học tập mẫu.

   Chạy SAU 02_seed_master.sql.

   TÀI KHOẢN DEMO
   ---------------------------------------------------------------------------
     admin      / Admin@123     (Admin)
     student01  / Student@123   (SE170001 - năm cuối, ~70% tiến độ, có 1 môn học lại)
     student02  / Student@123   (SE180002 - năm 2, ~53% tiến độ)
     student03  / Student@123   (SE190003 - năm nhất, ~14% tiến độ)
   ---------------------------------------------------------------------------

   Điểm được sinh bằng CÔNG THỨC TẤT ĐỊNH dựa trên Id, KHÔNG dùng RAND().
   Nhờ vậy mọi thành viên chạy script đều ra đúng cùng một bộ số => khi so
   Dashboard giữa 2 máy mà lệch thì chắc chắn là lỗi code, không phải lỗi data.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE SAT;
GO

BEGIN TRANSACTION;
GO

/* -----------------------------------------------------------------------------
   Tài khoản

   PasswordHash là hash BCrypt thật (work factor 11), đã verify bằng
   BCrypt.Net.Verify trước khi đưa vào đây. KHÔNG có mật khẩu thô nào trong DB.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.AppUser (Username, PasswordHash, RoleId)
SELECT u.Username, u.PasswordHash, r.RoleId
FROM (VALUES
    (N'admin',     N'$2a$11$JJQiWDIKwyl.f89GLxktb.lx2BSbc.XhflOzX9V993TDFW0fQsAzW', N'Admin'),
    (N'student01', N'$2a$11$QhwqYB24gDf/DvwQ4Ccs/e5iK.ij6QcbYOb1gu.HJ6iF5HKLGEhXe', N'Student'),
    (N'student02', N'$2a$11$QhwqYB24gDf/DvwQ4Ccs/e5iK.ij6QcbYOb1gu.HJ6iF5HKLGEhXe', N'Student'),
    (N'student03', N'$2a$11$QhwqYB24gDf/DvwQ4Ccs/e5iK.ij6QcbYOb1gu.HJ6iF5HKLGEhXe', N'Student')
) AS u(Username, PasswordHash, RoleName)
JOIN dbo.Role r ON r.RoleName = u.RoleName;
GO

/* -----------------------------------------------------------------------------
   Hồ sơ sinh viên
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Student (UserId, StudentCode, FullName, Email, DateOfBirth, EnrollmentDate, MajorId, Status)
SELECT au.UserId, s.StudentCode, s.FullName, s.Email, s.DateOfBirth, s.EnrollmentDate, m.MajorId, N'Active'
FROM (VALUES
    (N'student01', N'SE170001', N'Trần Nhật Long',  N'longtn.se170001@fpt.edu.vn', '2003-04-12', '2024-01-08'),
    (N'student02', N'SE180002', N'Nguyễn Minh Anh', N'anhnm.se180002@fpt.edu.vn',  '2004-09-30', '2025-01-06'),
    (N'student03', N'SE190003', N'Lê Hoàng Phúc',   N'phuclh.se190003@fpt.edu.vn', '2005-11-21', '2026-01-05')
) AS s(Username, StudentCode, FullName, Email, DateOfBirth, EnrollmentDate)
JOIN dbo.AppUser au ON au.Username = s.Username
CROSS JOIN dbo.Major m
WHERE m.MajorCode = N'SE';
GO

/* -----------------------------------------------------------------------------
   Enrollment - lộ trình học của 3 sinh viên

   student01 cố ý TRƯỢT CSD201 ở FA24 rồi HỌC LẠI ở SP25. Đây là dữ liệu để
   kiểm tra quy tắc "chỉ tính lần thi cuối vào GPA" (IsCounted) - lỗi rất dễ
   mắc và sinh ra GPA cao bất thường nếu tính cả 2 lần.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Enrollment (StudentId, CourseId, SemesterId)
SELECT st.StudentId, c.CourseId, sm.SemesterId
FROM (VALUES
    -- ===== student01: kỳ 1..7 đã xong, kỳ hiện tại đang thực tập =====
    (N'SE170001', N'CSI104',  N'SP24'), (N'SE170001', N'PRF192',  N'SP24'),
    (N'SE170001', N'MAE101',  N'SP24'), (N'SE170001', N'CEA201',  N'SP24'),
    (N'SE170001', N'SSL101c', N'SP24'),

    (N'SE170001', N'PRO192',  N'SU24'), (N'SE170001', N'MAD101',  N'SU24'),
    (N'SE170001', N'OSG202',  N'SU24'), (N'SE170001', N'NWC204',  N'SU24'),
    (N'SE170001', N'SSG104',  N'SU24'),

    (N'SE170001', N'CSD201',  N'FA24'),          -- lần 1: sẽ TRƯỢT
    (N'SE170001', N'DBI202',  N'FA24'), (N'SE170001', N'LAB211',  N'FA24'),
    (N'SE170001', N'WED201c', N'FA24'), (N'SE170001', N'IOT102',  N'FA24'),

    (N'SE170001', N'CSD201',  N'SP25'),          -- lần 2: học lại, ĐẬU
    (N'SE170001', N'PRJ301',  N'SP25'), (N'SE170001', N'SWE201c', N'SP25'),
    (N'SE170001', N'MAS291',  N'SP25'), (N'SE170001', N'ITE302c', N'SP25'),

    (N'SE170001', N'PRN212',  N'SU25'), (N'SE170001', N'SWP391',  N'SU25'),
    (N'SE170001', N'SWT301',  N'SU25'),

    (N'SE170001', N'PRN222',  N'FA25'), (N'SE170001', N'SWR302',  N'FA25'),
    (N'SE170001', N'SDN302',  N'FA25'),

    (N'SE170001', N'PMG201c', N'SP26'), (N'SE170001', N'EXE101',  N'SP26'),
    (N'SE170001', N'MLN111',  N'SP26'),

    (N'SE170001', N'OJT202',  N'SU26'),          -- kỳ hiện tại: đang học

    -- ===== student02: kỳ 1..4 đã xong, kỳ 5 đang học =====
    (N'SE180002', N'CSI104',  N'SP25'), (N'SE180002', N'PRF192',  N'SP25'),
    (N'SE180002', N'MAE101',  N'SP25'), (N'SE180002', N'CEA201',  N'SP25'),
    (N'SE180002', N'SSL101c', N'SP25'),

    (N'SE180002', N'PRO192',  N'SU25'), (N'SE180002', N'MAD101',  N'SU25'),
    (N'SE180002', N'OSG202',  N'SU25'), (N'SE180002', N'NWC204',  N'SU25'),
    (N'SE180002', N'SSG104',  N'SU25'),

    (N'SE180002', N'CSD201',  N'FA25'), (N'SE180002', N'DBI202',  N'FA25'),
    (N'SE180002', N'LAB211',  N'FA25'), (N'SE180002', N'WED201c', N'FA25'),
    (N'SE180002', N'IOT102',  N'FA25'),

    (N'SE180002', N'PRJ301',  N'SP26'), (N'SE180002', N'SWE201c', N'SP26'),
    (N'SE180002', N'MAS291',  N'SP26'), (N'SE180002', N'ITE302c', N'SP26'),

    (N'SE180002', N'PRN212',  N'SU26'), (N'SE180002', N'SWP391',  N'SU26'),
    (N'SE180002', N'SWT301',  N'SU26'),          -- kỳ hiện tại: đang học

    -- ===== student03: năm nhất =====
    (N'SE190003', N'CSI104',  N'SP26'), (N'SE190003', N'PRF192',  N'SP26'),
    (N'SE190003', N'MAE101',  N'SP26'), (N'SE190003', N'CEA201',  N'SP26'),
    (N'SE190003', N'SSL101c', N'SP26'),

    (N'SE190003', N'PRO192',  N'SU26'), (N'SE190003', N'MAD101',  N'SU26'),
    (N'SE190003', N'OSG202',  N'SU26'), (N'SE190003', N'NWC204',  N'SU26'),
    (N'SE190003', N'SSG104',  N'SU26')           -- kỳ hiện tại: đang học
) AS e(StudentCode, CourseCode, SemesterCode)
JOIN dbo.Student  st ON st.StudentCode  = e.StudentCode
JOIN dbo.Course   c  ON c.CourseCode    = e.CourseCode
JOIN dbo.Semester sm ON sm.SemesterCode = e.SemesterCode;
GO

/* -----------------------------------------------------------------------------
   Grade - điểm thành phần

   Công thức tất định:
     base  = 5.8 + ((StudentId * 7 + CourseId * 13) % 38) / 10.0   -> 5.8 .. 9.5
     score = base + (((AssessmentId * 3 + EnrollmentId * 5) % 11) - 5) / 10.0
   tức là dao động +/- 0.5 quanh base, rồi kẹp về [0, 10].
   ----------------------------------------------------------------------------- */

DECLARE @CurrentOrder INT = (SELECT DisplayOrder FROM dbo.Semester WHERE IsCurrent = 1);

-- (a) Kỳ ĐÃ KẾT THÚC: có đủ mọi đầu điểm
INSERT INTO dbo.Grade (EnrollmentId, AssessmentId, Score)
SELECT
    e.EnrollmentId,
    a.AssessmentId,
    CAST(
        CASE
            WHEN raw.Val < 0  THEN 0
            WHEN raw.Val > 10 THEN 10
            ELSE raw.Val
        END AS DECIMAL(4,2))
FROM dbo.Enrollment e
JOIN dbo.Semester   s ON s.SemesterId = e.SemesterId
JOIN dbo.Assessment a ON a.CourseId   = e.CourseId
CROSS APPLY (
    SELECT Val =
        (5.8 + ((e.StudentId * 7 + e.CourseId * 13) % 38) / 10.0)
      + (((a.AssessmentId * 3 + e.EnrollmentId * 5) % 11) - 5) / 10.0
) AS raw
WHERE s.DisplayOrder < @CurrentOrder;

-- (b) Kỳ HIỆN TẠI: mới có điểm quá trình, chưa thi cuối kỳ
--     => FinalScore vẫn NULL, Status vẫn 'Studying'. Đây là đường đi thật của
--        màn hình "môn đang học" nên phải có dữ liệu để test.
INSERT INTO dbo.Grade (EnrollmentId, AssessmentId, Score)
SELECT
    e.EnrollmentId,
    a.AssessmentId,
    CAST(
        CASE
            WHEN raw.Val < 0  THEN 0
            WHEN raw.Val > 10 THEN 10
            ELSE raw.Val
        END AS DECIMAL(4,2))
FROM dbo.Enrollment e
JOIN dbo.Semester   s ON s.SemesterId = e.SemesterId
JOIN dbo.Assessment a ON a.CourseId   = e.CourseId
CROSS APPLY (
    SELECT Val =
        (5.8 + ((e.StudentId * 7 + e.CourseId * 13) % 38) / 10.0)
      + (((a.AssessmentId * 3 + e.EnrollmentId * 5) % 11) - 5) / 10.0
) AS raw
WHERE s.DisplayOrder = @CurrentOrder
  AND a.Name IN (N'Assignment', N'Progress Test', N'Supervisor Evaluation');
GO

/* -----------------------------------------------------------------------------
   Ép student01 TRƯỢT CSD201 ở lần học đầu (FA24)

   Điểm thi cuối kỳ 3.0 < MinScoreToPass 4.0 => trượt môn theo quy chế, kể cả
   khi điểm tổng kết có thể vẫn >= 5. Đây chính là ca kiểm thử "trượt vì điểm
   thành phần" trong docs/plan §9.
   ----------------------------------------------------------------------------- */
UPDATE g
SET Score = CASE WHEN a.Name = N'Final Exam' THEN 3.00 ELSE 4.50 END
FROM dbo.Grade g
JOIN dbo.Assessment a ON a.AssessmentId = g.AssessmentId
JOIN dbo.Enrollment e ON e.EnrollmentId = g.EnrollmentId
JOIN dbo.Student    s ON s.StudentId    = e.StudentId
JOIN dbo.Course     c ON c.CourseId     = e.CourseId
JOIN dbo.Semester   m ON m.SemesterId   = e.SemesterId
WHERE s.StudentCode = N'SE170001'
  AND c.CourseCode  = N'CSD201'
  AND m.SemesterCode = N'FA24';
GO

/* -----------------------------------------------------------------------------
   Chốt kết quả môn: FinalScore, LetterGrade, GradePoint, Status

   Đây CHÍNH LÀ công thức mà GradeService trong code phải cho ra kết quả giống
   hệt. Nếu app tính khác seed => app sai.
   ----------------------------------------------------------------------------- */
WITH Calc AS (
    SELECT
        g.EnrollmentId,
        -- Điểm tổng kết = tổng có trọng số, làm tròn 1 chữ số thập phân
        ROUND(SUM(g.Score * a.Weight), 1) AS FinalScore,
        -- Số đầu điểm rơi dưới điểm sàn riêng của nó
        SUM(CASE WHEN a.MinScoreToPass IS NOT NULL AND g.Score < a.MinScoreToPass
                 THEN 1 ELSE 0 END)       AS MinViolations,
        COUNT(*)                          AS GradedCount
    FROM dbo.Grade g
    JOIN dbo.Assessment a ON a.AssessmentId = g.AssessmentId
    GROUP BY g.EnrollmentId
),
Complete AS (
    -- Chỉ chốt điểm khi đã có ĐỦ mọi đầu điểm của môn đó
    SELECT c.*
    FROM Calc c
    JOIN dbo.Enrollment e ON e.EnrollmentId = c.EnrollmentId
    WHERE c.GradedCount = (SELECT COUNT(*) FROM dbo.Assessment a2 WHERE a2.CourseId = e.CourseId)
)
UPDATE e
SET FinalScore  = cp.FinalScore,
    LetterGrade = gs.LetterGrade,
    GradePoint  = gs.GradePoint,
    Status      = CASE WHEN cp.FinalScore >= 5.0 AND cp.MinViolations = 0
                       THEN N'Passed' ELSE N'Failed' END,
    UpdatedAt   = SYSUTCDATETIME()
FROM dbo.Enrollment e
JOIN Complete cp       ON cp.EnrollmentId = e.EnrollmentId
JOIN dbo.GradeScale gs ON cp.FinalScore >= gs.MinScore AND cp.FinalScore < gs.MaxScore;
GO

/* -----------------------------------------------------------------------------
   AttemptNo + IsCounted

   Quy tắc: với mỗi (sinh viên, môn học), chỉ LẦN HỌC MỚI NHẤT được tính vào
   GPA. Các lần trước giữ lại trong bảng điểm để xem lịch sử nhưng IsCounted=0.
   ----------------------------------------------------------------------------- */
WITH Ranked AS (
    SELECT
        e.EnrollmentId,
        ROW_NUMBER() OVER (PARTITION BY e.StudentId, e.CourseId ORDER BY s.DisplayOrder ASC)  AS AttemptAsc,
        ROW_NUMBER() OVER (PARTITION BY e.StudentId, e.CourseId ORDER BY s.DisplayOrder DESC) AS AttemptDesc
    FROM dbo.Enrollment e
    JOIN dbo.Semester s ON s.SemesterId = e.SemesterId
)
UPDATE e
SET AttemptNo = r.AttemptAsc,
    IsCounted = CASE WHEN r.AttemptDesc = 1 THEN 1 ELSE 0 END
FROM dbo.Enrollment e
JOIN Ranked r ON r.EnrollmentId = e.EnrollmentId;
GO

/* -----------------------------------------------------------------------------
   AcademicPlan - kế hoạch cho phần còn lại
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.AcademicPlan (StudentId, PlanName, Note)
SELECT s.StudentId, p.PlanName, p.Note
FROM (VALUES
    (N'SE170001', N'Kế hoạch tốt nghiệp 2027', N'Hoàn tất kỳ 9 trong SP27'),
    (N'SE180002', N'Kế hoạch năm 3',           N'Dự kiến kỳ 6 vào FA26')
) AS p(StudentCode, PlanName, Note)
JOIN dbo.Student s ON s.StudentCode = p.StudentCode;
GO

INSERT INTO dbo.AcademicPlanItem (PlanId, CourseId, SemesterId, TargetTermNo, ExpectedScore, DisplayOrder)
SELECT pl.PlanId, c.CourseId, sm.SemesterId, i.TargetTermNo, i.ExpectedScore, i.DisplayOrder
FROM (VALUES
    (N'SE170001', N'SEP490',  N'SP27', 9, CAST(8.5 AS DECIMAL(4,2)), 1),
    (N'SE170001', N'EXE201',  N'SP27', 9, CAST(8.0 AS DECIMAL(4,2)), 2),
    (N'SE180002', N'PRN222',  N'FA26', 6, CAST(7.5 AS DECIMAL(4,2)), 1),
    (N'SE180002', N'SWR302',  N'FA26', 6, CAST(8.0 AS DECIMAL(4,2)), 2),
    (N'SE180002', N'SDN302',  N'FA26', 6, CAST(7.0 AS DECIMAL(4,2)), 3)
) AS i(StudentCode, CourseCode, SemesterCode, TargetTermNo, ExpectedScore, DisplayOrder)
JOIN dbo.Student      s  ON s.StudentCode   = i.StudentCode
JOIN dbo.AcademicPlan pl ON pl.StudentId    = s.StudentId
JOIN dbo.Course       c  ON c.CourseCode    = i.CourseCode
JOIN dbo.Semester     sm ON sm.SemesterCode = i.SemesterCode;
GO

/* -----------------------------------------------------------------------------
   Material - tài liệu học tập mẫu

   Nội dung là văn bản thật (không phải byte rỗng) để nút Tải xuống mở ra được
   file có nội dung ngay từ lần chạy đầu, không cần ai tải file lên trước.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Material (CourseId, Title, Description, Category, FileName, ContentType, FileSizeBytes, UploadedByUserId)
SELECT c.CourseId, m.Title, m.Description, m.Category, m.FileName, m.ContentType,
       DATALENGTH(CONVERT(VARBINARY(MAX), m.Body)), au.UserId
FROM (VALUES
    (N'PRF192',  N'PRF192 - Slide chương 1: Nhập môn C',      N'Biến, kiểu dữ liệu, toán tử',            N'Slide',     N'PRF192-Chapter01.txt', N'text/plain', N'PRF192 - Chuong 1: Nhap mon lap trinh C.'),
    (N'PRF192',  N'PRF192 - Bài tập thực hành tuần 1',        N'10 bài tập vòng lặp và mảng',            N'Exercise',  N'PRF192-Lab01.txt',     N'text/plain', N'PRF192 - Bai tap tuan 1: vong lap for, while, mang mot chieu.'),
    (N'PRO192',  N'PRO192 - Tổng hợp OOP với Java',           N'Kế thừa, đa hình, đóng gói',             N'Textbook',  N'PRO192-OOP-Notes.txt', N'text/plain', N'PRO192 - Tong hop OOP: Encapsulation, Inheritance, Polymorphism, Abstraction.'),
    (N'DBI202',  N'DBI202 - Đề thi mẫu cuối kỳ',              N'Đề tham khảo có đáp án',                 N'Exam',      N'DBI202-SampleFE.txt',  N'text/plain', N'DBI202 - De thi mau: thiet ke ERD, chuan hoa 3NF, truy van SQL nang cao.'),
    (N'DBI202',  N'DBI202 - Slide chuẩn hóa CSDL',            N'1NF, 2NF, 3NF, BCNF',                    N'Slide',     N'DBI202-Normalization.txt', N'text/plain', N'DBI202 - Chuan hoa co so du lieu: 1NF, 2NF, 3NF, BCNF kem vi du.'),
    (N'CSD201',  N'CSD201 - Cấu trúc dữ liệu và giải thuật',  N'Danh sách liên kết, cây, đồ thị',        N'Textbook',  N'CSD201-DSA.txt',       N'text/plain', N'CSD201 - Linked List, Stack, Queue, Binary Tree, Graph traversal.'),
    (N'PRN212',  N'PRN212 - Hướng dẫn WPF và MVVM',           N'Binding, Command, DataTemplate',         N'Reference', N'PRN212-WPF-MVVM.txt',  N'text/plain', N'PRN212 - WPF MVVM: DataBinding, ICommand, DataTemplate, DependencyInjection.'),
    (NULL,       N'Sổ tay sinh viên',                         N'Quy chế học vụ và thang điểm',           N'Reference', N'Student-Handbook.txt', N'text/plain', N'So tay sinh vien: quy che hoc vu, thang diem 10, dieu kien tot nghiep.')
) AS m(CourseCode, Title, Description, Category, FileName, ContentType, Body)
LEFT JOIN dbo.Course c ON c.CourseCode = m.CourseCode
CROSS JOIN (SELECT UserId FROM dbo.AppUser WHERE Username = N'admin') AS au;
GO

-- Nội dung file, ghép theo đúng thứ tự đã chèn ở trên
INSERT INTO dbo.MaterialFile (MaterialId, Content)
SELECT m.MaterialId, CONVERT(VARBINARY(MAX), b.Body)
FROM dbo.Material m
JOIN (VALUES
    (N'PRF192-Chapter01.txt',     N'PRF192 - Chuong 1: Nhap mon lap trinh C.'),
    (N'PRF192-Lab01.txt',         N'PRF192 - Bai tap tuan 1: vong lap for, while, mang mot chieu.'),
    (N'PRO192-OOP-Notes.txt',     N'PRO192 - Tong hop OOP: Encapsulation, Inheritance, Polymorphism, Abstraction.'),
    (N'DBI202-SampleFE.txt',      N'DBI202 - De thi mau: thiet ke ERD, chuan hoa 3NF, truy van SQL nang cao.'),
    (N'DBI202-Normalization.txt', N'DBI202 - Chuan hoa co so du lieu: 1NF, 2NF, 3NF, BCNF kem vi du.'),
    (N'CSD201-DSA.txt',           N'CSD201 - Linked List, Stack, Queue, Binary Tree, Graph traversal.'),
    (N'PRN212-WPF-MVVM.txt',      N'PRN212 - WPF MVVM: DataBinding, ICommand, DataTemplate, DependencyInjection.'),
    (N'Student-Handbook.txt',     N'So tay sinh vien: quy che hoc vu, thang diem 10, dieu kien tot nghiep.')
) AS b(FileName, Body) ON b.FileName = m.FileName;
GO

COMMIT TRANSACTION;
GO

/* =============================================================================
   Tự kiểm tra
   ============================================================================= */

-- 0. Mọi tài liệu đều phải có nội dung file đi kèm, và kích thước phải khớp
IF EXISTS (
    SELECT 1 FROM dbo.Material m
    LEFT JOIN dbo.MaterialFile f ON f.MaterialId = m.MaterialId
    WHERE f.MaterialId IS NULL OR DATALENGTH(f.Content) <> m.FileSizeBytes
)
BEGIN
    THROW 50010, N'[03_seed] LOI: co tai lieu thieu noi dung hoac sai FileSizeBytes.', 1;
END
GO

-- 1. student01 phải có đúng 1 môn trượt và môn đó phải bị loại khỏi GPA
IF NOT EXISTS (
    SELECT 1 FROM dbo.Enrollment e
    JOIN dbo.Student s ON s.StudentId = e.StudentId
    JOIN dbo.Course  c ON c.CourseId  = e.CourseId
    WHERE s.StudentCode = N'SE170001' AND c.CourseCode = N'CSD201'
      AND e.Status = N'Failed' AND e.IsCounted = 0
)
BEGIN
    THROW 50011, N'[03_seed] LOI: ca hoc lai CSD201 cua SE170001 khong dung trang thai.', 1;
END

-- 2. Không sinh viên nào được có 2 lần học cùng 1 môn cùng IsCounted = 1
IF EXISTS (
    SELECT StudentId, CourseId FROM dbo.Enrollment
    WHERE IsCounted = 1
    GROUP BY StudentId, CourseId HAVING COUNT(*) > 1
)
BEGIN
    THROW 50012, N'[03_seed] LOI: mot mon bi tinh vao GPA nhieu hon 1 lan.', 1;
END

-- 3. Môn của kỳ hiện tại phải còn đang học (chưa chốt điểm)
IF EXISTS (
    SELECT 1 FROM dbo.Enrollment e
    JOIN dbo.Semester s ON s.SemesterId = e.SemesterId
    WHERE s.IsCurrent = 1 AND e.Status <> N'Studying'
)
BEGIN
    THROW 50013, N'[03_seed] LOI: mon o ky hien tai da bi chot diem.', 1;
END

-- 4. Mọi môn đã kết thúc đều phải có điểm chữ
IF EXISTS (
    SELECT 1 FROM dbo.Enrollment
    WHERE Status IN (N'Passed', N'Failed') AND (FinalScore IS NULL OR LetterGrade IS NULL)
)
BEGIN
    THROW 50014, N'[03_seed] LOI: co mon da ket thuc nhung thieu diem tong ket.', 1;
END
GO

PRINT '[03_seed_demo] OK - da nap tai khoan va ket qua hoc tap mau.';
GO
