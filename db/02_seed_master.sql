/* =============================================================================
   FAT - FPT Academic Tracker
   02_seed_master.sql : reference data (Role, GradeScale, Major, Semester,
                         Course, Assessment, Prerequisite, Curriculum).
   AUTO-GENERATED EXCLUSIVELY FROM db/data/BIT_SE_K19D_K20A.json
   ============================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE FAT_DB;
GO

BEGIN TRANSACTION;
GO

/* -----------------------------------------------------------------------------
   Role
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Role (RoleName, Description) VALUES
    (N'Admin',   N'Manages the course catalog, curricula and user accounts'),
    (N'Student', N'Views and maintains their own academic record');
GO

/* -----------------------------------------------------------------------------
   GradeScale
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.GradeScale (MinScore, MaxScore, LetterGrade, GradePoint, Description) VALUES
    (8.50, 10.01, N'A',  4.00, N'Very good'),
    (8.00,  8.50, N'B+', 3.50, N'Good plus'),
    (7.00,  8.00, N'B',  3.00, N'Good'),
    (6.50,  7.00, N'C+', 2.50, N'Fairly good'),
    (5.50,  6.50, N'C',  2.00, N'Average'),
    (5.00,  5.50, N'D+', 1.50, N'Below average'),
    (4.00,  5.00, N'D',  1.00, N'Weak - not a pass'),
    (0.00,  4.00, N'F',  0.00, N'Fail');
GO

/* -----------------------------------------------------------------------------
   Major
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Major (MajorCode, MajorName, RequiredCredits, TotalTerms) VALUES
    (N'SE', N'Software Engineering', 145, 9);
GO

/* -----------------------------------------------------------------------------
   Semester
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Semester (SemesterCode, SemesterName, StartDate, EndDate, DisplayOrder, IsCurrent) VALUES
    (N'SP24', N'Spring 2024', '2024-01-08', '2024-04-28',  1, 0),
    (N'SU24', N'Summer 2024', '2024-05-06', '2024-08-25',  2, 0),
    (N'FA24', N'Fall 2024',   '2024-09-02', '2024-12-22',  3, 0),
    (N'SP25', N'Spring 2025', '2025-01-06', '2025-04-27',  4, 0),
    (N'SU25', N'Summer 2025', '2025-05-05', '2025-08-24',  5, 0),
    (N'FA25', N'Fall 2025',   '2025-09-01', '2025-12-21',  6, 0),
    (N'SP26', N'Spring 2026', '2026-01-05', '2026-04-26',  7, 0),
    (N'SU26', N'Summer 2026', '2026-05-04', '2026-08-23',  8, 1),
    (N'FA26', N'Fall 2026',   '2026-08-31', '2026-12-20',  9, 0),
    (N'SP27', N'Spring 2027', '2027-01-04', '2027-04-25', 10, 0);
GO

/* -----------------------------------------------------------------------------
   Course - parsed from BIT_SE_K19D_K20A.json
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Course (CourseCode, CourseName, Credits, Description) VALUES
    (N'OTP101', N'Định hướng và Rèn luyện tập trung', 0, N'Định hướng và Rèn luyện tập trung'),
    (N'PEN', N'Tiếng Anh chuẩn bị', 0, N'Tiếng Anh chuẩn bị'),
    (N'PHE_COM*1', N'Giáo dục thể chất 1', 2, N'Giáo dục thể chất 1'),
    (N'TMI_ELE', N'Nhạc cụ truyền thống', 3, N'Nhạc cụ truyền thống'),
    (N'CEA201', N'Tổ chức và Kiến trúc máy tính', 3, N'Tổ chức và Kiến trúc máy tính'),
    (N'CSI106', N'Nhập môn khoa học máy tính', 3, N'Nhập môn khoa học máy tính'),
    (N'MAE101', N'Toán cho ngành kỹ thuật', 3, N'Toán cho ngành kỹ thuật'),
    (N'PHE_COM*2', N'Giáo dục thể chất 2', 2, N'Giáo dục thể chất 2'),
    (N'PRF192', N'Cơ sở lập trình', 3, N'Cơ sở lập trình'),
    (N'SSL101c', N'Kỹ năng học tập đại học', 3, N'Kỹ năng học tập đại học'),
    (N'MAD101', N'Toán rời rạc', 3, N'Toán rời rạc'),
    (N'NWC204', N'Mạng máy tính', 3, N'Mạng máy tính'),
    (N'OSG202', N'Hệ điều hành', 3, N'Hệ điều hành'),
    (N'PHE_COM*3', N'Giáo dục thể chất 3', 2, N'Giáo dục thể chất 3'),
    (N'PRO192', N'Lập trình hướng đối tượng', 3, N'Lập trình hướng đối tượng'),
    (N'WED201c', N'Thiết kế web', 3, N'Thiết kế web'),
    (N'CSD201', N'Cấu trúc dữ liệu và giải thuật', 3, N'Cấu trúc dữ liệu và giải thuật'),
    (N'DBI202', N'Các hệ cơ sở dữ liệu', 3, N'Các hệ cơ sở dữ liệu'),
    (N'JPD113', N'Tiếng Nhật sơ cấp 1-A1.1', 3, N'Tiếng Nhật sơ cấp 1-A1.1'),
    (N'LAB211', N'Thực hành OOP với Java', 3, N'Thực hành OOP với Java'),
    (N'MAS291', N'Xác suất thống kê', 3, N'Xác suất thống kê'),
    (N'IOT102', N'Internet vạn vật', 3, N'Internet vạn vật'),
    (N'JPD123', N'Tiếng Nhật sơ cấp 1-A1.2', 3, N'Tiếng Nhật sơ cấp 1-A1.2'),
    (N'PRJ301', N'Phát triển ứng dụng Java web', 3, N'Phát triển ứng dụng Java web'),
    (N'SSG104', N'Kỹ năng giao tiếp và cộng tác', 3, N'Kỹ năng giao tiếp và cộng tác'),
    (N'SWE202c', N'Nhập môn kĩ thuật phần mềm', 3, N'Nhập môn kĩ thuật phần mềm'),
    (N'SE_COM*1', N'Học phần 1 của combo*', 3, N'Học phần 1 của combo*'),
    (N'SWP391', N'Dự án phát triển phần mềm', 3, N'Dự án phát triển phần mềm'),
    (N'SWR302', N'Yêu cầu phần mềm', 3, N'Yêu cầu phần mềm'),
    (N'SWT301', N'Kiểm thử phần mềm', 3, N'Kiểm thử phần mềm'),
    (N'WDU203c', N'Thiết kế trải nghiệm người dùng', 3, N'Thiết kế trải nghiệm người dùng'),
    (N'ENW493c', N'Phương pháp nghiên cứu & Kỹ năng viết học thuật', 3, N'Phương pháp nghiên cứu & Kỹ năng viết học thuật'),
    (N'OJT202', N'Đào tạo trong môi trường thực tế', 10, N'Đào tạo trong môi trường thực tế'),
    (N'EXE101', N'Trải nghiệm khởi nghiệp 1', 3, N'Trải nghiệm khởi nghiệp 1'),
    (N'PMG201c', N'Quản lý dự án', 3, N'Quản lý dự án'),
    (N'SE_COM*2', N'Học phần 2 của combo*', 3, N'Học phần 2 của combo*'),
    (N'SE_COM*3', N'Học phần 3 của combo*', 3, N'Học phần 3 của combo*'),
    (N'SWD392', N'Kiến trúc và thiết kế phần mềm', 3, N'Kiến trúc và thiết kế phần mềm'),
    (N'EXE201', N'Trải nghiệm khởi nghiệp 2', 3, N'Trải nghiệm khởi nghiệp 2'),
    (N'ITE302c', N'Đạo đức trong CNTT', 3, N'Đạo đức trong CNTT'),
    (N'MLN111', N'Triết học Mác - Lê-nin', 3, N'Triết học Mác - Lê-nin'),
    (N'MLN122', N'Kinh tế chính trị Mác - Lê-nin', 2, N'Kinh tế chính trị Mác - Lê-nin'),
    (N'PRM393', N'Lập trình di động', 3, N'Lập trình di động'),
    (N'SE_COM*4_ELE', N'Học phần 4 của combo SE', 3, N'Học phần 4 của combo SE'),
    (N'HCM202', N'Tư tưởng Hồ Chí Minh', 2, N'Tư tưởng Hồ Chí Minh'),
    (N'MLN131', N'Chủ nghĩa xã hội khoa học', 2, N'Chủ nghĩa xã hội khoa học'),
    (N'SE_GRA_ELE', N'Học phần lựa chọn Đồ án tốt nghiệp chuyên ngành Kỹ thuật phần mềm', 10, N'Học phần lựa chọn Đồ án tốt nghiệp chuyên ngành Kỹ thuật phần mềm'),
    (N'VNR202', N'Lịch sử Đảng Cộng sản Việt Nam', 2, N'Lịch sử Đảng Cộng sản Việt Nam')
;
GO

/* -----------------------------------------------------------------------------
   Assessment - parsed from BIT_SE_K19D_K20A.json
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Assessment (CourseId, Name, Weight, MinScoreToPass, DisplayOrder)
SELECT CourseId, N'Final exam', 1.0, 5, 1 FROM dbo.Course WHERE CourseCode = N'PHE_COM*1'
UNION ALL
SELECT CourseId, N'Assignment', 0.15, 0, 1 FROM dbo.Course WHERE CourseCode = N'TMI_ELE'
UNION ALL
SELECT CourseId, N'Participation', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'TMI_ELE'
UNION ALL
SELECT CourseId, N'Final exam', 0.7, 4, 3 FROM dbo.Course WHERE CourseCode = N'TMI_ELE'
UNION ALL
SELECT CourseId, N'Assignment', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'CEA201'
UNION ALL
SELECT CourseId, N'Exercises', 0.4, 0, 2 FROM dbo.Course WHERE CourseCode = N'CEA201'
UNION ALL
SELECT CourseId, N'Final exam', 0.4, 4, 3 FROM dbo.Course WHERE CourseCode = N'CEA201'
UNION ALL
SELECT CourseId, N'Group presentation', 0.1, 0, 1 FROM dbo.Course WHERE CourseCode = N'CSI106'
UNION ALL
SELECT CourseId, N'Lab', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'CSI106'
UNION ALL
SELECT CourseId, N'Progress Test', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'CSI106'
UNION ALL
SELECT CourseId, N'Final exam', 0.4, 4, 4 FROM dbo.Course WHERE CourseCode = N'CSI106'
UNION ALL
SELECT CourseId, N'Assignments/Exercises', 0.3, 0, 1 FROM dbo.Course WHERE CourseCode = N'MAE101'
UNION ALL
SELECT CourseId, N'Progress Test', 0.3, 0, 2 FROM dbo.Course WHERE CourseCode = N'MAE101'
UNION ALL
SELECT CourseId, N'Final Exam', 0.4, 4, 3 FROM dbo.Course WHERE CourseCode = N'MAE101'
UNION ALL
SELECT CourseId, N'Final exam', 1.0, 5, 1 FROM dbo.Course WHERE CourseCode = N'PHE_COM*2'
UNION ALL
SELECT CourseId, N'Assignment', 0.15, 0, 1 FROM dbo.Course WHERE CourseCode = N'PRF192'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.3, 0, 2 FROM dbo.Course WHERE CourseCode = N'PRF192'
UNION ALL
SELECT CourseId, N'Progress test', 0.15, 0, 3 FROM dbo.Course WHERE CourseCode = N'PRF192'
UNION ALL
SELECT CourseId, N'Workshop', 0.1, 0, 4 FROM dbo.Course WHERE CourseCode = N'PRF192'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 5 FROM dbo.Course WHERE CourseCode = N'PRF192'
UNION ALL
SELECT CourseId, N'Theoretical Exam (TE)', 1.0, 4, 1 FROM dbo.Course WHERE CourseCode = N'SSL101c'
UNION ALL
SELECT CourseId, N'Progress Test', 0.3, 0, 1 FROM dbo.Course WHERE CourseCode = N'MAD101'
UNION ALL
SELECT CourseId, N'Assignments/Exercises', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'MAD101'
UNION ALL
SELECT CourseId, N'Programming Assignment', 0.1, 0, 3 FROM dbo.Course WHERE CourseCode = N'MAD101'
UNION ALL
SELECT CourseId, N'Final Exam', 0.4, 4, 4 FROM dbo.Course WHERE CourseCode = N'MAD101'
UNION ALL
SELECT CourseId, N'Lab', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'NWC204'
UNION ALL
SELECT CourseId, N'Progress Test', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'NWC204'
UNION ALL
SELECT CourseId, N'Project', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'NWC204'
UNION ALL
SELECT CourseId, N'Final Exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'NWC204'
UNION ALL
SELECT CourseId, N'Lab', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'OSG202'
UNION ALL
SELECT CourseId, N'Presentation', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'OSG202'
UNION ALL
SELECT CourseId, N'Progress test', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'OSG202'
UNION ALL
SELECT CourseId, N'Final exam', 0.4, 4, 4 FROM dbo.Course WHERE CourseCode = N'OSG202'
UNION ALL
SELECT CourseId, N'Final exam', 1.0, 5, 1 FROM dbo.Course WHERE CourseCode = N'PHE_COM*3'
UNION ALL
SELECT CourseId, N'Assignment', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'PRO192'
UNION ALL
SELECT CourseId, N'Lab', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'PRO192'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'PRO192'
UNION ALL
SELECT CourseId, N'Progress Test', 0.1, 0, 4 FROM dbo.Course WHERE CourseCode = N'PRO192'
UNION ALL
SELECT CourseId, N'Final Exam', 0.3, 4, 5 FROM dbo.Course WHERE CourseCode = N'PRO192'
UNION ALL
SELECT CourseId, N'PE (Practical Exam)', 0.5, 4, 1 FROM dbo.Course WHERE CourseCode = N'WED201c'
UNION ALL
SELECT CourseId, N'TE (Theoretical Exam)', 0.5, 4, 2 FROM dbo.Course WHERE CourseCode = N'WED201c'
UNION ALL
SELECT CourseId, N'Progress test (PT)', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'CSD201'
UNION ALL
SELECT CourseId, N'Assignment (AS)', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'CSD201'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'CSD201'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'CSD201'
UNION ALL
SELECT CourseId, N'Assignment', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'DBI202'
UNION ALL
SELECT CourseId, N'Lab', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'DBI202'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'DBI202'
UNION ALL
SELECT CourseId, N'Progress test', 0.1, 0, 4 FROM dbo.Course WHERE CourseCode = N'DBI202'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 5 FROM dbo.Course WHERE CourseCode = N'DBI202'
UNION ALL
SELECT CourseId, N'Small test (Kiểm tra nhỏ)', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'JPD113'
UNION ALL
SELECT CourseId, N'Class Participation (Tham gia giờ học)', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'JPD113'
UNION ALL
SELECT CourseId, N'Final Exam - Written (Lý thuyết)', 0.15, 0, 3 FROM dbo.Course WHERE CourseCode = N'JPD113'
UNION ALL
SELECT CourseId, N'Final Exam - Speaking (Nói)', 0.3, 0, 4 FROM dbo.Course WHERE CourseCode = N'JPD113'
UNION ALL
SELECT CourseId, N'Course Completion', 1.0, NULL, 1 FROM dbo.Course WHERE CourseCode = N'LAB211'
UNION ALL
SELECT CourseId, N'Assignment', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'MAS291'
UNION ALL
SELECT CourseId, N'Computer Project', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'MAS291'
UNION ALL
SELECT CourseId, N'Progress Test', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'MAS291'
UNION ALL
SELECT CourseId, N'Final exam', 0.35, 4, 4 FROM dbo.Course WHERE CourseCode = N'MAS291'
UNION ALL
SELECT CourseId, N'Active learning', 0.1, 0, 1 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'Final Project Presentation', 0.2, 4, 2 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'On-Going Project Assessment', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'Presentation', 0.1, 0, 4 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'Progress test (Practice/Exercises/Quiz)', 0.1, 0, 5 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'Final exam', 0.2, 4, 6 FROM dbo.Course WHERE CourseCode = N'IOT102'
UNION ALL
SELECT CourseId, N'Small test (Kiểm tra nhỏ)', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'JPD123'
UNION ALL
SELECT CourseId, N'Class Participation (Tham gia giờ học)', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'JPD123'
UNION ALL
SELECT CourseId, N'Final Exam - Written (Lý thuyết)', 0.15, 0, 3 FROM dbo.Course WHERE CourseCode = N'JPD123'
UNION ALL
SELECT CourseId, N'Final Exam - Speaking (Nói)', 0.3, 0, 4 FROM dbo.Course WHERE CourseCode = N'JPD123'
UNION ALL
SELECT CourseId, N'Assignment', 0.3, 0, 1 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.3, 0, 2 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Progress Test 1', 0.05, 0, 3 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Progress Test 2', 0.05, 0, 4 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Workshop 1', 0.05, 0, 5 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Workshop 2', 0.05, 0, 6 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Final Exam', 0.2, 4, 7 FROM dbo.Course WHERE CourseCode = N'PRJ301'
UNION ALL
SELECT CourseId, N'Activity', 0.15, 0, 1 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'Group assignment (Group asm)', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'Group Project', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 4 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'Quiz', 0.05, 0, 5 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'Final exam', 0.2, 4, 6 FROM dbo.Course WHERE CourseCode = N'SSG104'
UNION ALL
SELECT CourseId, N'PE (Practical Exam)', 0.5, 4, 1 FROM dbo.Course WHERE CourseCode = N'SWE202c'
UNION ALL
SELECT CourseId, N'TE (Theoretical Exam)', 0.5, 4, 2 FROM dbo.Course WHERE CourseCode = N'SWE202c'
UNION ALL
SELECT CourseId, N'Assessment 1 (Week 3)', 0.15, 0, 1 FROM dbo.Course WHERE CourseCode = N'SWP391'
UNION ALL
SELECT CourseId, N'Assessment 2 (Week 8)', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'SWP391'
UNION ALL
SELECT CourseId, N'Assessment 3 (Week 10)', 0.25, 0, 3 FROM dbo.Course WHERE CourseCode = N'SWP391'
UNION ALL
SELECT CourseId, N'Final Project Presentation', 0.4, 4, 4 FROM dbo.Course WHERE CourseCode = N'SWP391'
UNION ALL
SELECT CourseId, N'Assignment', 0.2, 0, 1 FROM dbo.Course WHERE CourseCode = N'SWR302'
UNION ALL
SELECT CourseId, N'LAB', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'SWR302'
UNION ALL
SELECT CourseId, N'Progress Test', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'SWR302'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.25, 4, 4 FROM dbo.Course WHERE CourseCode = N'SWR302'
UNION ALL
SELECT CourseId, N'Theory Exam', 0.25, 4, 5 FROM dbo.Course WHERE CourseCode = N'SWR302'
UNION ALL
SELECT CourseId, N'Lab', 0.25, 0, 1 FROM dbo.Course WHERE CourseCode = N'SWT301'
UNION ALL
SELECT CourseId, N'Presentation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'SWT301'
UNION ALL
SELECT CourseId, N'Progress Test', 0.15, 0, 3 FROM dbo.Course WHERE CourseCode = N'SWT301'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.25, 4, 4 FROM dbo.Course WHERE CourseCode = N'SWT301'
UNION ALL
SELECT CourseId, N'Theory Exam', 0.25, 4, 5 FROM dbo.Course WHERE CourseCode = N'SWT301'
UNION ALL
SELECT CourseId, N'TE (Theoretical Exam)', 1.0, 4, 1 FROM dbo.Course WHERE CourseCode = N'WDU203c'
UNION ALL
SELECT CourseId, N'Final Exam', 1.0, 4, 1 FROM dbo.Course WHERE CourseCode = N'ENW493c'
UNION ALL
SELECT CourseId, N'Professional knowledge and skills', 0.4, 4, 1 FROM dbo.Course WHERE CourseCode = N'OJT202'
UNION ALL
SELECT CourseId, N'Soft skills', 0.3, 4, 2 FROM dbo.Course WHERE CourseCode = N'OJT202'
UNION ALL
SELECT CourseId, N'Attitude', 0.3, 4, 3 FROM dbo.Course WHERE CourseCode = N'OJT202'
UNION ALL
SELECT CourseId, N'Constructivism Presentations', 0.15, 5, 1 FROM dbo.Course WHERE CourseCode = N'EXE101'
UNION ALL
SELECT CourseId, N'Group Assignment 1 (Checkpoint 1)', 0.1, 5, 2 FROM dbo.Course WHERE CourseCode = N'EXE101'
UNION ALL
SELECT CourseId, N'Group Assignment 2 (Checkpoint 2)', 0.2, 5, 3 FROM dbo.Course WHERE CourseCode = N'EXE101'
UNION ALL
SELECT CourseId, N'Group Assignment 3 (Checkpoint 3)', 0.15, 5, 4 FROM dbo.Course WHERE CourseCode = N'EXE101'
UNION ALL
SELECT CourseId, N'Presentation (Checkpoint 4)', 0.4, 5, 5 FROM dbo.Course WHERE CourseCode = N'EXE101'
UNION ALL
SELECT CourseId, N'PE (Practical Exam)', 0.5, 4, 1 FROM dbo.Course WHERE CourseCode = N'PMG201c'
UNION ALL
SELECT CourseId, N'TE (Theoretical Exam)', 0.5, 4, 2 FROM dbo.Course WHERE CourseCode = N'PMG201c'
UNION ALL
SELECT CourseId, N'Course Project', 0.25, 5, 1 FROM dbo.Course WHERE CourseCode = N'SWD392'
UNION ALL
SELECT CourseId, N'Progress test', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'SWD392'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.2, 4, 3 FROM dbo.Course WHERE CourseCode = N'SWD392'
UNION ALL
SELECT CourseId, N'Theory Exam', 0.4, 4, 4 FROM dbo.Course WHERE CourseCode = N'SWD392'
UNION ALL
SELECT CourseId, N'Outcome 1 (Product/Service)', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'EXE201'
UNION ALL
SELECT CourseId, N'Outcome 2 (Presentation)', 0.2, 0, 2 FROM dbo.Course WHERE CourseCode = N'EXE201'
UNION ALL
SELECT CourseId, N'Outcome 3 (Sales Results)', 0.4, 4, 3 FROM dbo.Course WHERE CourseCode = N'EXE201'
UNION ALL
SELECT CourseId, N'TE (Theoretical Exam)', 1.0, 4, 1 FROM dbo.Course WHERE CourseCode = N'ITE302c'
UNION ALL
SELECT CourseId, N'Assignment', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'MLN111'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'MLN111'
UNION ALL
SELECT CourseId, N'Progress tests', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'MLN111'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'MLN111'
UNION ALL
SELECT CourseId, N'Assignment', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'MLN122'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'MLN122'
UNION ALL
SELECT CourseId, N'Progress tests', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'MLN122'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'MLN122'
UNION ALL
SELECT CourseId, N'Practical Exam', 0.25, 4, 1 FROM dbo.Course WHERE CourseCode = N'PRM393'
UNION ALL
SELECT CourseId, N'Progress Test', 0.15, 0, 2 FROM dbo.Course WHERE CourseCode = N'PRM393'
UNION ALL
SELECT CourseId, N'Project', 0.3, 0, 3 FROM dbo.Course WHERE CourseCode = N'PRM393'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'PRM393'
UNION ALL
SELECT CourseId, N'Assignment', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'HCM202'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'HCM202'
UNION ALL
SELECT CourseId, N'Progress test', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'HCM202'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'HCM202'
UNION ALL
SELECT CourseId, N'Assignment', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'MLN131'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'MLN131'
UNION ALL
SELECT CourseId, N'Progress tests', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'MLN131'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'MLN131'
UNION ALL
SELECT CourseId, N'Assignment', 0.4, 0, 1 FROM dbo.Course WHERE CourseCode = N'VNR202'
UNION ALL
SELECT CourseId, N'Participation', 0.1, 0, 2 FROM dbo.Course WHERE CourseCode = N'VNR202'
UNION ALL
SELECT CourseId, N'Progress test', 0.2, 0, 3 FROM dbo.Course WHERE CourseCode = N'VNR202'
UNION ALL
SELECT CourseId, N'Final exam', 0.3, 4, 4 FROM dbo.Course WHERE CourseCode = N'VNR202'
;
GO

/* -----------------------------------------------------------------------------
   Prerequisite - parsed from BIT_SE_K19D_K20A.json
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Prerequisite (CourseId, RequiredCourseId, Type)
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'PRO192' AND r.CourseCode = N'Pass'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'PRO192' AND r.CourseCode = N'PRF192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'CSD201' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'JPD113' AND r.CourseCode = N'Không'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'LAB211' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'MAS291' AND r.CourseCode = N'MAE101'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'MAS291' AND r.CourseCode = N'MAC101'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'JPD123' AND r.CourseCode = N'JPD113'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'PRJ301' AND r.CourseCode = N'DBI202'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'PRJ301' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWE202c' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWP391' AND r.CourseCode = N'PRJ301'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWP391' AND r.CourseCode = N'SWE201c'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWP391' AND r.CourseCode = N'pass'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWP391' AND r.CourseCode = N'LAB211'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWR302' AND r.CourseCode = N'SWE102'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWR302' AND r.CourseCode = N'SWE201c'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWT301' AND r.CourseCode = N'SWE102'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWT301' AND r.CourseCode = N'SWE201c'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Students'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'attained'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'90%'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'the'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'total'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'credits'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'prior'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'the'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'OJT'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'term'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'(excluding'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Physical'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Education'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'and'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'OTP'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Programs)'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Students'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'choosing'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'combo'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'(Japanese'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Bridge'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'Engineer)'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'have'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'pass'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'OJT202' AND r.CourseCode = N'JPD133'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWD392' AND r.CourseCode = N'SWE201c'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'SWD392' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'EXE201' AND r.CourseCode = N'EXE101'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'PRM393' AND r.CourseCode = N'PRO192'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'HCM202' AND r.CourseCode = N'MLN111'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'HCM202' AND r.CourseCode = N'MLN122'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'MLN131' AND r.CourseCode = N'MLN111'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'MLN131' AND r.CourseCode = N'MLN122'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'VNR202' AND r.CourseCode = N'MLN111'
UNION ALL
SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'VNR202' AND r.CourseCode = N'MLN122'
;
GO

/* -----------------------------------------------------------------------------
   Curriculum - parsed from BIT_SE_K19D_K20A.json
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Curriculum (MajorId, CourseId, TermNo, IsMandatory)
SELECT m.MajorId, c.CourseId, k.TermNo, 1
FROM (VALUES
    (N'OTP101', 0),
    (N'PEN', 0),
    (N'PHE_COM*1', 0),
    (N'TMI_ELE', 0),
    (N'CEA201', 1),
    (N'CSI106', 1),
    (N'MAE101', 1),
    (N'PHE_COM*2', 1),
    (N'PRF192', 1),
    (N'SSL101c', 1),
    (N'MAD101', 2),
    (N'NWC204', 2),
    (N'OSG202', 2),
    (N'PHE_COM*3', 2),
    (N'PRO192', 2),
    (N'WED201c', 2),
    (N'CSD201', 3),
    (N'DBI202', 3),
    (N'JPD113', 3),
    (N'LAB211', 3),
    (N'MAS291', 3),
    (N'IOT102', 4),
    (N'JPD123', 4),
    (N'PRJ301', 4),
    (N'SSG104', 4),
    (N'SWE202c', 4),
    (N'SE_COM*1', 5),
    (N'SWP391', 5),
    (N'SWR302', 5),
    (N'SWT301', 5),
    (N'WDU203c', 5),
    (N'ENW493c', 6),
    (N'OJT202', 6),
    (N'EXE101', 7),
    (N'PMG201c', 7),
    (N'SE_COM*2', 7),
    (N'SE_COM*3', 7),
    (N'SWD392', 7),
    (N'EXE201', 8),
    (N'ITE302c', 8),
    (N'MLN111', 8),
    (N'MLN122', 8),
    (N'PRM393', 8),
    (N'SE_COM*4_ELE', 8),
    (N'HCM202', 9),
    (N'MLN131', 9),
    (N'SE_GRA_ELE', 9),
    (N'VNR202', 9)
) AS k(CourseCode, TermNo)
JOIN dbo.Course c ON c.CourseCode = k.CourseCode
CROSS JOIN dbo.Major m
WHERE m.MajorCode = N'SE';
GO

COMMIT TRANSACTION;
GO

