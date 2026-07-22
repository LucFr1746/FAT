/* =============================================================================
   SAT - Student Academic Tracker
   01_schema.sql : Tạo database + toàn bộ bảng.

   NGUỒN SỰ THẬT của schema là file này (dự án KHÔNG dùng EF Core Migrations).
   Entity C# trong SAT.Domain/Entities phải khớp 1-1 với các bảng dưới đây.
   Đổi cột ở đây => phải đổi entity + báo cả nhóm chạy lại script.

   Script này XÓA VÀ TẠO LẠI database SAT => chạy bao nhiêu lần cũng ra kết quả
   giống hệt nhau (idempotent). Mọi dữ liệu đang có trong SAT sẽ mất.

   Chạy: .\db\setup-db.ps1     (khuyến nghị)
   hoặc: mở file này trong SSMS -> F5, rồi chạy tiếp 02_ và 03_.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE master;
GO

/* Đá hết connection đang mở rồi mới drop, nếu không DROP sẽ treo khi
   ai đó còn mở app hoặc còn tab query trỏ vào SAT. */
IF DB_ID(N'SAT') IS NOT NULL
BEGIN
    ALTER DATABASE SAT SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE SAT;
END
GO

CREATE DATABASE SAT;
GO

ALTER DATABASE SAT SET RECOVERY SIMPLE;
GO

USE SAT;
GO

/* =============================================================================
   1. Role - vai trò đăng nhập
   ============================================================================= */
CREATE TABLE dbo.Role
(
    RoleId      INT             IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(50)    NOT NULL,
    Description NVARCHAR(200)   NULL,
    CONSTRAINT PK_Role         PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Role_Name    UNIQUE (RoleName)
);
GO

/* =============================================================================
   2. AppUser - tài khoản đăng nhập

   Tên bảng là AppUser chứ KHÔNG phải User: "User" là từ khóa dành riêng của
   T-SQL, dùng nó thì mọi câu lệnh đều phải viết [User] và rất dễ quên.
   ============================================================================= */
CREATE TABLE dbo.AppUser
(
    UserId       INT            IDENTITY(1,1) NOT NULL,
    Username     NVARCHAR(50)   NOT NULL,
    -- Hash BCrypt (60 ký tự). KHÔNG BAO GIỜ lưu mật khẩu dạng thô.
    PasswordHash NVARCHAR(255)  NOT NULL,
    RoleId       INT            NOT NULL,
    IsActive     BIT            NOT NULL CONSTRAINT DF_AppUser_IsActive  DEFAULT (1),
    LastLoginAt  DATETIME2(0)   NULL,
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AppUser          PRIMARY KEY (UserId),
    CONSTRAINT UQ_AppUser_Username UNIQUE (Username),
    CONSTRAINT FK_AppUser_Role     FOREIGN KEY (RoleId) REFERENCES dbo.Role (RoleId)
);
GO

/* =============================================================================
   3. Major - ngành đào tạo
   ============================================================================= */
CREATE TABLE dbo.Major
(
    MajorId         INT           IDENTITY(1,1) NOT NULL,
    MajorCode       NVARCHAR(20)  NOT NULL,
    MajorName       NVARCHAR(150) NOT NULL,
    -- Tổng tín chỉ cần để tốt nghiệp. PHẢI khớp tổng tín chỉ trong Curriculum,
    -- nếu lệch thì % tiến độ tốt nghiệp sẽ sai. 03_seed kiểm tra điều này.
    RequiredCredits INT           NOT NULL,
    TotalTerms      INT           NOT NULL,
    IsActive        BIT           NOT NULL CONSTRAINT DF_Major_IsActive DEFAULT (1),
    CONSTRAINT PK_Major        PRIMARY KEY (MajorId),
    CONSTRAINT UQ_Major_Code   UNIQUE (MajorCode),
    CONSTRAINT CK_Major_Credit CHECK (RequiredCredits > 0 AND TotalTerms > 0)
);
GO

/* =============================================================================
   4. Student - hồ sơ sinh viên
   ============================================================================= */
CREATE TABLE dbo.Student
(
    StudentId      INT           IDENTITY(1,1) NOT NULL,
    UserId         INT           NOT NULL,
    StudentCode    NVARCHAR(20)  NOT NULL,
    FullName       NVARCHAR(150) NOT NULL,
    Email          NVARCHAR(150) NULL,
    DateOfBirth    DATE          NULL,
    EnrollmentDate DATE          NOT NULL,
    MajorId        INT           NOT NULL,
    -- Active | Suspended | Graduated | DroppedOut
    Status         NVARCHAR(20)  NOT NULL CONSTRAINT DF_Student_Status DEFAULT (N'Active'),
    CONSTRAINT PK_Student        PRIMARY KEY (StudentId),
    CONSTRAINT UQ_Student_Code   UNIQUE (StudentCode),
    -- 1 tài khoản chỉ gắn đúng 1 hồ sơ sinh viên
    CONSTRAINT UQ_Student_UserId UNIQUE (UserId),
    CONSTRAINT FK_Student_User   FOREIGN KEY (UserId)  REFERENCES dbo.AppUser (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Student_Major  FOREIGN KEY (MajorId) REFERENCES dbo.Major (MajorId)
);
GO

/* =============================================================================
   5. Course - môn học
   ============================================================================= */
CREATE TABLE dbo.Course
(
    CourseId    INT           IDENTITY(1,1) NOT NULL,
    CourseCode  NVARCHAR(20)  NOT NULL,
    CourseName  NVARCHAR(200) NOT NULL,
    Credits     INT           NOT NULL,
    Description NVARCHAR(500) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Course_IsActive DEFAULT (1),
    CONSTRAINT PK_Course         PRIMARY KEY (CourseId),
    CONSTRAINT UQ_Course_Code    UNIQUE (CourseCode),
    CONSTRAINT CK_Course_Credits CHECK (Credits >= 0 AND Credits <= 20)
);
GO

/* =============================================================================
   6. Prerequisite - môn tiên quyết

   CourseId cần học xong RequiredCourseId trước.
   Cả 2 FK đều trỏ về Course nên BẮT BUỘC để NO ACTION: SQL Server cấm hai
   đường cascade cùng đi tới một bảng (lỗi "multiple cascade paths").
   ============================================================================= */
CREATE TABLE dbo.Prerequisite
(
    PrerequisiteId   INT          IDENTITY(1,1) NOT NULL,
    CourseId         INT          NOT NULL,
    RequiredCourseId INT          NOT NULL,
    -- Prerequisite (học trước) | Corequisite (học cùng kỳ)
    Type             NVARCHAR(20) NOT NULL CONSTRAINT DF_Prereq_Type DEFAULT (N'Prerequisite'),
    CONSTRAINT PK_Prerequisite       PRIMARY KEY (PrerequisiteId),
    CONSTRAINT UQ_Prerequisite_Pair  UNIQUE (CourseId, RequiredCourseId),
    -- Chặn môn tự làm tiên quyết của chính nó (chu trình độ dài 1)
    CONSTRAINT CK_Prerequisite_Self  CHECK (CourseId <> RequiredCourseId),
    CONSTRAINT FK_Prereq_Course      FOREIGN KEY (CourseId)         REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION,
    CONSTRAINT FK_Prereq_Required    FOREIGN KEY (RequiredCourseId) REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   7. Curriculum - khung chương trình đào tạo (ngành X, kỳ thứ N học môn Y)
   ============================================================================= */
CREATE TABLE dbo.Curriculum
(
    CurriculumId INT NOT NULL IDENTITY(1,1),
    MajorId      INT NOT NULL,
    CourseId     INT NOT NULL,
    TermNo       INT NOT NULL,
    IsMandatory  BIT NOT NULL CONSTRAINT DF_Curriculum_Mandatory DEFAULT (1),
    CONSTRAINT PK_Curriculum        PRIMARY KEY (CurriculumId),
    -- Một môn chỉ xuất hiện đúng 1 lần trong khung của một ngành
    CONSTRAINT UQ_Curriculum_Pair   UNIQUE (MajorId, CourseId),
    CONSTRAINT CK_Curriculum_Term   CHECK (TermNo >= 1),
    CONSTRAINT FK_Curriculum_Major  FOREIGN KEY (MajorId)  REFERENCES dbo.Major (MajorId)  ON DELETE CASCADE,
    CONSTRAINT FK_Curriculum_Course FOREIGN KEY (CourseId) REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   8. Semester - học kỳ
   ============================================================================= */
CREATE TABLE dbo.Semester
(
    SemesterId   INT          IDENTITY(1,1) NOT NULL,
    SemesterCode NVARCHAR(10) NOT NULL,
    SemesterName NVARCHAR(50) NOT NULL,
    StartDate    DATE         NOT NULL,
    EndDate      DATE         NOT NULL,
    -- Sắp xếp theo thời gian. BẮT BUỘC phải có: sort theo SemesterCode là SAI
    -- ("FA25" < "SP26" theo alphabet nhưng FA25 lại diễn ra TRƯỚC SP26).
    DisplayOrder INT          NOT NULL,
    IsCurrent    BIT          NOT NULL CONSTRAINT DF_Semester_IsCurrent DEFAULT (0),
    CONSTRAINT PK_Semester        PRIMARY KEY (SemesterId),
    CONSTRAINT UQ_Semester_Code   UNIQUE (SemesterCode),
    CONSTRAINT UQ_Semester_Order  UNIQUE (DisplayOrder),
    CONSTRAINT CK_Semester_Dates  CHECK (EndDate > StartDate)
);
GO

/* =============================================================================
   9. Enrollment - sinh viên học môn nào ở kỳ nào + kết quả cuối cùng
   ============================================================================= */
CREATE TABLE dbo.Enrollment
(
    EnrollmentId INT           IDENTITY(1,1) NOT NULL,
    StudentId    INT           NOT NULL,
    CourseId     INT           NOT NULL,
    SemesterId   INT           NOT NULL,
    -- Studying | Passed | Failed | Withdrawn
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Enrollment_Status DEFAULT (N'Studying'),
    -- DECIMAL chứ KHÔNG dùng FLOAT: FLOAT có sai số nhị phân, cộng dồn qua
    -- hàng chục môn sẽ làm GPA lệch ở chữ số thập phân thứ 2.
    FinalScore   DECIMAL(4,2)  NULL,
    LetterGrade  NVARCHAR(5)   NULL,
    GradePoint   DECIMAL(3,2)  NULL,
    -- Học lại: chỉ lần thi được tính vào GPA mới có IsCounted = 1.
    IsCounted    BIT           NOT NULL CONSTRAINT DF_Enrollment_IsCounted DEFAULT (1),
    AttemptNo    INT           NOT NULL CONSTRAINT DF_Enrollment_Attempt   DEFAULT (1),
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Enrollment_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2(0)  NULL,
    CONSTRAINT PK_Enrollment          PRIMARY KEY (EnrollmentId),
    -- Chặn đăng ký trùng: 1 sinh viên không thể học 1 môn 2 lần trong cùng kỳ
    CONSTRAINT UQ_Enrollment_Unique   UNIQUE (StudentId, CourseId, SemesterId),
    CONSTRAINT CK_Enrollment_Score    CHECK (FinalScore IS NULL OR (FinalScore >= 0 AND FinalScore <= 10)),
    CONSTRAINT CK_Enrollment_Attempt  CHECK (AttemptNo >= 1),
    CONSTRAINT FK_Enrollment_Student  FOREIGN KEY (StudentId)  REFERENCES dbo.Student  (StudentId)  ON DELETE CASCADE,
    CONSTRAINT FK_Enrollment_Course   FOREIGN KEY (CourseId)   REFERENCES dbo.Course   (CourseId)   ON DELETE NO ACTION,
    CONSTRAINT FK_Enrollment_Semester FOREIGN KEY (SemesterId) REFERENCES dbo.Semester (SemesterId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   10. Assessment - đầu điểm của môn ("Assignment" 30%, "Final Exam" 40%...)
   ============================================================================= */
CREATE TABLE dbo.Assessment
(
    AssessmentId   INT           IDENTITY(1,1) NOT NULL,
    CourseId       INT           NOT NULL,
    Name           NVARCHAR(100) NOT NULL,
    -- Trọng số dạng phân số: 0.30 = 30%. Tổng các đầu điểm của 1 môn phải = 1.
    Weight         DECIMAL(5,4)  NOT NULL,
    -- Điểm sàn của riêng đầu điểm này. Dưới ngưỡng là trượt môn dù tổng >= 5.
    MinScoreToPass DECIMAL(4,2)  NULL,
    DisplayOrder   INT           NOT NULL CONSTRAINT DF_Assessment_Order DEFAULT (0),
    CONSTRAINT PK_Assessment        PRIMARY KEY (AssessmentId),
    CONSTRAINT UQ_Assessment_Name   UNIQUE (CourseId, Name),
    CONSTRAINT CK_Assessment_Weight CHECK (Weight > 0 AND Weight <= 1),
    CONSTRAINT FK_Assessment_Course FOREIGN KEY (CourseId) REFERENCES dbo.Course (CourseId) ON DELETE CASCADE
);
GO

/* =============================================================================
   11. Grade - điểm thành phần thực tế của một enrollment
   ============================================================================= */
CREATE TABLE dbo.Grade
(
    GradeId      INT          IDENTITY(1,1) NOT NULL,
    EnrollmentId INT          NOT NULL,
    AssessmentId INT          NOT NULL,
    Score        DECIMAL(4,2) NOT NULL,
    UpdatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Grade_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Grade            PRIMARY KEY (GradeId),
    -- Mỗi đầu điểm chỉ có đúng 1 con điểm trong 1 lần học
    CONSTRAINT UQ_Grade_Unique     UNIQUE (EnrollmentId, AssessmentId),
    CONSTRAINT CK_Grade_Score      CHECK (Score >= 0 AND Score <= 10),
    CONSTRAINT FK_Grade_Enrollment FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollment (EnrollmentId) ON DELETE CASCADE,
    -- NO ACTION: nếu cascade thì xóa Course sẽ có 2 đường tới Grade
    -- (qua Assessment và qua Enrollment) => SQL Server từ chối tạo bảng.
    CONSTRAINT FK_Grade_Assessment FOREIGN KEY (AssessmentId) REFERENCES dbo.Assessment (AssessmentId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   12. GradeScale - bảng quy đổi điểm 10 -> chữ -> thang 4

   Khoảng là NỬA MỞ: MinScore <= Score < MaxScore. Nhờ vậy không có kẽ hở
   (vd 8.45) và không có vùng chồng lấn giữa hai hàng.
   ============================================================================= */
CREATE TABLE dbo.GradeScale
(
    GradeScaleId INT           IDENTITY(1,1) NOT NULL,
    MinScore     DECIMAL(4,2)  NOT NULL,
    MaxScore     DECIMAL(4,2)  NOT NULL,
    LetterGrade  NVARCHAR(5)   NOT NULL,
    GradePoint   DECIMAL(3,2)  NOT NULL,
    Description  NVARCHAR(50)  NULL,
    CONSTRAINT PK_GradeScale         PRIMARY KEY (GradeScaleId),
    CONSTRAINT UQ_GradeScale_Letter  UNIQUE (LetterGrade),
    CONSTRAINT CK_GradeScale_Range   CHECK (MaxScore > MinScore)
);
GO

/* =============================================================================
   13. AcademicPlan - kế hoạch học tập của sinh viên
   ============================================================================= */
CREATE TABLE dbo.AcademicPlan
(
    PlanId    INT           IDENTITY(1,1) NOT NULL,
    StudentId INT           NOT NULL,
    PlanName  NVARCHAR(150) NOT NULL,
    Note      NVARCHAR(500) NULL,
    IsActive  BIT           NOT NULL CONSTRAINT DF_Plan_IsActive  DEFAULT (1),
    CreatedAt DATETIME2(0)  NOT NULL CONSTRAINT DF_Plan_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0)  NULL,
    CONSTRAINT PK_AcademicPlan      PRIMARY KEY (PlanId),
    CONSTRAINT FK_Plan_Student      FOREIGN KEY (StudentId) REFERENCES dbo.Student (StudentId) ON DELETE CASCADE
);
GO

/* =============================================================================
   14. AcademicPlanItem - một dòng trong kế hoạch (môn + kỳ dự kiến)
   ============================================================================= */
CREATE TABLE dbo.AcademicPlanItem
(
    PlanItemId    INT          IDENTITY(1,1) NOT NULL,
    PlanId        INT          NOT NULL,
    CourseId      INT          NOT NULL,
    -- Kỳ cụ thể (nếu đã chọn) hoặc chỉ là "kỳ thứ N" khi chưa có Semester
    SemesterId    INT          NULL,
    TargetTermNo  INT          NULL,
    -- Điểm kỳ vọng, dùng cho What-if GPA
    ExpectedScore DECIMAL(4,2) NULL,
    DisplayOrder  INT          NOT NULL CONSTRAINT DF_PlanItem_Order DEFAULT (0),
    CONSTRAINT PK_AcademicPlanItem   PRIMARY KEY (PlanItemId),
    CONSTRAINT UQ_PlanItem_Course    UNIQUE (PlanId, CourseId),
    CONSTRAINT CK_PlanItem_Score     CHECK (ExpectedScore IS NULL OR (ExpectedScore >= 0 AND ExpectedScore <= 10)),
    CONSTRAINT FK_PlanItem_Plan      FOREIGN KEY (PlanId)     REFERENCES dbo.AcademicPlan (PlanId)     ON DELETE CASCADE,
    CONSTRAINT FK_PlanItem_Course    FOREIGN KEY (CourseId)   REFERENCES dbo.Course       (CourseId)   ON DELETE NO ACTION,
    CONSTRAINT FK_PlanItem_Semester  FOREIGN KEY (SemesterId) REFERENCES dbo.Semester     (SemesterId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   15. AuditLog - ghi vết thao tác (chủ yếu của Admin)

   UserId để NULL được và FK là NO ACTION: xóa tài khoản KHÔNG được xóa mất
   dấu vết thao tác của tài khoản đó.
   ============================================================================= */
CREATE TABLE dbo.AuditLog
(
    AuditLogId BIGINT        IDENTITY(1,1) NOT NULL,
    UserId     INT           NULL,
    Action     NVARCHAR(50)  NOT NULL,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId   NVARCHAR(50)  NULL,
    Detail     NVARCHAR(MAX) NULL,
    CreatedAt  DATETIME2(0)  NOT NULL CONSTRAINT DF_AuditLog_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLog      PRIMARY KEY (AuditLogId),
    CONSTRAINT FK_AuditLog_User FOREIGN KEY (UserId) REFERENCES dbo.AppUser (UserId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   16. Material - tài liệu học tập (metadata)

   TÁCH LÀM HAI BẢNG là quyết định thiết kế quan trọng nhất ở đây:
   metadata nằm ở bảng này, còn nội dung file nằm ở dbo.MaterialFile.

   Nếu nhét VARBINARY(MAX) chung vào một bảng thì mỗi lần mở danh sách tài
   liệu, EF sẽ kéo toàn bộ byte của MỌI file về máy chỉ để hiển thị cái tên.
   Danh sách 50 tài liệu PDF là vài trăm MB đi qua mạng cho một màn hình
   lẽ ra chỉ cần vài KB.
   ============================================================================= */
CREATE TABLE dbo.Material
(
    MaterialId       INT            IDENTITY(1,1) NOT NULL,
    -- NULL nghĩa là tài liệu dùng chung, không gắn với môn học cụ thể nào
    CourseId         INT            NULL,
    Title            NVARCHAR(200)  NOT NULL,
    Description      NVARCHAR(500)  NULL,
    -- Slide | Textbook | Exercise | Exam | Reference | Other
    Category         NVARCHAR(30)   NOT NULL CONSTRAINT DF_Material_Category DEFAULT (N'Other'),
    FileName         NVARCHAR(255)  NOT NULL,
    ContentType      NVARCHAR(100)  NOT NULL,
    FileSizeBytes    BIGINT         NOT NULL,
    -- Băm nội dung để phát hiện tải lên trùng file
    ContentHash      CHAR(64)       NULL,
    UploadedByUserId INT            NULL,
    UploadedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Material_UploadedAt DEFAULT (SYSUTCDATETIME()),
    DownloadCount    INT            NOT NULL CONSTRAINT DF_Material_Downloads  DEFAULT (0),
    IsActive         BIT            NOT NULL CONSTRAINT DF_Material_IsActive   DEFAULT (1),
    CONSTRAINT PK_Material          PRIMARY KEY (MaterialId),
    CONSTRAINT CK_Material_Size     CHECK (FileSizeBytes > 0 AND FileSizeBytes <= 26214400), -- trần 25 MB
    CONSTRAINT CK_Material_Downloads CHECK (DownloadCount >= 0),
    CONSTRAINT FK_Material_Course   FOREIGN KEY (CourseId)         REFERENCES dbo.Course  (CourseId) ON DELETE NO ACTION,
    -- NO ACTION: xóa tài khoản không được xóa mất tài liệu người đó đã tải lên
    CONSTRAINT FK_Material_User     FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser (UserId)   ON DELETE NO ACTION
);
GO

/* =============================================================================
   17. MaterialFile - nội dung nhị phân của tài liệu (quan hệ 1-1 với Material)

   Chỉ đọc bảng này khi người dùng thực sự bấm Tải xuống.
   ============================================================================= */
CREATE TABLE dbo.MaterialFile
(
    MaterialId INT            NOT NULL,
    Content    VARBINARY(MAX) NOT NULL,
    CONSTRAINT PK_MaterialFile      PRIMARY KEY (MaterialId),
    CONSTRAINT FK_MaterialFile_Item FOREIGN KEY (MaterialId) REFERENCES dbo.Material (MaterialId) ON DELETE CASCADE
);
GO

/* =============================================================================
   Index - phủ các truy vấn nóng nhất của app

   SQL Server KHÔNG tự tạo index cho cột khóa ngoại. Thiếu các index này thì
   mọi màn hình (transcript, dashboard, tiến độ tốt nghiệp) đều quét toàn bảng.
   ============================================================================= */

-- Transcript + GPA: luôn lọc theo sinh viên, gom theo kỳ
CREATE INDEX IX_Enrollment_Student_Semester ON dbo.Enrollment (StudentId, SemesterId) INCLUDE (CourseId, Status, FinalScore, IsCounted);
CREATE INDEX IX_Enrollment_Course           ON dbo.Enrollment (CourseId);
CREATE INDEX IX_Enrollment_Semester         ON dbo.Enrollment (SemesterId);

-- Màn nhập điểm: lấy hết điểm thành phần của 1 enrollment
CREATE INDEX IX_Grade_Enrollment            ON dbo.Grade (EnrollmentId) INCLUDE (AssessmentId, Score);
CREATE INDEX IX_Assessment_Course           ON dbo.Assessment (CourseId);

-- Kiểm tra tiên quyết (đệ quy) đi theo cả 2 chiều
CREATE INDEX IX_Prerequisite_Course         ON dbo.Prerequisite (CourseId)         INCLUDE (RequiredCourseId, Type);
CREATE INDEX IX_Prerequisite_Required       ON dbo.Prerequisite (RequiredCourseId) INCLUDE (CourseId);

-- Tiến độ tốt nghiệp: đối chiếu khung CTĐT với môn đã học
CREATE INDEX IX_Curriculum_Major            ON dbo.Curriculum (MajorId, TermNo) INCLUDE (CourseId, IsMandatory);

CREATE INDEX IX_Student_Major               ON dbo.Student (MajorId);
CREATE INDEX IX_PlanItem_Plan               ON dbo.AcademicPlanItem (PlanId);
CREATE INDEX IX_AuditLog_CreatedAt          ON dbo.AuditLog (CreatedAt DESC);

-- Màn hình tài liệu: lọc theo môn, và tìm kiếm theo tiêu đề
CREATE INDEX IX_Material_Course             ON dbo.Material (CourseId, IsActive) INCLUDE (Title, Category, FileSizeBytes);
CREATE INDEX IX_Material_Title              ON dbo.Material (Title);
CREATE INDEX IX_Material_Category           ON dbo.Material (Category);
GO

PRINT '[01_schema] OK - da tao database SAT voi 17 bang.';
GO
