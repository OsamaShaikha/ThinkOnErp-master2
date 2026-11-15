using ThinkOnErp.Domain.Entities.Hr;
using Xunit;
namespace ThinkOnErp.HR.Tests;
public class EmployeeHierarchyRulesTests
{
    [Theory] [InlineData("A")] [InlineData("B")] [InlineData("C")]
    public void RejectSelfAndIndirectCycles(string manager)
    {
        var graph = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase) { ["A"] = null, ["B"] = "C", ["C"] = "A" };
        Assert.Throws<ArgumentException>(() => EmployeeHierarchyRules.Validate("a", manager, graph));
    }
    [Fact] public void RejectPreexistingCycleAndUnavailableAncestor()
    {
        Assert.Throws<ArgumentException>(() => EmployeeHierarchyRules.Validate("A", "B", new Dictionary<string,string?> { ["B"] = "C", ["C"] = "B" }));
        Assert.Throws<ArgumentException>(() => EmployeeHierarchyRules.Validate("A", "B", new Dictionary<string,string?> { ["B"] = "FOREIGN" }));
    }
    [Fact] public void AcceptTerminatingChainWithoutImposingHierarchyDepth()
    {
        var graph = new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
        for (var i=0; i<300; i++) graph["M"+i] = i==299 ? null : "M"+(i+1);
        EmployeeHierarchyRules.Validate("A", "M0", graph); EmployeeHierarchyRules.Validate("A", null, graph);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Validate_NullOrWhitespaceManagerCode_AlwaysSucceeds(string? managerCode)
    {
        var emptyGraph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        // Validating employee with null/empty manager should succeed immediately
        EmployeeHierarchyRules.Validate("EMP01", managerCode, emptyGraph);
    }

    [Fact]
    public void Validate_SelfReportingCycle_ThrowsReportingCycleException()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMP01"] = "EMP01"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP01", "EMP01", graph));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void Validate_TwoLevelCycle_ThrowsReportingCycleException()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMP01"] = "EMP02",
            ["EMP02"] = "EMP01"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP01", "EMP02", graph));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void Validate_FiveLevelCycle_DeepLoop_ThrowsReportingCycleException()
    {
        // Chain: A -> B -> C -> D -> E -> B (cycle at B)
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "D",
            ["D"] = "E",
            ["E"] = "B"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", graph));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void Validate_FiveLevelCycle_LoopBackToOrigin_ThrowsReportingCycleException()
    {
        // Chain: A -> B -> C -> D -> E -> A (cycle back to origin A)
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "D",
            ["D"] = "E",
            ["E"] = "A"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", graph));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void Validate_CycleDetection_IsCaseInsensitive()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["emp_alpha"] = "EMP_BETA",
            ["emp_beta"] = "EMP_GAMMA",
            ["emp_gamma"] = "EMP_ALPHA"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("EMP_ALPHA", "emp_beta", graph));
        Assert.Equal("Reporting relationship contains a cycle.", ex.Message);
    }

    [Fact]
    public void Validate_MissingAncestorInChain_ThrowsUnavailableException()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C",
            ["C"] = "UNKNOWN_MANAGER"
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            EmployeeHierarchyRules.Validate("A", "B", graph));
        Assert.Equal("Reporting chain contains an unavailable or foreign-company employee.", ex.Message);
    }

    [Fact]
    public void Validate_BranchingHierarchy_MultipleSubordinatesShareManager_Succeeds()
    {
        // Tree:
        //       CEO (null)
        //        |
        //       VP
        //        |
        //      MGR_01
        //     /   |   \
        //   DEV1 DEV2 DEV3
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CEO"] = null,
            ["VP"] = "CEO",
            ["MGR_01"] = "VP",
            ["DEV1"] = "MGR_01",
            ["DEV2"] = "MGR_01",
            ["DEV3"] = "MGR_01"
        };

        EmployeeHierarchyRules.Validate("DEV1", "MGR_01", graph);
        EmployeeHierarchyRules.Validate("DEV2", "MGR_01", graph);
        EmployeeHierarchyRules.Validate("DEV3", "MGR_01", graph);
    }

    [Fact]
    public void Validate_DeepHierarchy_100Levels_Succeeds()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 100; i++)
        {
            graph[$"NODE_{i}"] = i == 99 ? null : $"NODE_{i + 1}";
        }

        EmployeeHierarchyRules.Validate("NODE_0", "NODE_1", graph);
    }

    [Fact]
    public void Validate_DisconnectedCycleElsewhereInGraph_DoesNotBlockValidReportingChain()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // Valid chain
            ["EMP_X"] = "MGR_Y",
            ["MGR_Y"] = "HEAD_Z",
            ["HEAD_Z"] = null,

            // Disconnected cycle
            ["CYC_1"] = "CYC_2",
            ["CYC_2"] = "CYC_3",
            ["CYC_3"] = "CYC_1"
        };

        // Validating EMP_X only traverses X -> Y -> Z -> null; it should succeed and ignore the unrelated cycle
        EmployeeHierarchyRules.Validate("EMP_X", "MGR_Y", graph);
    }

    [Fact]
    public void Validate_EmployeeReassignmentAcrossDepartments_MaintainsChainIntegrity()
    {
        var graph = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CEO"] = null,
            ["HEAD_SALES"] = "CEO",
            ["MGR_SALES"] = "HEAD_SALES",
            ["HEAD_ENG"] = "CEO",
            ["MGR_ENG"] = "HEAD_ENG",
            ["EMP01"] = "MGR_SALES" // initially in sales
        };

        // Current reporting structure valid
        EmployeeHierarchyRules.Validate("EMP01", "MGR_SALES", graph);

        // Reassigning to engineering manager
        graph["EMP01"] = "MGR_ENG";
        EmployeeHierarchyRules.Validate("EMP01", "MGR_ENG", graph);
    }
}
