$jsonPath = Join-Path $PSScriptRoot "data\BIT_SE_K19D_K20A.json"
$targetSql = Join-Path $PSScriptRoot "02_seed_master.sql"

$json = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

$sb = [System.Text.StringBuilder]::new()

[void]$sb.AppendLine("/* =============================================================================")
[void]$sb.AppendLine("   FAT - FPT Academic Tracker")
[void]$sb.AppendLine("   02_seed_master.sql : reference data (Role, GradeScale, Major, Semester,")
[void]$sb.AppendLine("                         Course, Assessment, Prerequisite, Curriculum).")
[void]$sb.AppendLine("   AUTO-GENERATED EXCLUSIVELY FROM db/data/BIT_SE_K19D_K20A.json")
[void]$sb.AppendLine("   ============================================================================= */")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("SET NOCOUNT ON;")
[void]$sb.AppendLine("SET ANSI_NULLS ON;")
[void]$sb.AppendLine("SET QUOTED_IDENTIFIER ON;")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("USE FAT_DB;")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("BEGIN TRANSACTION;")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Role")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
[void]$sb.AppendLine("INSERT INTO dbo.Role (RoleName, Description) VALUES")
[void]$sb.AppendLine("    (N'Admin',   N'Manages the course catalog, curricula and user accounts'),")
[void]$sb.AppendLine("    (N'Student', N'Views and maintains their own academic record');")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   GradeScale")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
[void]$sb.AppendLine("INSERT INTO dbo.GradeScale (MinScore, MaxScore, LetterGrade, GradePoint, Description) VALUES")
[void]$sb.AppendLine("    (8.50, 10.01, N'A',  4.00, N'Very good'),")
[void]$sb.AppendLine("    (8.00,  8.50, N'B+', 3.50, N'Good plus'),")
[void]$sb.AppendLine("    (7.00,  8.00, N'B',  3.00, N'Good'),")
[void]$sb.AppendLine("    (6.50,  7.00, N'C+', 2.50, N'Fairly good'),")
[void]$sb.AppendLine("    (5.50,  6.50, N'C',  2.00, N'Average'),")
[void]$sb.AppendLine("    (5.00,  5.50, N'D+', 1.50, N'Below average'),")
[void]$sb.AppendLine("    (4.00,  5.00, N'D',  1.00, N'Weak - not a pass'),")
[void]$sb.AppendLine("    (0.00,  4.00, N'F',  0.00, N'Fail');")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Major")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
$totalCredits = $json.curriculum_info.total_credits
[void]$sb.AppendLine("INSERT INTO dbo.Major (MajorCode, MajorName, RequiredCredits, TotalTerms) VALUES")
[void]$sb.AppendLine("    (N'SE', N'Software Engineering', $totalCredits, 9);")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Semester")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
[void]$sb.AppendLine("INSERT INTO dbo.Semester (SemesterCode, SemesterName, StartDate, EndDate, DisplayOrder, IsCurrent) VALUES")
[void]$sb.AppendLine("    (N'SP24', N'Spring 2024', '2024-01-08', '2024-04-28',  1, 0),")
[void]$sb.AppendLine("    (N'SU24', N'Summer 2024', '2024-05-06', '2024-08-25',  2, 0),")
[void]$sb.AppendLine("    (N'FA24', N'Fall 2024',   '2024-09-02', '2024-12-22',  3, 0),")
[void]$sb.AppendLine("    (N'SP25', N'Spring 2025', '2025-01-06', '2025-04-27',  4, 0),")
[void]$sb.AppendLine("    (N'SU25', N'Summer 2025', '2025-05-05', '2025-08-24',  5, 0),")
[void]$sb.AppendLine("    (N'FA25', N'Fall 2025',   '2025-09-01', '2025-12-21',  6, 0),")
[void]$sb.AppendLine("    (N'SP26', N'Spring 2026', '2026-01-05', '2026-04-26',  7, 0),")
[void]$sb.AppendLine("    (N'SU26', N'Summer 2026', '2026-05-04', '2026-08-23',  8, 1),")
[void]$sb.AppendLine("    (N'FA26', N'Fall 2026',   '2026-08-31', '2026-12-20',  9, 0),")
[void]$sb.AppendLine("    (N'SP27', N'Spring 2027', '2027-01-04', '2027-04-25', 10, 0);")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Course - parsed from BIT_SE_K19D_K20A.json")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
[void]$sb.AppendLine("INSERT INTO dbo.Course (CourseCode, CourseName, Credits, Description) VALUES")

$courseRows = @()
foreach ($s in $json.subjects) {
    $code = $s.code.Trim()
    $name = if ($s.name_vi) { $s.name_vi.Trim() } else { $s.name_en.Trim() }
    $nameEscaped = $name.Replace("'", "''")
    $cred = $s.credit
    $descEscaped = $nameEscaped
    $courseRows += "    (N'$code', N'$nameEscaped', $cred, N'$descEscaped')"
}
[void]$sb.AppendLine(($courseRows -join ",`n"))
[void]$sb.AppendLine(";")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")

[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Assessment - parsed from BIT_SE_K19D_K20A.json")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")

$assessRows = @()
$seenKeys = [System.Collections.Generic.HashSet[string]]::new()

foreach ($s in $json.subjects) {
    $code = $s.code.Trim()
    if ($s.assessment_plan) {
        $order = 1
        foreach ($a in $s.assessment_plan) {
            $cat = $a.category.Trim()
            $key = "$code|$cat".ToLowerInvariant()
            if ($seenKeys.Add($key)) {
                $catEsc = $cat.Replace("'", "''")
                $weight = [Math]::Round([decimal]($a.weight_percent) / 100.0, 4)
                $minPass = "NULL"
                if ($a.completion_criteria -and $a.completion_criteria -match ">=?\s*([0-9\.]+)") {
                    $val = $Matches[1]
                    $minPass = [string][decimal]$val
                }
                $assessRows += "SELECT CourseId, N'$catEsc', $weight, $minPass, $order FROM dbo.Course WHERE CourseCode = N'$code'"
                $order++
            }
        }
    }
}

if ($assessRows.Count -gt 0) {
    [void]$sb.AppendLine("INSERT INTO dbo.Assessment (CourseId, Name, Weight, MinScoreToPass, DisplayOrder)")
    [void]$sb.AppendLine(($assessRows -join "`nUNION ALL`n"))
    [void]$sb.AppendLine(";")
    [void]$sb.AppendLine("GO")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Prerequisite - parsed from BIT_SE_K19D_K20A.json")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")

$prereqRows = @()
foreach ($s in $json.subjects) {
    $code = $s.code.Trim()
    if ($s.prerequisite -and $s.prerequisite -ne "None" -and $s.prerequisite -ne "null") {
        # Parse prereq codes separated by comma or space
        $rawPrereqs = $s.prerequisite -split "[\s,]+"
        foreach ($pCode in $rawPrereqs) {
            $cleanP = $pCode.Trim()
            if ($cleanP.Length -gt 2 -and $cleanP -ne "None") {
                $prereqRows += "SELECT c.CourseId, r.CourseId, N'Prerequisite' FROM dbo.Course c CROSS JOIN dbo.Course r WHERE c.CourseCode = N'$code' AND r.CourseCode = N'$cleanP'"
            }
        }
    }
}

if ($prereqRows.Count -gt 0) {
    [void]$sb.AppendLine("INSERT INTO dbo.Prerequisite (CourseId, RequiredCourseId, Type)")
    [void]$sb.AppendLine(($prereqRows -join "`nUNION ALL`n"))
    [void]$sb.AppendLine(";")
    [void]$sb.AppendLine("GO")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("/* -----------------------------------------------------------------------------")
[void]$sb.AppendLine("   Curriculum - parsed from BIT_SE_K19D_K20A.json")
[void]$sb.AppendLine("   ----------------------------------------------------------------------------- */")
[void]$sb.AppendLine("INSERT INTO dbo.Curriculum (MajorId, CourseId, TermNo, IsMandatory)")
[void]$sb.AppendLine("SELECT m.MajorId, c.CourseId, k.TermNo, 1")
[void]$sb.AppendLine("FROM (VALUES")

$currRows = @()
foreach ($s in $json.subjects) {
    $code = $s.code.Trim()
    $sem = [int]($s.semester)
    $currRows += "    (N'$code', $sem)"
}
[void]$sb.AppendLine(($currRows -join ",`n"))
[void]$sb.AppendLine(") AS k(CourseCode, TermNo)")
[void]$sb.AppendLine("JOIN dbo.Course c ON c.CourseCode = k.CourseCode")
[void]$sb.AppendLine("CROSS JOIN dbo.Major m")
[void]$sb.AppendLine("WHERE m.MajorCode = N'SE';")
[void]$sb.AppendLine("GO")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("COMMIT TRANSACTION;")
[void]$sb.AppendLine("GO")

Set-Content -Path $targetSql -Value $sb.ToString() -Encoding UTF8
Write-Host "Generated $targetSql successfully from $jsonPath!" -ForegroundColor Green
