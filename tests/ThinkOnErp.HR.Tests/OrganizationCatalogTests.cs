using System;
using ThinkOnErp.Domain.Entities.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class OrganizationCatalogTests
{
    [Fact]
    public void Code_ValidString_TrimsAndConvertsToUppercase()
    {
        var result = OrganizationCatalogRules.Code("  dept_sales_01  ");
        Assert.Equal("DEPT_SALES_01", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Code_NullOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Code(input!));
    }

    [Fact]
    public void Code_Exceeding50Characters_ThrowsArgumentException()
    {
        var longCode = new string('A', 51);
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Code(longCode));
        Assert.Contains("must not exceed 50 characters", ex.Message);
    }

    [Fact]
    public void OptionalCode_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(OrganizationCatalogRules.OptionalCode(null));
        Assert.Null(OrganizationCatalogRules.OptionalCode(""));
        Assert.Null(OrganizationCatalogRules.OptionalCode("   \t\n"));
    }

    [Fact]
    public void OptionalCode_ValidString_NormalizesAndReturnsUpper()
    {
        Assert.Equal("PARENT_DEPT", OrganizationCatalogRules.OptionalCode("  parent_dept  "));
    }

    [Fact]
    public void Text_ValidString_ReturnsTrimmed()
    {
        var result = OrganizationCatalogRules.Text("  Engineering  ", 100, "Title");
        Assert.Equal("Engineering", result);
    }

    [Fact]
    public void Text_ExceedingMax_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Text("TooLongText", 5, "ShortField"));
        Assert.Contains("ShortField is required and must not exceed 5 characters", ex.Message);
    }

    [Fact]
    public void Normalize_DepartmentWrite_ValidInput_NormalizesFields()
    {
        var input = new DepartmentWrite("  المبيعات  ", "  Sales  ", 5L, "  parent_corp  ", true);
        var normalized = OrganizationCatalogRules.Normalize(input);

        Assert.Equal("المبيعات", normalized.NameLocal);
        Assert.Equal("Sales", normalized.NameEn);
        Assert.Equal(5L, normalized.BranchId);
        Assert.Equal("PARENT_CORP", normalized.ParentDepartmentCode);
        Assert.True(normalized.IsActive);
    }

    [Fact]
    public void Normalize_DepartmentWrite_NonPositiveBranchId_ThrowsArgumentException()
    {
        var input = new DepartmentWrite("المبيعات", "Sales", 0L, null, true);
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Normalize(input));
        Assert.Contains("BranchId must be positive", ex.Message);

        var inputNegative = new DepartmentWrite("المبيعات", "Sales", -1L, null, true);
        Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Normalize(inputNegative));
    }

    [Fact]
    public void Normalize_DepartmentWrite_NullBranchId_Allowed()
    {
        var input = new DepartmentWrite("المركز الرئيسي", "Headquarters", null, null, true);
        var normalized = OrganizationCatalogRules.Normalize(input);
        Assert.Null(normalized.BranchId);
    }

    [Fact]
    public void Normalize_PositionWrite_ValidInput_NormalizesFields()
    {
        var input = new PositionWrite("  dept_it  ", "  مهندس برمجيات  ", "  Software Engineer  ", "  lead_dev  ", 3, true);
        var normalized = OrganizationCatalogRules.Normalize(input);

        Assert.Equal("DEPT_IT", normalized.DepartmentCode);
        Assert.Equal("مهندس برمجيات", normalized.TitleLocal);
        Assert.Equal("Software Engineer", normalized.TitleEn);
        Assert.Equal("LEAD_DEV", normalized.ReportsToPositionCode);
        Assert.Equal(3, normalized.Headcount);
        Assert.True(normalized.IsActive);
    }

    [Fact]
    public void Normalize_PositionWrite_NegativeHeadcount_ThrowsArgumentException()
    {
        var input = new PositionWrite("DEPT_IT", "مهندس", "Engineer", null, -1, true);
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Normalize(input));
        Assert.Contains("Headcount must be nonnegative", ex.Message);
    }

    [Fact]
    public void ExpectedVersion_Valid64HexChar_ReturnsCleanHex()
    {
        var validHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        var withQuotes = $"\"{validHash}\"";

        Assert.Equal(validHash, OrganizationCatalogRules.ExpectedVersion(validHash));
        Assert.Equal(validHash, OrganizationCatalogRules.ExpectedVersion(withQuotes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-hex")]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B85")] // 63 chars
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855F")] // 65 chars
    [InlineData("G3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")] // G is non-hex
    public void ExpectedVersion_InvalidInput_ThrowsArgumentException(string? input)
    {
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.ExpectedVersion(input));
        Assert.Contains("A single current If-Match version is required", ex.Message);
    }

    [Fact]
    public void Version_Department_DeterministicAndReflectsFieldChanges()
    {
        var dept1 = new HrDepartment
        {
            DepartmentCode = "DEP_FIN",
            BranchId = 1L,
            NameLocal = "المالية",
            NameEn = "Finance",
            CostCenterCode = "CC01",
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        var dept2 = new HrDepartment
        {
            DepartmentCode = "DEP_FIN",
            BranchId = 1L,
            NameLocal = "المالية",
            NameEn = "Finance",
            CostCenterCode = "CC01",
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        var v1 = OrganizationCatalogRules.Version(dept1);
        var v2 = OrganizationCatalogRules.Version(dept2);

        Assert.Equal(64, v1.Length);
        Assert.Equal(v1, v2);

        // Modifying any property produces a distinct version
        dept2.NameEn = "Financial Affairs";
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(dept2));

        dept2.NameEn = "Finance";
        dept2.IsActive = false;
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(dept2));

        dept2.IsActive = true;
        dept2.UpdateDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        dept2.UpdateUser = "EDITOR";
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(dept2));
    }

    [Fact]
    public void Version_Position_DeterministicAndReflectsFieldChanges()
    {
        var pos1 = new HrPosition
        {
            PositionCode = "POS_MGR",
            DepartmentCode = "DEP_FIN",
            TitleLocal = "مدير مالي",
            TitleEn = "Finance Manager",
            Headcount = 1,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        var pos2 = new HrPosition
        {
            PositionCode = "POS_MGR",
            DepartmentCode = "DEP_FIN",
            TitleLocal = "مدير مالي",
            TitleEn = "Finance Manager",
            Headcount = 1,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        var v1 = OrganizationCatalogRules.Version(pos1);
        var v2 = OrganizationCatalogRules.Version(pos2);

        Assert.Equal(64, v1.Length);
        Assert.Equal(v1, v2);

        pos2.Headcount = 2;
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(pos2));

        pos2.Headcount = 1;
        pos2.ReportsToPositionCode = "POS_CFO";
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(pos2));

        pos2.ReportsToPositionCode = null;
        pos2.JobGradeCode = "GRADE_A";
        Assert.NotEqual(v1, OrganizationCatalogRules.Version(pos2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmployeeHierarchyRules_Validate_NullOrWhitespaceManager_Succeeds(string? managerCode)
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        // Should not throw
        EmployeeHierarchyRules.Validate("EMP01", managerCode, managers);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_DirectSelfCycle_ThrowsArgumentException()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMP01"] = "EMP01"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP01", "EMP01", managers));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_TwoNodeCycle_ThrowsArgumentException()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMP01"] = "EMP02",
            ["EMP02"] = "EMP01"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP01", "EMP02", managers));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_MultiNodeCycle_ThrowsArgumentException()
    {
        // Cycle: A -> B -> C -> D -> B
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "D",
            ["D"] = "B"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", managers));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_CycleIsCaseInsensitive_ThrowsArgumentException()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["emp_a"] = "EMP_B",
            ["emp_b"] = "EMP_A"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP_A", "emp_b", managers));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_MissingOrForeignManagerInChain_ThrowsArgumentException()
    {
        // A -> B -> FOREIGN_C (not in dictionary)
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "FOREIGN_C"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", managers));
        Assert.Equal("Reporting chain contains an unavailable or foreign-company employee.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_DeepValidChain_Succeeds()
    {
        // Chain: E1 -> E2 -> E3 -> ... -> E10 -> null
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < 10; i++)
        {
            managers[$"E{i}"] = $"E{i + 1}";
        }
        managers["E10"] = null;

        // Validating from E1 up the chain to E10 (root) should succeed cleanly
        EmployeeHierarchyRules.Validate("E1", "E2", managers);
    }

    [Fact]
    public void Normalize_DepartmentWrite_MaxTextLength_Validation()
    {
        var longLocal = new string('ع', 201);
        var exLocal = Assert.Throws<ArgumentException>(() =>
            OrganizationCatalogRules.Normalize(new DepartmentWrite(longLocal, "ValidEn", 1L, null, true)));
        Assert.Contains("NameLocal is required and must not exceed 200 characters", exLocal.Message);

        var longEn = new string('A', 201);
        var exEn = Assert.Throws<ArgumentException>(() =>
            OrganizationCatalogRules.Normalize(new DepartmentWrite("اسم صالح", longEn, 1L, null, true)));
        Assert.Contains("NameEn is required and must not exceed 200 characters", exEn.Message);
    }

    [Fact]
    public void Normalize_PositionWrite_MaxTextLength_Validation()
    {
        var longLocal = new string('م', 201);
        var exLocal = Assert.Throws<ArgumentException>(() =>
            OrganizationCatalogRules.Normalize(new PositionWrite("DEPT", longLocal, "ValidEn", null, 1, true)));
        Assert.Contains("TitleLocal is required and must not exceed 200 characters", exLocal.Message);

        var longEn = new string('E', 201);
        var exEn = Assert.Throws<ArgumentException>(() =>
            OrganizationCatalogRules.Normalize(new PositionWrite("DEPT", "مسمى صالح", longEn, null, 1, true)));
        Assert.Contains("TitleEn is required and must not exceed 200 characters", exEn.Message);

        var longDept = new string('D', 51);
        var exDept = Assert.Throws<ArgumentException>(() =>
            OrganizationCatalogRules.Normalize(new PositionWrite(longDept, "مسمى", "Title", null, 1, true)));
        Assert.Contains("Code is required and must not exceed 50 characters", exDept.Message);
    }

    [Fact]
    public void OrganizationVersionConflictException_HasExpectedMessage()
    {
        var ex = new OrganizationVersionConflictException();
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Equal("Organization version changed. Read it again before editing.", ex.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_TreeStructureWithMultipleBranchesReportingToSameRoot_Succeeds()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CEO"] = null,
            ["MGR_SALES"] = "CEO",
            ["MGR_ENG"] = "CEO",
            ["DEV1"] = "MGR_ENG",
            ["DEV2"] = "MGR_ENG",
            ["SALES1"] = "MGR_SALES"
        };

        // All reporting relationships in the tree should validate without cycle or error
        EmployeeHierarchyRules.Validate("DEV1", "MGR_ENG", managers);
        EmployeeHierarchyRules.Validate("DEV2", "MGR_ENG", managers);
        EmployeeHierarchyRules.Validate("SALES1", "MGR_SALES", managers);
        EmployeeHierarchyRules.Validate("MGR_ENG", "CEO", managers);
        EmployeeHierarchyRules.Validate("MGR_SALES", "CEO", managers);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_CycleAtEndOfChain_ThrowsArgumentException()
    {
        // Chain: A -> B -> C -> D -> E -> C (cycle between C, D, E)
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "D",
            ["D"] = "E",
            ["E"] = "C"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", managers));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void OrganizationCatalogRules_Version_NullOptionalFieldsDoNotThrow()
    {
        var dept = new HrDepartment
        {
            DepartmentCode = "MINIMAL_DEPT",
            NameLocal = "قسم تجريبي",
            NameEn = "Test Dept",
            BranchId = null,
            ParentDepartmentCode = null,
            CostCenterCode = null,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "SYSTEM",
            UpdateDate = null,
            UpdateUser = null
        };

        var deptVersion = OrganizationCatalogRules.Version(dept);
        Assert.Equal(64, deptVersion.Length);
        Assert.True(deptVersion.All(Uri.IsHexDigit));

        var pos = new HrPosition
        {
            PositionCode = "MINIMAL_POS",
            DepartmentCode = "MINIMAL_DEPT",
            TitleLocal = "وظيفة تجريبية",
            TitleEn = "Test Position",
            ReportsToPositionCode = null,
            JobGradeCode = null,
            Headcount = 0,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "SYSTEM",
            UpdateDate = null,
            UpdateUser = null
        };

        var posVersion = OrganizationCatalogRules.Version(pos);
        Assert.Equal(64, posVersion.Length);
        Assert.True(posVersion.All(Uri.IsHexDigit));
    }

    [Theory]
    [InlineData("  dept-hr  ", "DEPT-HR")]
    [InlineData("finance", "FINANCE")]
    [InlineData("ENG_01", "ENG_01")]
    [InlineData("ops_branch_2", "OPS_BRANCH_2")]
    public void Code_Theory_VariousValidCodes_TrimsAndConvertsToUpper(string input, string expected)
    {
        Assert.Equal(expected, OrganizationCatalogRules.Code(input));
    }

    [Fact]
    public void Code_Exact50Characters_ReturnsNormalizedCode()
    {
        var code50 = new string('X', 50);
        var result = OrganizationCatalogRules.Code(code50);
        Assert.Equal(code50, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Text_Theory_NullEmptyWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Text(input, 100, "FieldName"));
    }

    [Fact]
    public void Text_ExactMaxCharacters_Succeeds()
    {
        var maxText = new string('T', 20);
        var result = OrganizationCatalogRules.Text(maxText, 20, "Field");
        Assert.Equal(maxText, result);
    }

    [Fact]
    public void Text_ExceedingMaxByOneChar_ThrowsArgumentException()
    {
        var overText = new string('T', 21);
        var ex = Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.Text(overText, 20, "TestField"));
        Assert.Contains("TestField is required and must not exceed 20 characters", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void OptionalCode_Theory_WhitespaceVariants_ReturnsNull(string? input)
    {
        Assert.Null(OrganizationCatalogRules.OptionalCode(input));
    }

    [Fact]
    public void Normalize_DepartmentWrite_ParentCode_WhitespaceBecomesNull()
    {
        var input = new DepartmentWrite("قسم", "Dept", 1L, "   ", true);
        var normalized = OrganizationCatalogRules.Normalize(input);
        Assert.Null(normalized.ParentDepartmentCode);
    }

    [Fact]
    public void Normalize_PositionWrite_ReportsToCode_WhitespaceBecomesNull()
    {
        var input = new PositionWrite("DEPT", "مسمى", "Title", "   ", 2, true);
        var normalized = OrganizationCatalogRules.Normalize(input);
        Assert.Null(normalized.ReportsToPositionCode);
    }

    [Fact]
    public void Normalize_PositionWrite_ZeroHeadcount_Succeeds()
    {
        var input = new PositionWrite("DEPT", "مسمى", "Title", null, 0, true);
        var normalized = OrganizationCatalogRules.Normalize(input);
        Assert.Equal(0, normalized.Headcount);
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("\"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF\"")]
    [InlineData("  \"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"  ")]
    public void ExpectedVersion_Theory_ValidVersionsWithQuotesAndTrimming(string input)
    {
        var clean = OrganizationCatalogRules.ExpectedVersion(input);
        Assert.Equal(64, clean.Length);
        Assert.True(clean.All(Uri.IsHexDigit));
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDE")] // 63 chars
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEFA")] // 65 chars
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEZ")] // Z non-hex
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDE!")] // ! non-hex
    public void ExpectedVersion_Theory_InvalidFormats_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => OrganizationCatalogRules.ExpectedVersion(input));
    }

    [Fact]
    public void Version_Department_AllPropertiesAffectHash()
    {
        var baseDept = new HrDepartment
        {
            DepartmentCode = "DEP_OPS",
            BranchId = 1L,
            NameLocal = "العمليات",
            NameEn = "Operations",
            ParentDepartmentCode = "DEP_CORP",
            CostCenterCode = "CC_OPS",
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "SYSTEM",
            UpdateDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdateUser = "ADMIN"
        };

        var baseHash = OrganizationCatalogRules.Version(baseDept);

        // BranchId mutation
        var d1 = new HrDepartment { DepartmentCode = baseDept.DepartmentCode, BranchId = 2L, NameLocal = baseDept.NameLocal, NameEn = baseDept.NameEn, ParentDepartmentCode = baseDept.ParentDepartmentCode, CostCenterCode = baseDept.CostCenterCode, IsActive = baseDept.IsActive, CreationDate = baseDept.CreationDate, CreationUser = baseDept.CreationUser, UpdateDate = baseDept.UpdateDate, UpdateUser = baseDept.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(d1));

        // ParentDepartmentCode mutation
        var d2 = new HrDepartment { DepartmentCode = baseDept.DepartmentCode, BranchId = baseDept.BranchId, NameLocal = baseDept.NameLocal, NameEn = baseDept.NameEn, ParentDepartmentCode = "DEP_OTHER", CostCenterCode = baseDept.CostCenterCode, IsActive = baseDept.IsActive, CreationDate = baseDept.CreationDate, CreationUser = baseDept.CreationUser, UpdateDate = baseDept.UpdateDate, UpdateUser = baseDept.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(d2));

        // CostCenterCode mutation
        var d3 = new HrDepartment { DepartmentCode = baseDept.DepartmentCode, BranchId = baseDept.BranchId, NameLocal = baseDept.NameLocal, NameEn = baseDept.NameEn, ParentDepartmentCode = baseDept.ParentDepartmentCode, CostCenterCode = "CC_DIFF", IsActive = baseDept.IsActive, CreationDate = baseDept.CreationDate, CreationUser = baseDept.CreationUser, UpdateDate = baseDept.UpdateDate, UpdateUser = baseDept.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(d3));

        // CreationUser mutation
        var d4 = new HrDepartment { DepartmentCode = baseDept.DepartmentCode, BranchId = baseDept.BranchId, NameLocal = baseDept.NameLocal, NameEn = baseDept.NameEn, ParentDepartmentCode = baseDept.ParentDepartmentCode, CostCenterCode = baseDept.CostCenterCode, IsActive = baseDept.IsActive, CreationDate = baseDept.CreationDate, CreationUser = "OTHER_USER", UpdateDate = baseDept.UpdateDate, UpdateUser = baseDept.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(d4));
    }

    [Fact]
    public void Version_Position_AllPropertiesAffectHash()
    {
        var basePos = new HrPosition
        {
            PositionCode = "POS_LEAD",
            DepartmentCode = "DEP_OPS",
            TitleLocal = "مشرف عمليات",
            TitleEn = "Operations Lead",
            ReportsToPositionCode = "POS_MGR",
            JobGradeCode = "GRADE_3",
            Headcount = 2,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "SYSTEM",
            UpdateDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdateUser = "ADMIN"
        };

        var baseHash = OrganizationCatalogRules.Version(basePos);

        // JobGradeCode mutation
        var p1 = new HrPosition { PositionCode = basePos.PositionCode, DepartmentCode = basePos.DepartmentCode, TitleLocal = basePos.TitleLocal, TitleEn = basePos.TitleEn, ReportsToPositionCode = basePos.ReportsToPositionCode, JobGradeCode = "GRADE_4", Headcount = basePos.Headcount, IsActive = basePos.IsActive, CreationDate = basePos.CreationDate, CreationUser = basePos.CreationUser, UpdateDate = basePos.UpdateDate, UpdateUser = basePos.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(p1));

        // ReportsToPositionCode mutation
        var p2 = new HrPosition { PositionCode = basePos.PositionCode, DepartmentCode = basePos.DepartmentCode, TitleLocal = basePos.TitleLocal, TitleEn = basePos.TitleEn, ReportsToPositionCode = "POS_VP", JobGradeCode = basePos.JobGradeCode, Headcount = basePos.Headcount, IsActive = basePos.IsActive, CreationDate = basePos.CreationDate, CreationUser = basePos.CreationUser, UpdateDate = basePos.UpdateDate, UpdateUser = basePos.UpdateUser };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(p2));

        // UpdateUser mutation
        var p3 = new HrPosition { PositionCode = basePos.PositionCode, DepartmentCode = basePos.DepartmentCode, TitleLocal = basePos.TitleLocal, TitleEn = basePos.TitleEn, ReportsToPositionCode = basePos.ReportsToPositionCode, JobGradeCode = basePos.JobGradeCode, Headcount = basePos.Headcount, IsActive = basePos.IsActive, CreationDate = basePos.CreationDate, CreationUser = basePos.CreationUser, UpdateDate = basePos.UpdateDate, UpdateUser = "SUPERVISOR" };
        Assert.NotEqual(baseHash, OrganizationCatalogRules.Version(p3));
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_ThreeAndFourNodeCycles_ThrowsArgumentException()
    {
        // 3-node cycle: A -> B -> C -> A
        var managers3 = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "A"
        };
        var ex3 = Assert.Throws<ArgumentException>(() => EmployeeHierarchyRules.Validate("A", "B", managers3));
        Assert.Equal("Reporting relationship contains a cycle.", ex3.Message);

        // 4-node cycle: A -> B -> C -> D -> A
        var managers4 = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "D",
            ["D"] = "A"
        };
        var ex4 = Assert.Throws<ArgumentException>(() => EmployeeHierarchyRules.Validate("A", "B", managers4));
        Assert.Equal("Reporting relationship contains a cycle.", ex4.Message);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_VeryDeepChain100Levels_SucceedsWithoutStackOverflow()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < 100; i++)
        {
            managers[$"EMP_{i}"] = $"EMP_{i + 1}";
        }
        managers["EMP_100"] = null;

        EmployeeHierarchyRules.Validate("EMP_1", "EMP_2", managers);
    }

    [Fact]
    public void EmployeeHierarchyRules_Validate_DiamondHierarchy_Succeeds()
    {
        var managers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CEO"] = null,
            ["DIR_1"] = "CEO",
            ["DIR_2"] = "CEO",
            ["LEAD_1"] = "DIR_1",
            ["LEAD_2"] = "DIR_2",
            ["DEV"] = "LEAD_1"
        };

        EmployeeHierarchyRules.Validate("DEV", "LEAD_1", managers);
        EmployeeHierarchyRules.Validate("LEAD_1", "DIR_1", managers);
        EmployeeHierarchyRules.Validate("LEAD_2", "DIR_2", managers);
        EmployeeHierarchyRules.Validate("DIR_1", "CEO", managers);
        EmployeeHierarchyRules.Validate("DIR_2", "CEO", managers);
    }

    [Fact]
    public void HrDepartment_VersionProperty_MatchesStaticRulesVersion()
    {
        var dept = new HrDepartment
        {
            DepartmentCode = "DEP_FIN",
            BranchId = 1L,
            NameLocal = "المالية",
            NameEn = "Finance",
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        Assert.Equal(OrganizationCatalogRules.Version(dept), dept.Version);
    }

    [Fact]
    public void HrPosition_VersionProperty_MatchesStaticRulesVersion()
    {
        var pos = new HrPosition
        {
            PositionCode = "POS_MGR",
            DepartmentCode = "DEP_FIN",
            TitleLocal = "مدير مالي",
            TitleEn = "Finance Manager",
            Headcount = 1,
            IsActive = true,
            CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreationUser = "ADMIN"
        };

        Assert.Equal(OrganizationCatalogRules.Version(pos), pos.Version);
    }
}

