using Microsoft.EntityFrameworkCore;

namespace Data;

/// <summary>
/// Brings an EXISTING FAT database up to the current model without dropping it.
///
/// WHY THIS EXISTS
/// db/01_schema.sql is the source of truth, but it drops and recreates the
/// whole database - fine for a fresh clone, unacceptable for a teammate who
/// already has data. This class is the other half: the same changes expressed
/// as idempotent T-SQL that can run on every startup.
///
/// RULES FOR ADDING TO THIS FILE
///   1. Every statement must be guarded (IF NOT EXISTS / IF EXISTS). Running
///      the upgrader twice must be a no-op, because it runs on every launch.
///   2. Never DROP a column or table here. Losing a teammate's data to an
///      automatic startup step is not recoverable.
///   3. Mirror the change in db/01_schema.sql in the SAME commit, or a fresh
///      clone and an upgraded database end up with different schemas.
/// </summary>
public static class SchemaUpgrader
{
    /// <summary>
    /// Each step is a named, self-guarded batch. Names appear in the exception
    /// message when one fails, so a broken upgrade says which change broke
    /// rather than just "incorrect syntax".
    /// </summary>
    private static readonly (string Name, string Sql)[] Steps =
    [
        // Google OAuth columns. They reached db/01_schema.sql but never had an
        // upgrade path, so any database created before that commit still fails
        // every AppUser query with "Invalid column name 'GoogleId'".
        ("AppUser.GoogleId", """
            IF OBJECT_ID(N'dbo.AppUser', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AppUser', N'GoogleId') IS NULL
                ALTER TABLE dbo.AppUser ADD GoogleId NVARCHAR(255) NULL;
            """),

        ("AppUser.AvatarUrl", """
            IF OBJECT_ID(N'dbo.AppUser', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AppUser', N'AvatarUrl') IS NULL
                ALTER TABLE dbo.AppUser ADD AvatarUrl NVARCHAR(1000) NULL;
            """),

        // Filtered so that the many accounts with no Google link do not all
        // collide on NULL.
        ("AppUser.GoogleId unique index", """
            IF OBJECT_ID(N'dbo.AppUser', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.AppUser', N'GoogleId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AppUser_GoogleId')
                CREATE UNIQUE INDEX IX_AppUser_GoogleId
                    ON dbo.AppUser (GoogleId) WHERE GoogleId IS NOT NULL;
            """),

        // PasswordHash must be nullable for Google-only accounts.
        ("AppUser.PasswordHash nullable", """
            IF OBJECT_ID(N'dbo.AppUser', N'U') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.columns
                           WHERE object_id = OBJECT_ID(N'dbo.AppUser')
                             AND name = N'PasswordHash' AND is_nullable = 0)
                ALTER TABLE dbo.AppUser ALTER COLUMN PasswordHash NVARCHAR(255) NULL;
            """),

        ("Student.CurrentSemester", """
            IF OBJECT_ID(N'dbo.Student', N'U') IS NOT NULL
            BEGIN
               IF COL_LENGTH(N'dbo.Student', N'CurrentSemester') IS NULL
                   ALTER TABLE dbo.Student ADD CurrentSemester NVARCHAR(100) NULL;
               ELSE
                   ALTER TABLE dbo.Student ALTER COLUMN CurrentSemester NVARCHAR(100) NULL;
            END
            """),

        ("Student.CurrentTermNo", """
            IF OBJECT_ID(N'dbo.Student', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Student', N'CurrentTermNo') IS NULL
                ALTER TABLE dbo.Student ADD CurrentTermNo INT NULL;
            """),

        ("Student.IsProfileCompleted", """
            IF OBJECT_ID(N'dbo.Student', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Student', N'IsProfileCompleted') IS NULL
                ALTER TABLE dbo.Student ADD IsProfileCompleted BIT NOT NULL
                    CONSTRAINT DF_Student_IsProfileCompleted DEFAULT (0);
            """),

        ("Student.Phone", """
            IF OBJECT_ID(N'dbo.Student', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Student', N'Phone') IS NULL
                ALTER TABLE dbo.Student ADD Phone NVARCHAR(20) NULL;
            """),

        ("Student.ClassName", """
            IF OBJECT_ID(N'dbo.Student', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Student', N'ClassName') IS NULL
                ALTER TABLE dbo.Student ADD ClassName NVARCHAR(50) NULL;
            """),

        ("Major.Description", """
            IF OBJECT_ID(N'dbo.Major', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Major', N'Description') IS NULL
                ALTER TABLE dbo.Major ADD Description NVARCHAR(500) NULL;
            """),

        // The FLM syllabus descriptions run past 6,000 characters, so the
        // original NVARCHAR(500) truncates real data on import.
        ("Course.Description widened to MAX", """
            IF OBJECT_ID(N'dbo.Course', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Course', N'Description') <> -1
                ALTER TABLE dbo.Course ALTER COLUMN Description NVARCHAR(MAX) NULL;
            """),

        ("Course.CountsTowardGpa", """
            IF OBJECT_ID(N'dbo.Course', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Course', N'CountsTowardGpa') IS NULL
                ALTER TABLE dbo.Course ADD CountsTowardGpa BIT NOT NULL
                    CONSTRAINT DF_Course_CountsTowardGpa DEFAULT (1);
            """),

        ("Course.MinAvgMarkToPass", """
            IF OBJECT_ID(N'dbo.Course', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Course', N'MinAvgMarkToPass') IS NULL
                ALTER TABLE dbo.Course ADD MinAvgMarkToPass DECIMAL(4,2) NULL;
            """),

        ("Course.SyllabusCode", """
            IF OBJECT_ID(N'dbo.Course', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Course', N'SyllabusCode') IS NULL
                ALTER TABLE dbo.Course ADD SyllabusCode NVARCHAR(20) NULL;
            """),

        ("Course.PrerequisiteText", """
            IF OBJECT_ID(N'dbo.Course', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Course', N'PrerequisiteText') IS NULL
                ALTER TABLE dbo.Course ADD PrerequisiteText NVARCHAR(500) NULL;
            """),

        // 156 is the longest real FLM category name.
        ("Assessment.Name widened to 200", """
            IF OBJECT_ID(N'dbo.Assessment', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Assessment', N'Name') < 400
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = N'UQ_Assessment_Name'
                             AND object_id = OBJECT_ID(N'dbo.Assessment'))
                    ALTER TABLE dbo.Assessment DROP CONSTRAINT UQ_Assessment_Name;

                ALTER TABLE dbo.Assessment ALTER COLUMN Name NVARCHAR(200) NOT NULL;

                ALTER TABLE dbo.Assessment
                    ADD CONSTRAINT UQ_Assessment_Name UNIQUE (CourseId, Name);
            END
            """),

        ("Prerequisite.GroupNo", """
            IF OBJECT_ID(N'dbo.Prerequisite', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Prerequisite', N'GroupNo') IS NULL
                ALTER TABLE dbo.Prerequisite ADD GroupNo INT NOT NULL
                    CONSTRAINT DF_Prerequisite_GroupNo DEFAULT (0);
            """),

        ("Curriculum.DisplayOrder", """
            IF OBJECT_ID(N'dbo.Curriculum', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Curriculum', N'DisplayOrder') IS NULL
                ALTER TABLE dbo.Curriculum ADD DisplayOrder INT NOT NULL
                    CONSTRAINT DF_Curriculum_DisplayOrder DEFAULT (0);
            """),

        // OTP101 sits in Kỳ 0, so the original "TermNo >= 1" rejects real data.
        ("Curriculum term check allows Kỳ 0", """
            IF EXISTS (SELECT 1 FROM sys.check_constraints
                       WHERE name = N'CK_Curriculum_Term'
                         AND parent_object_id = OBJECT_ID(N'dbo.Curriculum'))
                ALTER TABLE dbo.Curriculum DROP CONSTRAINT CK_Curriculum_Term;

            IF OBJECT_ID(N'dbo.Curriculum', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                               WHERE name = N'CK_Curriculum_Term'
                                 AND parent_object_id = OBJECT_ID(N'dbo.Curriculum'))
                ALTER TABLE dbo.Curriculum
                    ADD CONSTRAINT CK_Curriculum_Term CHECK (TermNo >= 0);
            """),

        ("Term table", """
            IF OBJECT_ID(N'dbo.Term', N'U') IS NULL
                CREATE TABLE dbo.Term
                (
                    TermId      INT           IDENTITY(1,1) NOT NULL,
                    TermNo      INT           NOT NULL,
                    TermName    NVARCHAR(50)  NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive    BIT           NOT NULL CONSTRAINT DF_Term_IsActive DEFAULT (1),
                    CONSTRAINT PK_Term      PRIMARY KEY (TermId),
                    CONSTRAINT UQ_Term_No   UNIQUE (TermNo),
                    CONSTRAINT CK_Term_No   CHECK (TermNo >= 0)
                );
            """),

        // Kỳ 0..9 covers every FLM programme. Insert only what is missing so a
        // renamed or edited kỳ is never overwritten.
        ("Term seed rows", """
            IF OBJECT_ID(N'dbo.Term', N'U') IS NOT NULL
                INSERT INTO dbo.Term (TermNo, TermName, Description, IsActive)
                SELECT s.TermNo, s.TermName, s.Description, 1
                FROM (VALUES
                    (0, N'Kỳ 0 (Định hướng)', N'Định hướng và Rèn luyện tập trung'),
                    (1, N'Kỳ 1', NULL), (2, N'Kỳ 2', NULL), (3, N'Kỳ 3', NULL),
                    (4, N'Kỳ 4', NULL), (5, N'Kỳ 5', NULL), (6, N'Kỳ 6', NULL),
                    (7, N'Kỳ 7', NULL), (8, N'Kỳ 8', NULL), (9, N'Kỳ 9', NULL)
                ) AS s (TermNo, TermName, Description)
                WHERE NOT EXISTS (SELECT 1 FROM dbo.Term t WHERE t.TermNo = s.TermNo);
            """),

        // Added only once every existing Curriculum row has a matching Term,
        // otherwise the constraint cannot be validated and the whole startup
        // upgrade fails on a database that already holds data.
        ("Curriculum -> Term foreign key", """
            IF OBJECT_ID(N'dbo.Curriculum', N'U') IS NOT NULL
               AND OBJECT_ID(N'dbo.Term', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                               WHERE name = N'FK_Curriculum_Term')
               AND NOT EXISTS (SELECT 1 FROM dbo.Curriculum c
                               WHERE NOT EXISTS (SELECT 1 FROM dbo.Term t WHERE t.TermNo = c.TermNo))
                ALTER TABLE dbo.Curriculum
                    ADD CONSTRAINT FK_Curriculum_Term
                        FOREIGN KEY (TermNo) REFERENCES dbo.Term (TermNo);
            """),

        ("SubjectMaterial table", """
            IF OBJECT_ID(N'dbo.SubjectMaterial', N'U') IS NULL
                CREATE TABLE dbo.SubjectMaterial
                (
                    SubjectMaterialId INT            IDENTITY(1,1) NOT NULL,
                    CourseId          INT            NOT NULL,
                    Title             NVARCHAR(500)  NOT NULL,
                    Description       NVARCHAR(MAX)  NULL,
                    Url               NVARCHAR(500)  NULL,
                    Author            NVARCHAR(200)  NULL,
                    Publisher         NVARCHAR(200)  NULL,
                    Isbn              NVARCHAR(50)   NULL,
                    DisplayOrder      INT            NOT NULL CONSTRAINT DF_SubjectMaterial_Order  DEFAULT (0),
                    IsActive          BIT            NOT NULL CONSTRAINT DF_SubjectMaterial_Active DEFAULT (1),
                    CONSTRAINT PK_SubjectMaterial        PRIMARY KEY (SubjectMaterialId),
                    CONSTRAINT UQ_SubjectMaterial_Title  UNIQUE (CourseId, Title),
                    CONSTRAINT FK_SubjectMaterial_Course FOREIGN KEY (CourseId)
                        REFERENCES dbo.Course (CourseId) ON DELETE CASCADE
                );
            """),

        ("AssessmentSchedule table", """
            IF OBJECT_ID(N'dbo.AssessmentSchedule', N'U') IS NULL
                CREATE TABLE dbo.AssessmentSchedule
                (
                    AssessmentScheduleId INT           IDENTITY(1,1) NOT NULL,
                    CourseId             INT           NOT NULL,
                    SessionNo            INT           NOT NULL,
                    WeekNo               INT           NULL,
                    Title                NVARCHAR(500) NOT NULL,
                    Description          NVARCHAR(MAX) NULL,
                    ExpectedDate         DATE          NULL,
                    TeachingType         NVARCHAR(100) NULL,
                    AssessmentId         INT           NULL,
                    CONSTRAINT PK_AssessmentSchedule            PRIMARY KEY (AssessmentScheduleId),
                    CONSTRAINT UQ_AssessmentSchedule_Session    UNIQUE (CourseId, SessionNo),
                    CONSTRAINT CK_AssessmentSchedule_Session    CHECK (SessionNo >= 1),
                    CONSTRAINT FK_AssessmentSchedule_Course     FOREIGN KEY (CourseId)
                        REFERENCES dbo.Course (CourseId) ON DELETE CASCADE,
                    -- NO ACTION: Course already cascades into both this table and
                    -- Assessment, and SQL Server forbids two cascade paths.
                    CONSTRAINT FK_AssessmentSchedule_Assessment FOREIGN KEY (AssessmentId)
                        REFERENCES dbo.Assessment (AssessmentId) ON DELETE NO ACTION
                );
            """),

        ("GradePrediction table", """
            IF OBJECT_ID(N'dbo.GradePrediction', N'U') IS NULL
                CREATE TABLE dbo.GradePrediction
                (
                    GradePredictionId      INT           IDENTITY(1,1) NOT NULL,
                    StudentId              INT           NOT NULL,
                    CurrentGpa             DECIMAL(4,2)  NULL,
                    PredictedGpa           DECIMAL(4,2)  NOT NULL,
                    RetakeCount            INT           NOT NULL,
                    BaseClassification     NVARCHAR(20)  NOT NULL,
                    AdjustedClassification NVARCHAR(20)  NOT NULL,
                    Note                   NVARCHAR(500) NULL,
                    CreatedAt              DATETIME2(0)  NOT NULL
                        CONSTRAINT DF_GradePrediction_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT PK_GradePrediction         PRIMARY KEY (GradePredictionId),
                    CONSTRAINT CK_GradePrediction_Gpa     CHECK (PredictedGpa >= 0 AND PredictedGpa <= 10),
                    CONSTRAINT CK_GradePrediction_Retakes CHECK (RetakeCount >= 0),
                    CONSTRAINT FK_GradePrediction_Student FOREIGN KEY (StudentId)
                        REFERENCES dbo.Student (StudentId) ON DELETE CASCADE
                );
            """),

        ("Catalog indexes", """
            IF OBJECT_ID(N'dbo.SubjectMaterial', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubjectMaterial_Course')
                CREATE INDEX IX_SubjectMaterial_Course
                    ON dbo.SubjectMaterial (CourseId, IsActive) INCLUDE (Title, Url);

            IF OBJECT_ID(N'dbo.AssessmentSchedule', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssessmentSchedule_Course')
                CREATE INDEX IX_AssessmentSchedule_Course
                    ON dbo.AssessmentSchedule (CourseId, SessionNo);

            IF OBJECT_ID(N'dbo.GradePrediction', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GradePrediction_Student')
                CREATE INDEX IX_GradePrediction_Student
                    ON dbo.GradePrediction (StudentId, CreatedAt DESC);

            IF OBJECT_ID(N'dbo.Curriculum', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Curriculum_Major_Order')
                CREATE INDEX IX_Curriculum_Major_Order
                    ON dbo.Curriculum (MajorId, TermNo, DisplayOrder) INCLUDE (CourseId, IsMandatory);

            IF OBJECT_ID(N'dbo.Prerequisite', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Prerequisite_Group')
                CREATE INDEX IX_Prerequisite_Group
                    ON dbo.Prerequisite (CourseId, GroupNo) INCLUDE (RequiredCourseId);
            """)
    ];

    /// <summary>
    /// Applies every pending step. Safe to call on every startup.
    ///
    /// Does nothing at all on a non-relational provider, which is what makes
    /// the in-memory unit tests unaffected by this file.
    /// </summary>
    /// <returns>The names of the steps that were executed.</returns>
    public static IReadOnlyList<string> Upgrade(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Database.IsSqlServer())
        {
            return [];
        }

        var applied = new List<string>();
        var failures = new List<Exception>();

        foreach (var (name, sql) in Steps)
        {
            try
            {
                context.Database.ExecuteSqlRaw(sql);
                applied.Add(name);
            }
            catch (Exception ex)
            {
                // Record and CARRY ON rather than abort. The steps are
                // independent guarded batches, so letting the first failure
                // stop the loop would leave every later change unapplied - one
                // odd permission on one table would block the whole upgrade.
                //
                // The step name is in the message on purpose: a bare SQL error
                // gives no way to tell which change broke without bisecting
                // this file by hand.
                failures.Add(new InvalidOperationException(
                    $"Schema upgrade step '{name}' failed: {ex.Message}", ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more schema upgrade steps failed. The database may be partially upgraded; " +
                "re-running is safe because every step is guarded.",
                failures);
        }

        return applied;
    }
}
