/* =============================================================================
   SAT - Student Academic Tracker
   02_seed_master.sql : Dữ liệu danh mục (Role, GradeScale, Major, Semester,
                        Course, Assessment, Prerequisite, Curriculum).

   Chạy SAU 01_schema.sql.

   Toàn bộ script tra cứu khóa bằng MÃ (CourseCode, MajorCode...) chứ không
   hardcode Id. Nhờ vậy thêm/bớt một dòng ở trên không làm lệch mọi dòng dưới.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE SAT;
GO

BEGIN TRANSACTION;
GO

/* -----------------------------------------------------------------------------
   Role
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Role (RoleName, Description) VALUES
    (N'Admin',   N'Quản trị danh mục môn học, khung chương trình, tài khoản'),
    (N'Student', N'Sinh viên: xem và cập nhật kết quả học tập của chính mình');
GO

/* -----------------------------------------------------------------------------
   GradeScale - quy đổi thang 10 sang điểm chữ và thang 4

   Khoảng nửa mở: MinScore <= Score < MaxScore.
   Hàng A có MaxScore = 10.01 để điểm 10.00 vẫn rơi vào khoảng.
   Ngưỡng đạt môn là 5.0 => D+ trở lên là đạt, D và F là trượt.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.GradeScale (MinScore, MaxScore, LetterGrade, GradePoint, Description) VALUES
    (8.50, 10.01, N'A',  4.00, N'Giỏi'),
    (8.00,  8.50, N'B+', 3.50, N'Khá giỏi'),
    (7.00,  8.00, N'B',  3.00, N'Khá'),
    (6.50,  7.00, N'C+', 2.50, N'Trung bình khá'),
    (5.50,  6.50, N'C',  2.00, N'Trung bình'),
    (5.00,  5.50, N'D+', 1.50, N'Trung bình yếu'),
    (4.00,  5.00, N'D',  1.00, N'Yếu - không đạt'),
    (0.00,  4.00, N'F',  0.00, N'Kém - không đạt');
GO

/* -----------------------------------------------------------------------------
   Major

   RequiredCredits = 107 = đúng tổng tín chỉ của khung CTĐT bên dưới.
   Cuối script có câu lệnh tự kiểm tra con số này; lệch là script báo lỗi
   ngay chứ không để tới lúc % tiến độ tốt nghiệp hiển thị sai.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Major (MajorCode, MajorName, RequiredCredits, TotalTerms) VALUES
    (N'SE', N'Kỹ thuật phần mềm (Software Engineering)', 107, 9);
GO

/* -----------------------------------------------------------------------------
   Semester - từ Spring 2024 đến Spring 2027

   DisplayOrder là thứ tự thời gian thật. KHÔNG sắp xếp theo SemesterCode:
   theo alphabet thì "FA25" < "SP26" nhưng thực tế FA25 diễn ra TRƯỚC SP26.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Semester (SemesterCode, SemesterName, StartDate, EndDate, DisplayOrder, IsCurrent) VALUES
    (N'SP24', N'Spring 2024', '2024-01-08', '2024-04-28',  1, 0),
    (N'SU24', N'Summer 2024', '2024-05-06', '2024-08-25',  2, 0),
    (N'FA24', N'Fall 2024',   '2024-09-02', '2024-12-22',  3, 0),
    (N'SP25', N'Spring 2025', '2025-01-06', '2025-04-27',  4, 0),
    (N'SU25', N'Summer 2025', '2025-05-05', '2025-08-24',  5, 0),
    (N'FA25', N'Fall 2025',   '2025-09-01', '2025-12-21',  6, 0),
    (N'SP26', N'Spring 2026', '2026-01-05', '2026-04-26',  7, 0),
    (N'SU26', N'Summer 2026', '2026-05-04', '2026-08-23',  8, 1),   -- kỳ hiện tại
    (N'FA26', N'Fall 2026',   '2026-08-31', '2026-12-20',  9, 0),
    (N'SP27', N'Spring 2027', '2027-01-04', '2027-04-25', 10, 0);
GO

/* -----------------------------------------------------------------------------
   Course - 31 môn theo khung Kỹ thuật phần mềm
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Course (CourseCode, CourseName, Credits, Description) VALUES
    -- Kỳ 1
    (N'CSI104',  N'Introduction to Computer Science',              3, N'Nhập môn khoa học máy tính'),
    (N'PRF192',  N'Programming Fundamentals (C)',                  3, N'Lập trình cơ bản với C'),
    (N'MAE101',  N'Mathematics for Engineering',                   3, N'Toán cho kỹ thuật'),
    (N'CEA201',  N'Computer Organization and Architecture',        3, N'Kiến trúc máy tính'),
    (N'SSL101c', N'Academic Skills for University Success',        3, N'Kỹ năng học đại học'),
    -- Kỳ 2
    (N'PRO192',  N'Object-Oriented Programming (Java)',            3, N'Lập trình hướng đối tượng với Java'),
    (N'MAD101',  N'Discrete Mathematics',                          3, N'Toán rời rạc'),
    (N'OSG202',  N'Operating Systems',                             3, N'Hệ điều hành'),
    (N'NWC204',  N'Computer Networking',                           3, N'Mạng máy tính'),
    (N'SSG104',  N'Communication and In-Group Working Skills',     3, N'Kỹ năng làm việc nhóm'),
    -- Kỳ 3
    (N'CSD201',  N'Data Structures and Algorithms',                3, N'Cấu trúc dữ liệu và giải thuật'),
    (N'DBI202',  N'Database Systems',                              3, N'Hệ quản trị cơ sở dữ liệu'),
    (N'LAB211',  N'OOP with Java Lab',                             3, N'Thực hành lập trình hướng đối tượng'),
    (N'WED201c', N'Web Design',                                    3, N'Thiết kế web'),
    (N'IOT102',  N'Internet of Things',                            3, N'Internet vạn vật'),
    -- Kỳ 4
    (N'PRJ301',  N'Java Web Application Development',              3, N'Lập trình web với Java'),
    (N'SWE201c', N'Introduction to Software Engineering',          3, N'Nhập môn công nghệ phần mềm'),
    (N'MAS291',  N'Statistics and Probability',                    3, N'Xác suất thống kê'),
    (N'ITE302c', N'Ethics in IT',                                  3, N'Đạo đức nghề nghiệp CNTT'),
    -- Kỳ 5
    (N'PRN212',  N'Basic Cross-Platform Application Programming',  3, N'Lập trình đa nền tảng cơ bản với .NET'),
    (N'SWP391',  N'Software Development Project',                  3, N'Đồ án phát triển phần mềm'),
    (N'SWT301',  N'Software Testing',                              3, N'Kiểm thử phần mềm'),
    -- Kỳ 6
    (N'PRN222',  N'Advanced Cross-Platform Application Programming',3, N'Lập trình đa nền tảng nâng cao'),
    (N'SWR302',  N'Software Requirement',                          3, N'Đặc tả yêu cầu phần mềm'),
    (N'SDN302',  N'Software Development with .NET',                3, N'Phát triển phần mềm với .NET'),
    -- Kỳ 7
    (N'PMG201c', N'Project Management',                            3, N'Quản trị dự án'),
    (N'EXE101',  N'Experiential Entrepreneurship 1',               3, N'Khởi nghiệp trải nghiệm 1'),
    (N'MLN111',  N'Philosophy of Marxism - Leninism',              3, N'Triết học Mác - Lênin'),
    -- Kỳ 8
    (N'OJT202',  N'On-the-Job Training',                          10, N'Thực tập tại doanh nghiệp'),
    -- Kỳ 9
    (N'SEP490',  N'Software Engineering Capstone Project',        10, N'Đồ án tốt nghiệp'),
    (N'EXE201',  N'Experiential Entrepreneurship 2',               3, N'Khởi nghiệp trải nghiệm 2');
GO

/* -----------------------------------------------------------------------------
   Assessment - đầu điểm của từng môn

   Dùng INSERT ... SELECT thay vì liệt kê tay 31 x 4 dòng: ngắn hơn, và quan
   trọng hơn là không thể gõ sai trọng số ở một môn lẻ nào đó.

   Tổng trọng số mỗi môn = 1.00. Final Exam có điểm sàn 4.0 theo quy chế FPT:
   dưới 4 điểm thi cuối kỳ là trượt môn dù điểm tổng kết vẫn >= 5.
   ----------------------------------------------------------------------------- */

-- Môn lý thuyết/thực hành thông thường (29 môn)
INSERT INTO dbo.Assessment (CourseId, Name, Weight, MinScoreToPass, DisplayOrder)
SELECT c.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder
FROM dbo.Course c
CROSS JOIN (VALUES
    (N'Assignment',    CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 1),
    (N'Progress Test', CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 2),
    (N'Practical Exam',CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 3),
    (N'Final Exam',    CAST(0.40 AS DECIMAL(5,4)), CAST(4.00 AS DECIMAL(4,2)), 4)
) AS a(Name, Weight, MinScoreToPass, DisplayOrder)
WHERE c.CourseCode NOT IN (N'OJT202', N'SEP490');
GO

-- Môn dự án / thực tập: chỉ có đánh giá của người hướng dẫn và buổi bảo vệ
INSERT INTO dbo.Assessment (CourseId, Name, Weight, MinScoreToPass, DisplayOrder)
SELECT c.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder
FROM dbo.Course c
CROSS JOIN (VALUES
    (N'Supervisor Evaluation', CAST(0.40 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 1),
    (N'Final Defense',         CAST(0.60 AS DECIMAL(5,4)), CAST(4.00 AS DECIMAL(4,2)), 2)
) AS a(Name, Weight, MinScoreToPass, DisplayOrder)
WHERE c.CourseCode IN (N'OJT202', N'SEP490');
GO

/* -----------------------------------------------------------------------------
   Prerequisite - môn tiên quyết

   Chuỗi PRF192 -> PRO192 -> PRN212 -> PRN222 dài 4 tầng, cố ý để có dữ liệu
   thật kiểm tra thuật toán duyệt tiên quyết đệ quy (docs/plan §9).
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Prerequisite (CourseId, RequiredCourseId, Type)
SELECT c.CourseId, r.CourseId, N'Prerequisite'
FROM (VALUES
    (N'PRO192',  N'PRF192'),
    (N'CSD201',  N'PRO192'),
    (N'LAB211',  N'PRO192'),
    (N'MAD101',  N'MAE101'),
    (N'MAS291',  N'MAE101'),
    (N'IOT102',  N'CEA201'),
    (N'PRJ301',  N'PRO192'),
    (N'PRJ301',  N'DBI202'),
    (N'PRN212',  N'PRO192'),
    (N'PRN222',  N'PRN212'),
    (N'SDN302',  N'PRN212'),
    (N'SWT301',  N'SWE201c'),
    (N'SWR302',  N'SWE201c'),
    (N'SWP391',  N'SWE201c'),
    (N'SWP391',  N'DBI202'),
    (N'OJT202',  N'SWP391'),
    (N'SEP490',  N'SWP391'),
    (N'SEP490',  N'OJT202'),
    (N'EXE201',  N'EXE101')
) AS p(CourseCode, RequiredCode)
JOIN dbo.Course c ON c.CourseCode = p.CourseCode
JOIN dbo.Course r ON r.CourseCode = p.RequiredCode;
GO

/* -----------------------------------------------------------------------------
   Curriculum - khung chương trình ngành SE
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Curriculum (MajorId, CourseId, TermNo, IsMandatory)
SELECT m.MajorId, c.CourseId, k.TermNo, 1
FROM (VALUES
    (N'CSI104', 1), (N'PRF192', 1), (N'MAE101', 1), (N'CEA201', 1), (N'SSL101c',1),
    (N'PRO192', 2), (N'MAD101', 2), (N'OSG202', 2), (N'NWC204', 2), (N'SSG104', 2),
    (N'CSD201', 3), (N'DBI202', 3), (N'LAB211', 3), (N'WED201c',3), (N'IOT102', 3),
    (N'PRJ301', 4), (N'SWE201c',4), (N'MAS291', 4), (N'ITE302c',4),
    (N'PRN212', 5), (N'SWP391', 5), (N'SWT301', 5),
    (N'PRN222', 6), (N'SWR302', 6), (N'SDN302', 6),
    (N'PMG201c',7), (N'EXE101', 7), (N'MLN111', 7),
    (N'OJT202', 8),
    (N'SEP490', 9), (N'EXE201', 9)
) AS k(CourseCode, TermNo)
JOIN dbo.Course c ON c.CourseCode = k.CourseCode
CROSS JOIN dbo.Major m
WHERE m.MajorCode = N'SE';
GO

COMMIT TRANSACTION;
GO

/* =============================================================================
   Tự kiểm tra - thà script đỏ ngay bây giờ còn hơn Dashboard hiển thị sai số
   ============================================================================= */

-- 1. Tổng trọng số đầu điểm của MỌI môn phải đúng bằng 1.00
IF EXISTS (
    SELECT 1 FROM dbo.Assessment
    GROUP BY CourseId
    HAVING ABS(SUM(Weight) - 1.0) > 0.0001
)
BEGIN
    THROW 50001, N'[02_seed] LOI: co mon hoc tong trong so Assessment khac 1.00.', 1;
END

-- 2. RequiredCredits của ngành phải khớp tổng tín chỉ trong khung CTĐT
IF EXISTS (
    SELECT 1
    FROM dbo.Major m
    JOIN (
        SELECT cu.MajorId, SUM(c.Credits) AS TotalCredits
        FROM dbo.Curriculum cu
        JOIN dbo.Course c ON c.CourseId = cu.CourseId
        GROUP BY cu.MajorId
    ) t ON t.MajorId = m.MajorId
    WHERE t.TotalCredits <> m.RequiredCredits
)
BEGIN
    THROW 50002, N'[02_seed] LOI: Major.RequiredCredits khong khop tong tin chi Curriculum.', 1;
END

-- 3. Bảng quy đổi điểm không được có kẽ hở hay vùng chồng lấn trong [0, 10]
IF EXISTS (
    SELECT 1
    FROM dbo.GradeScale g
    LEFT JOIN dbo.GradeScale n ON n.MinScore = g.MaxScore
    WHERE g.MaxScore <= 10.00 AND n.GradeScaleId IS NULL
)
BEGIN
    THROW 50003, N'[02_seed] LOI: GradeScale bi ho hoac chong lan khoang diem.', 1;
END

-- 4. Đúng một kỳ được đánh dấu là kỳ hiện tại
IF (SELECT COUNT(*) FROM dbo.Semester WHERE IsCurrent = 1) <> 1
BEGIN
    THROW 50004, N'[02_seed] LOI: phai co dung 1 hoc ky IsCurrent = 1.', 1;
END
GO

PRINT '[02_seed_master] OK - da nap danh muc va vuot qua 4 buoc tu kiem tra.';
GO
