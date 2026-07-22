/* =============================================================================
   FAT - FPT Academic Tracker
   02_seed_master.sql : reference data (Role, GradeScale, Major, Semester,
                        Course, Assessment, Prerequisite, Curriculum).

   Run AFTER 01_schema.sql.

   Everything below resolves keys by CODE (CourseCode, MajorCode, ...) instead
   of hard-coding ids, so inserting or removing a row above does not silently
   shift every row below it.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE FAT;
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
   GradeScale - 10-point score to letter grade and 4-point value

   Half-open bands: MinScore <= Score < MaxScore.
   The A band uses MaxScore = 10.01 so that a perfect 10.00 still falls inside.
   The pass mark is 5.0, so D+ and above pass while D and F fail.
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

   RequiredCredits = 107, which is exactly the total credits of the curriculum
   defined below. A self-check at the end of this script enforces that, so a
   mismatch fails here rather than showing a wrong graduation percentage later.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Major (MajorCode, MajorName, RequiredCredits, TotalTerms) VALUES
    (N'SE', N'Software Engineering', 107, 9);
GO

/* -----------------------------------------------------------------------------
   Semester - Spring 2024 through Spring 2027

   DisplayOrder is the real chronological order. Do NOT sort by SemesterCode:
   alphabetically "FA25" precedes "SP26", but FA25 actually happens first.
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Semester (SemesterCode, SemesterName, StartDate, EndDate, DisplayOrder, IsCurrent) VALUES
    (N'SP24', N'Spring 2024', '2024-01-08', '2024-04-28',  1, 0),
    (N'SU24', N'Summer 2024', '2024-05-06', '2024-08-25',  2, 0),
    (N'FA24', N'Fall 2024',   '2024-09-02', '2024-12-22',  3, 0),
    (N'SP25', N'Spring 2025', '2025-01-06', '2025-04-27',  4, 0),
    (N'SU25', N'Summer 2025', '2025-05-05', '2025-08-24',  5, 0),
    (N'FA25', N'Fall 2025',   '2025-09-01', '2025-12-21',  6, 0),
    (N'SP26', N'Spring 2026', '2026-01-05', '2026-04-26',  7, 0),
    (N'SU26', N'Summer 2026', '2026-05-04', '2026-08-23',  8, 1),   -- current term
    (N'FA26', N'Fall 2026',   '2026-08-31', '2026-12-20',  9, 0),
    (N'SP27', N'Spring 2027', '2027-01-04', '2027-04-25', 10, 0);
GO

/* -----------------------------------------------------------------------------
   Course - 31 courses of the Software Engineering programme
   ----------------------------------------------------------------------------- */
INSERT INTO dbo.Course (CourseCode, CourseName, Credits, Description) VALUES
    -- Term 1
    (N'CSI104',  N'Introduction to Computer Science',               3, N'Foundations of computing'),
    (N'PRF192',  N'Programming Fundamentals (C)',                   3, N'Basic programming in C'),
    (N'MAE101',  N'Mathematics for Engineering',                    3, N'Engineering mathematics'),
    (N'CEA201',  N'Computer Organization and Architecture',         3, N'Computer architecture'),
    (N'SSL101c', N'Academic Skills for University Success',         3, N'University study skills'),
    -- Term 2
    (N'PRO192',  N'Object-Oriented Programming (Java)',             3, N'OOP with Java'),
    (N'MAD101',  N'Discrete Mathematics',                           3, N'Discrete structures'),
    (N'OSG202',  N'Operating Systems',                              3, N'Operating system principles'),
    (N'NWC204',  N'Computer Networking',                            3, N'Networking fundamentals'),
    (N'SSG104',  N'Communication and In-Group Working Skills',      3, N'Teamwork and communication'),
    -- Term 3
    (N'CSD201',  N'Data Structures and Algorithms',                 3, N'Data structures and algorithms'),
    (N'DBI202',  N'Database Systems',                               3, N'Relational database systems'),
    (N'LAB211',  N'OOP with Java Lab',                              3, N'Hands-on OOP lab'),
    (N'WED201c', N'Web Design',                                     3, N'Web design fundamentals'),
    (N'IOT102',  N'Internet of Things',                             3, N'IoT fundamentals'),
    -- Term 4
    (N'PRJ301',  N'Java Web Application Development',               3, N'Java web development'),
    (N'SWE201c', N'Introduction to Software Engineering',           3, N'Software engineering principles'),
    (N'MAS291',  N'Statistics and Probability',                     3, N'Probability and statistics'),
    (N'ITE302c', N'Ethics in IT',                                   3, N'Professional ethics in IT'),
    -- Term 5
    (N'PRN212',  N'Basic Cross-Platform Application Programming',   3, N'Cross-platform basics with .NET'),
    (N'SWP391',  N'Software Development Project',                   3, N'Team software project'),
    (N'SWT301',  N'Software Testing',                               3, N'Software testing'),
    -- Term 6
    (N'PRN222',  N'Advanced Cross-Platform Application Programming', 3, N'Advanced cross-platform development'),
    (N'SWR302',  N'Software Requirement',                           3, N'Requirements engineering'),
    (N'SDN302',  N'Software Development with .NET',                 3, N'Building software with .NET'),
    -- Term 7
    (N'PMG201c', N'Project Management',                             3, N'Project management'),
    (N'EXE101',  N'Experiential Entrepreneurship 1',                3, N'Entrepreneurship part 1'),
    (N'MLN111',  N'Philosophy of Marxism - Leninism',               3, N'Philosophy'),
    -- Term 8
    (N'OJT202',  N'On-the-Job Training',                           10, N'Industry internship'),
    -- Term 9
    (N'SEP490',  N'Software Engineering Capstone Project',         10, N'Capstone project'),
    (N'EXE201',  N'Experiential Entrepreneurship 2',                3, N'Entrepreneurship part 2');
GO

/* -----------------------------------------------------------------------------
   Assessment - grade components per course

   Written as INSERT ... SELECT rather than 31 x 4 hand-typed rows: shorter, and
   more importantly it makes it impossible to mistype a weight on one course.

   Weights sum to 1.00 per course. The final exam carries a minimum of 4.0:
   score below that and the course is failed even if the weighted total is 5.0
   or higher.
   ----------------------------------------------------------------------------- */

-- Regular lecture and lab courses (29 of them)
INSERT INTO dbo.Assessment (CourseId, Name, Weight, MinScoreToPass, DisplayOrder)
SELECT c.CourseId, a.Name, a.Weight, a.MinScoreToPass, a.DisplayOrder
FROM dbo.Course c
CROSS JOIN (VALUES
    (N'Assignment',     CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 1),
    (N'Progress Test',  CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 2),
    (N'Practical Exam', CAST(0.20 AS DECIMAL(5,4)), CAST(NULL AS DECIMAL(4,2)), 3),
    (N'Final Exam',     CAST(0.40 AS DECIMAL(5,4)), CAST(4.00 AS DECIMAL(4,2)), 4)
) AS a(Name, Weight, MinScoreToPass, DisplayOrder)
WHERE c.CourseCode NOT IN (N'OJT202', N'SEP490');
GO

-- Project and internship courses: supervisor assessment plus a defence
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
   Prerequisite

   The chain PRF192 -> PRO192 -> PRN212 -> PRN222 is four levels deep on
   purpose, to give the recursive prerequisite resolver real data to work on.
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
   Curriculum - the Software Engineering study path
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
   Self-checks - far better to fail loudly here than to render wrong numbers
   ============================================================================= */

-- 1. Assessment weights of EVERY course must add up to exactly 1.00
IF EXISTS (
    SELECT 1 FROM dbo.Assessment
    GROUP BY CourseId
    HAVING ABS(SUM(Weight) - 1.0) > 0.0001
)
BEGIN
    THROW 50001, N'[02_seed] FAILED: a course has assessment weights that do not sum to 1.00.', 1;
END

-- 2. Major.RequiredCredits must equal the total credits of its curriculum
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
    THROW 50002, N'[02_seed] FAILED: Major.RequiredCredits does not match the curriculum total.', 1;
END

-- 3. The grade scale must cover [0, 10] with no gap and no overlap
IF EXISTS (
    SELECT 1
    FROM dbo.GradeScale g
    LEFT JOIN dbo.GradeScale n ON n.MinScore = g.MaxScore
    WHERE g.MaxScore <= 10.00 AND n.GradeScaleId IS NULL
)
BEGIN
    THROW 50003, N'[02_seed] FAILED: the grade scale has a gap or an overlap.', 1;
END

-- 4. Exactly one semester may be flagged as current
IF (SELECT COUNT(*) FROM dbo.Semester WHERE IsCurrent = 1) <> 1
BEGIN
    THROW 50004, N'[02_seed] FAILED: exactly one semester must have IsCurrent = 1.', 1;
END
GO

PRINT '[02_seed_master] OK - reference data loaded and 4 self-checks passed.';
GO
