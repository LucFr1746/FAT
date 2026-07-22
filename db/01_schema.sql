/* =============================================================================
   FAT - FPT Academic Tracker
   01_schema.sql : creates the database and every table.

   THIS FILE IS THE SOURCE OF TRUTH for the schema - the project does NOT use
   EF Core Migrations. The entities under FAT.Domain/Entities must match these
   tables one for one. Changing a column here means changing the entity too,
   and telling the team to re-run the setup script.

   The script DROPS AND RECREATES the FAT database, so running it any number of
   times always produces the same result (it is idempotent). Everything
   currently stored in FAT is lost.

   Run: .\db\setup-db.ps1        (recommended)
   or : open this file in SSMS, press F5, then run 02_ and 03_.
   ============================================================================= */

SET NOCOUNT ON;
GO

USE master;
GO

/* Kick every open connection before dropping, otherwise DROP blocks forever
   because someone still has the app open or a query tab pointed at FAT. */
IF DB_ID(N'FAT') IS NOT NULL
BEGIN
    ALTER DATABASE FAT SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE FAT;
END
GO

CREATE DATABASE FAT;
GO

ALTER DATABASE FAT SET RECOVERY SIMPLE;
GO

USE FAT;
GO

/* =============================================================================
   1. Role - login roles
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
   2. AppUser - login accounts

   Named AppUser rather than User because USER is a reserved word in T-SQL;
   using it would force [User] in every statement and someone always forgets.
   ============================================================================= */
CREATE TABLE dbo.AppUser
(
    UserId       INT            IDENTITY(1,1) NOT NULL,
    Username     NVARCHAR(50)   NOT NULL,
    -- BCrypt hash (60 characters). NEVER store a plaintext password.
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
   3. Major - degree programmes
   ============================================================================= */
CREATE TABLE dbo.Major
(
    MajorId         INT           IDENTITY(1,1) NOT NULL,
    MajorCode       NVARCHAR(20)  NOT NULL,
    MajorName       NVARCHAR(150) NOT NULL,
    -- Credits required to graduate. This is the DENOMINATOR of the graduation
    -- percentage, so it must equal the total credits in Curriculum.
    -- 02_seed_master.sql asserts exactly that.
    RequiredCredits INT           NOT NULL,
    TotalTerms      INT           NOT NULL,
    IsActive        BIT           NOT NULL CONSTRAINT DF_Major_IsActive DEFAULT (1),
    CONSTRAINT PK_Major        PRIMARY KEY (MajorId),
    CONSTRAINT UQ_Major_Code   UNIQUE (MajorCode),
    CONSTRAINT CK_Major_Credit CHECK (RequiredCredits > 0 AND TotalTerms > 0)
);
GO

/* =============================================================================
   4. Student - student profiles
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
    -- One account maps to exactly one student profile
    CONSTRAINT UQ_Student_UserId UNIQUE (UserId),
    CONSTRAINT FK_Student_User   FOREIGN KEY (UserId)  REFERENCES dbo.AppUser (UserId) ON DELETE CASCADE,
    CONSTRAINT FK_Student_Major  FOREIGN KEY (MajorId) REFERENCES dbo.Major (MajorId)
);
GO

/* =============================================================================
   5. Course - the course catalog
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
   6. Prerequisite - course dependencies

   CourseId requires RequiredCourseId to be completed first.
   Both foreign keys point back at Course, so NO ACTION is MANDATORY: SQL Server
   forbids two cascade paths into the same table ("multiple cascade paths").
   ============================================================================= */
CREATE TABLE dbo.Prerequisite
(
    PrerequisiteId   INT          IDENTITY(1,1) NOT NULL,
    CourseId         INT          NOT NULL,
    RequiredCourseId INT          NOT NULL,
    -- Prerequisite (take earlier) | Corequisite (take in the same term)
    Type             NVARCHAR(20) NOT NULL CONSTRAINT DF_Prereq_Type DEFAULT (N'Prerequisite'),
    CONSTRAINT PK_Prerequisite       PRIMARY KEY (PrerequisiteId),
    CONSTRAINT UQ_Prerequisite_Pair  UNIQUE (CourseId, RequiredCourseId),
    -- Block a course from being its own prerequisite (a cycle of length one)
    CONSTRAINT CK_Prerequisite_Self  CHECK (CourseId <> RequiredCourseId),
    CONSTRAINT FK_Prereq_Course      FOREIGN KEY (CourseId)         REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION,
    CONSTRAINT FK_Prereq_Required    FOREIGN KEY (RequiredCourseId) REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   7. Curriculum - the study path (major X takes course Y in term N)
   ============================================================================= */
CREATE TABLE dbo.Curriculum
(
    CurriculumId INT NOT NULL IDENTITY(1,1),
    MajorId      INT NOT NULL,
    CourseId     INT NOT NULL,
    TermNo       INT NOT NULL,
    IsMandatory  BIT NOT NULL CONSTRAINT DF_Curriculum_Mandatory DEFAULT (1),
    CONSTRAINT PK_Curriculum        PRIMARY KEY (CurriculumId),
    -- A course appears at most once in a given major's curriculum
    CONSTRAINT UQ_Curriculum_Pair   UNIQUE (MajorId, CourseId),
    CONSTRAINT CK_Curriculum_Term   CHECK (TermNo >= 1),
    CONSTRAINT FK_Curriculum_Major  FOREIGN KEY (MajorId)  REFERENCES dbo.Major (MajorId)  ON DELETE CASCADE,
    CONSTRAINT FK_Curriculum_Course FOREIGN KEY (CourseId) REFERENCES dbo.Course (CourseId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   8. Semester - academic terms
   ============================================================================= */
CREATE TABLE dbo.Semester
(
    SemesterId   INT          IDENTITY(1,1) NOT NULL,
    SemesterCode NVARCHAR(10) NOT NULL,
    SemesterName NVARCHAR(50) NOT NULL,
    StartDate    DATE         NOT NULL,
    EndDate      DATE         NOT NULL,
    -- True chronological order. THIS IS REQUIRED: sorting by SemesterCode is
    -- wrong, because "FA25" sorts before "SP26" alphabetically even though FA25
    -- happens first in time.
    DisplayOrder INT          NOT NULL,
    IsCurrent    BIT          NOT NULL CONSTRAINT DF_Semester_IsCurrent DEFAULT (0),
    CONSTRAINT PK_Semester        PRIMARY KEY (SemesterId),
    CONSTRAINT UQ_Semester_Code   UNIQUE (SemesterCode),
    CONSTRAINT UQ_Semester_Order  UNIQUE (DisplayOrder),
    CONSTRAINT CK_Semester_Dates  CHECK (EndDate > StartDate)
);
GO

/* =============================================================================
   9. Enrollment - a student taking a course in a term, plus the outcome
   ============================================================================= */
CREATE TABLE dbo.Enrollment
(
    EnrollmentId INT           IDENTITY(1,1) NOT NULL,
    StudentId    INT           NOT NULL,
    CourseId     INT           NOT NULL,
    SemesterId   INT           NOT NULL,
    -- Studying | Passed | Failed | Withdrawn
    Status       NVARCHAR(20)  NOT NULL CONSTRAINT DF_Enrollment_Status DEFAULT (N'Studying'),
    -- DECIMAL, never FLOAT: binary rounding error accumulated over dozens of
    -- courses is enough to shift the GPA in its second decimal place.
    FinalScore   DECIMAL(4,2)  NULL,
    LetterGrade  NVARCHAR(5)   NULL,
    GradePoint   DECIMAL(3,2)  NULL,
    -- Retakes: only the attempt that counts toward the GPA has IsCounted = 1.
    IsCounted    BIT           NOT NULL CONSTRAINT DF_Enrollment_IsCounted DEFAULT (1),
    AttemptNo    INT           NOT NULL CONSTRAINT DF_Enrollment_Attempt   DEFAULT (1),
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Enrollment_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    DATETIME2(0)  NULL,
    CONSTRAINT PK_Enrollment          PRIMARY KEY (EnrollmentId),
    -- Block duplicate registration: a student cannot take one course twice in
    -- the same term
    CONSTRAINT UQ_Enrollment_Unique   UNIQUE (StudentId, CourseId, SemesterId),
    CONSTRAINT CK_Enrollment_Score    CHECK (FinalScore IS NULL OR (FinalScore >= 0 AND FinalScore <= 10)),
    CONSTRAINT CK_Enrollment_Attempt  CHECK (AttemptNo >= 1),
    CONSTRAINT FK_Enrollment_Student  FOREIGN KEY (StudentId)  REFERENCES dbo.Student  (StudentId)  ON DELETE CASCADE,
    CONSTRAINT FK_Enrollment_Course   FOREIGN KEY (CourseId)   REFERENCES dbo.Course   (CourseId)   ON DELETE NO ACTION,
    CONSTRAINT FK_Enrollment_Semester FOREIGN KEY (SemesterId) REFERENCES dbo.Semester (SemesterId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   10. Assessment - grade components ("Assignment" 20%, "Final Exam" 40%, ...)
   ============================================================================= */
CREATE TABLE dbo.Assessment
(
    AssessmentId   INT           IDENTITY(1,1) NOT NULL,
    CourseId       INT           NOT NULL,
    Name           NVARCHAR(100) NOT NULL,
    -- Weight as a fraction: 0.30 means 30%. Components of one course sum to 1.
    Weight         DECIMAL(5,4)  NOT NULL,
    -- Per-component minimum. Falling below it fails the course even when the
    -- weighted total is 5.0 or higher.
    MinScoreToPass DECIMAL(4,2)  NULL,
    DisplayOrder   INT           NOT NULL CONSTRAINT DF_Assessment_Order DEFAULT (0),
    CONSTRAINT PK_Assessment        PRIMARY KEY (AssessmentId),
    CONSTRAINT UQ_Assessment_Name   UNIQUE (CourseId, Name),
    CONSTRAINT CK_Assessment_Weight CHECK (Weight > 0 AND Weight <= 1),
    CONSTRAINT FK_Assessment_Course FOREIGN KEY (CourseId) REFERENCES dbo.Course (CourseId) ON DELETE CASCADE
);
GO

/* =============================================================================
   11. Grade - the actual component scores of one enrollment
   ============================================================================= */
CREATE TABLE dbo.Grade
(
    GradeId      INT          IDENTITY(1,1) NOT NULL,
    EnrollmentId INT          NOT NULL,
    AssessmentId INT          NOT NULL,
    Score        DECIMAL(4,2) NOT NULL,
    UpdatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_Grade_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Grade            PRIMARY KEY (GradeId),
    -- One score per component per attempt
    CONSTRAINT UQ_Grade_Unique     UNIQUE (EnrollmentId, AssessmentId),
    CONSTRAINT CK_Grade_Score      CHECK (Score >= 0 AND Score <= 10),
    CONSTRAINT FK_Grade_Enrollment FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollment (EnrollmentId) ON DELETE CASCADE,
    -- NO ACTION is required: cascading would give a Course deletion two paths
    -- into Grade (via Assessment and via Enrollment), which SQL Server rejects.
    CONSTRAINT FK_Grade_Assessment FOREIGN KEY (AssessmentId) REFERENCES dbo.Assessment (AssessmentId) ON DELETE NO ACTION
);
GO

/* =============================================================================
   12. GradeScale - converts a numeric score to a letter and a 4-point value

   Bands are HALF-OPEN: MinScore <= Score < MaxScore. That leaves no gap
   (such as 8.45) and no overlap between adjacent bands.
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
   13. AcademicPlan - a student's plan for upcoming terms
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
   14. AcademicPlanItem - one course placed into a plan
   ============================================================================= */
CREATE TABLE dbo.AcademicPlanItem
(
    PlanItemId    INT          IDENTITY(1,1) NOT NULL,
    PlanId        INT          NOT NULL,
    CourseId      INT          NOT NULL,
    -- A concrete term when one has been chosen, otherwise just "term N"
    SemesterId    INT          NULL,
    TargetTermNo  INT          NULL,
    -- Expected score, the input to the what-if GPA feature
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
   15. AuditLog - trail of data changes, mostly by administrators

   UserId is nullable and the foreign key is NO ACTION: deleting an account must
   not erase the record of what that account did.
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
   16. Material - learning materials (metadata)

   SPLITTING THIS ACROSS TWO TABLES is the important design decision here:
   metadata lives in this table, the file bytes live in dbo.MaterialFile.

   Put VARBINARY(MAX) in the same table and every time someone opens the
   material list, EF drags the full contents of EVERY file across the wire just
   to render their names. A list of 50 PDFs becomes hundreds of megabytes for a
   screen that needs a few kilobytes.
   ============================================================================= */
CREATE TABLE dbo.Material
(
    MaterialId       INT            IDENTITY(1,1) NOT NULL,
    -- NULL means a general material not tied to any course
    CourseId         INT            NULL,
    Title            NVARCHAR(200)  NOT NULL,
    Description      NVARCHAR(500)  NULL,
    -- Slide | Textbook | Exercise | Exam | Reference | Other
    Category         NVARCHAR(30)   NOT NULL CONSTRAINT DF_Material_Category DEFAULT (N'Other'),
    FileName         NVARCHAR(255)  NOT NULL,
    ContentType      NVARCHAR(100)  NOT NULL,
    FileSizeBytes    BIGINT         NOT NULL,
    -- Content hash, used to detect duplicate uploads
    ContentHash      CHAR(64)       NULL,
    UploadedByUserId INT            NULL,
    UploadedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Material_UploadedAt DEFAULT (SYSUTCDATETIME()),
    DownloadCount    INT            NOT NULL CONSTRAINT DF_Material_Downloads  DEFAULT (0),
    IsActive         BIT            NOT NULL CONSTRAINT DF_Material_IsActive   DEFAULT (1),
    CONSTRAINT PK_Material           PRIMARY KEY (MaterialId),
    CONSTRAINT CK_Material_Size      CHECK (FileSizeBytes > 0 AND FileSizeBytes <= 26214400), -- 25 MB cap
    CONSTRAINT CK_Material_Downloads CHECK (DownloadCount >= 0),
    CONSTRAINT FK_Material_Course    FOREIGN KEY (CourseId)         REFERENCES dbo.Course  (CourseId) ON DELETE NO ACTION,
    -- NO ACTION: deleting an account must not delete the materials they uploaded
    CONSTRAINT FK_Material_User      FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser (UserId)   ON DELETE NO ACTION
);
GO

/* =============================================================================
   17. MaterialFile - the binary payload (one-to-one with Material)

   Only read this table when the user actually clicks Download.
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
   Indexes - covering the hottest queries in the application

   SQL Server does NOT create indexes on foreign key columns automatically.
   Without these, every screen (transcript, dashboard, graduation progress)
   falls back to a full table scan.
   ============================================================================= */

-- Transcript and GPA: always filtered by student, grouped by term
CREATE INDEX IX_Enrollment_Student_Semester ON dbo.Enrollment (StudentId, SemesterId) INCLUDE (CourseId, Status, FinalScore, IsCounted);
CREATE INDEX IX_Enrollment_Course           ON dbo.Enrollment (CourseId);
CREATE INDEX IX_Enrollment_Semester         ON dbo.Enrollment (SemesterId);

-- Grade entry screen: fetch every component of one enrollment
CREATE INDEX IX_Grade_Enrollment            ON dbo.Grade (EnrollmentId) INCLUDE (AssessmentId, Score);
CREATE INDEX IX_Assessment_Course           ON dbo.Assessment (CourseId);

-- Prerequisite resolution walks the graph in both directions
CREATE INDEX IX_Prerequisite_Course         ON dbo.Prerequisite (CourseId)         INCLUDE (RequiredCourseId, Type);
CREATE INDEX IX_Prerequisite_Required       ON dbo.Prerequisite (RequiredCourseId) INCLUDE (CourseId);

-- Graduation progress: curriculum matched against completed courses
CREATE INDEX IX_Curriculum_Major            ON dbo.Curriculum (MajorId, TermNo) INCLUDE (CourseId, IsMandatory);

CREATE INDEX IX_Student_Major               ON dbo.Student (MajorId);
CREATE INDEX IX_PlanItem_Plan               ON dbo.AcademicPlanItem (PlanId);
CREATE INDEX IX_AuditLog_CreatedAt          ON dbo.AuditLog (CreatedAt DESC);

-- Materials screen: filter by course, search by title
CREATE INDEX IX_Material_Course             ON dbo.Material (CourseId, IsActive) INCLUDE (Title, Category, FileSizeBytes);
CREATE INDEX IX_Material_Title              ON dbo.Material (Title);
CREATE INDEX IX_Material_Category           ON dbo.Material (Category);
GO

PRINT '[01_schema] OK - created database FAT with 17 tables.';
GO
