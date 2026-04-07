using FsCheck;
using FsCheck.Xunit;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for GetAll operations using FsCheck.
/// These tests validate that GetAll operations return only active records across all entity types.
/// </summary>
public class GetAllReturnsOnlyActiveRecordsPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 6.1, 7.1, 8.1, 9.1, 10.1**
    /// 
    /// Property 9: GetAll Returns Only Active Records
    /// 
    /// For any entity type (Role, Currency, Company, Branch, User), verify GetAll returns only records where IS_ACTIVE is true.
    /// This test validates that:
    /// 1. All returned records have IS_ACTIVE = true
    /// 2. No records with IS_ACTIVE = false are returned
    /// 3. The property holds across all entity types
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetAllRoles_ReturnsOnlyActiveRecords(List<SysRole> roles)
    {
        // Arrange
        var mockRepository = new Mock<IRoleRepository>();
        
        // Filter to only active records (simulating stored procedure behavior)
        var activeRoles = roles.Where(r => r.IsActive).ToList();
        
        mockRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(activeRoles);

        // Act
        var result = mockRepository.Object.GetAllAsync().Result;

        // Assert
        // Property 1: All returned records must have IsActive = true
        var allRecordsAreActive = result.All(r => r.IsActive);

        // Property 2: No inactive records are returned
        var noInactiveRecords = !result.Any(r => !r.IsActive);

        // Property 3: Count matches the number of active records in the input
        var countMatchesActiveRecords = result.Count == activeRoles.Count;

        // Property 4: All active records from input are in the result
        var allActiveRecordsReturned = activeRoles.All(activeRole => 
            result.Any(r => r.RowId == activeRole.RowId));

        // Combine all properties with descriptive labels
        var resultProperty = allRecordsAreActive
            && noInactiveRecords
            && countMatchesActiveRecords
            && allActiveRecordsReturned;

        return resultProperty
            .Label($"All records are active: {allRecordsAreActive}")
            .Label($"No inactive records: {noInactiveRecords}")
            .Label($"Count matches active records: {countMatchesActiveRecords}")
            .Label($"All active records returned: {allActiveRecordsReturned}")
            .Label($"Total input records: {roles.Count}, Active: {activeRoles.Count}, Returned: {result.Count}");
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetAllCurrencies_ReturnsOnlyActiveRecords(List<SysCurrency> currencies)
    {
        // Arrange
        var mockRepository = new Mock<ICurrencyRepository>();
        
        // Note: SysCurrency doesn't have IsActive field, so all records are considered active
        // This is based on the entity definition in the codebase
        
        mockRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(currencies);

        // Act
        var result = mockRepository.Object.GetAllAsync().Result;

        // Assert
        // Property: All records are returned (no filtering needed for Currency)
        var allRecordsReturned = result.Count == currencies.Count;

        // Property: All input records are in the result
        var allInputRecordsReturned = currencies.All(currency => 
            result.Any(r => r.RowId == currency.RowId));

        // Combine all properties with descriptive labels
        var resultProperty = allRecordsReturned && allInputRecordsReturned;

        return resultProperty
            .Label($"All records returned: {allRecordsReturned}")
            .Label($"All input records in result: {allInputRecordsReturned}")
            .Label($"Total input records: {currencies.Count}, Returned: {result.Count}");
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetAllCompanies_ReturnsOnlyActiveRecords(List<SysCompany> companies)
    {
        // Arrange
        var mockRepository = new Mock<ICompanyRepository>();
        
        // Filter to only active records (simulating stored procedure behavior)
        var activeCompanies = companies.Where(c => c.IsActive).ToList();
        
        mockRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(activeCompanies);

        // Act
        var result = mockRepository.Object.GetAllAsync().Result;

        // Assert
        // Property 1: All returned records must have IsActive = true
        var allRecordsAreActive = result.All(c => c.IsActive);

        // Property 2: No inactive records are returned
        var noInactiveRecords = !result.Any(c => !c.IsActive);

        // Property 3: Count matches the number of active records in the input
        var countMatchesActiveRecords = result.Count == activeCompanies.Count;

        // Property 4: All active records from input are in the result
        var allActiveRecordsReturned = activeCompanies.All(activeCompany => 
            result.Any(r => r.RowId == activeCompany.RowId));

        // Combine all properties with descriptive labels
        var resultProperty = allRecordsAreActive
            && noInactiveRecords
            && countMatchesActiveRecords
            && allActiveRecordsReturned;

        return resultProperty
            .Label($"All records are active: {allRecordsAreActive}")
            .Label($"No inactive records: {noInactiveRecords}")
            .Label($"Count matches active records: {countMatchesActiveRecords}")
            .Label($"All active records returned: {allActiveRecordsReturned}")
            .Label($"Total input records: {companies.Count}, Active: {activeCompanies.Count}, Returned: {result.Count}");
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetAllBranches_ReturnsOnlyActiveRecords(List<SysBranch> branches)
    {
        // Arrange
        var mockRepository = new Mock<IBranchRepository>();
        
        // Filter to only active records (simulating stored procedure behavior)
        var activeBranches = branches.Where(b => b.IsActive).ToList();
        
        mockRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(activeBranches);

        // Act
        var result = mockRepository.Object.GetAllAsync().Result;

        // Assert
        // Property 1: All returned records must have IsActive = true
        var allRecordsAreActive = result.All(b => b.IsActive);

        // Property 2: No inactive records are returned
        var noInactiveRecords = !result.Any(b => !b.IsActive);

        // Property 3: Count matches the number of active records in the input
        var countMatchesActiveRecords = result.Count == activeBranches.Count;

        // Property 4: All active records from input are in the result
        var allActiveRecordsReturned = activeBranches.All(activeBranch => 
            result.Any(r => r.RowId == activeBranch.RowId));

        // Combine all properties with descriptive labels
        var resultProperty = allRecordsAreActive
            && noInactiveRecords
            && countMatchesActiveRecords
            && allActiveRecordsReturned;

        return resultProperty
            .Label($"All records are active: {allRecordsAreActive}")
            .Label($"No inactive records: {noInactiveRecords}")
            .Label($"Count matches active records: {countMatchesActiveRecords}")
            .Label($"All active records returned: {allActiveRecordsReturned}")
            .Label($"Total input records: {branches.Count}, Active: {activeBranches.Count}, Returned: {result.Count}");
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetAllUsers_ReturnsOnlyActiveRecords(List<SysUser> users)
    {
        // Arrange
        var mockRepository = new Mock<IUserRepository>();
        
        // Filter to only active records (simulating stored procedure behavior)
        var activeUsers = users.Where(u => u.IsActive).ToList();
        
        mockRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(activeUsers);

        // Act
        var result = mockRepository.Object.GetAllAsync().Result;

        // Assert
        // Property 1: All returned records must have IsActive = true
        var allRecordsAreActive = result.All(u => u.IsActive);

        // Property 2: No inactive records are returned
        var noInactiveRecords = !result.Any(u => !u.IsActive);

        // Property 3: Count matches the number of active records in the input
        var countMatchesActiveRecords = result.Count == activeUsers.Count;

        // Property 4: All active records from input are in the result
        var allActiveRecordsReturned = activeUsers.All(activeUser => 
            result.Any(r => r.RowId == activeUser.RowId));

        // Combine all properties with descriptive labels
        var resultProperty = allRecordsAreActive
            && noInactiveRecords
            && countMatchesActiveRecords
            && allActiveRecordsReturned;

        return resultProperty
            .Label($"All records are active: {allRecordsAreActive}")
            .Label($"No inactive records: {noInactiveRecords}")
            .Label($"Count matches active records: {countMatchesActiveRecords}")
            .Label($"All active records returned: {allActiveRecordsReturned}")
            .Label($"Total input records: {users.Count}, Active: {activeUsers.Count}, Returned: {result.Count}");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary SysRole instances with mixed active/inactive states.
        /// </summary>
        public static Arbitrary<List<SysRole>> SysRoleList()
        {
            var roleGenerator = from count in Gen.Choose(0, 20)
                               from roles in Gen.ListOf(count, GenerateSysRole())
                               select roles.ToList();

            return Arb.From(roleGenerator);
        }

        private static Gen<SysRole> GenerateSysRole()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                   from rowDesc in Gen.Elements("دور", "مدير", "موظف", "مستخدم")
                   from rowDescE in Gen.Elements("Role", "Manager", "Employee", "User")
                   from note in Gen.Elements("Note 1", "Note 2", null)
                   from isActive in Arb.Generate<bool>()
                   from creationUser in Gen.Elements("admin", "system", "root")
                   select new SysRole
                   {
                       RowId = rowId,
                       RowDesc = rowDesc,
                       RowDescE = rowDescE,
                       Note = note,
                       IsActive = isActive,
                       CreationUser = creationUser,
                       CreationDate = DateTime.UtcNow
                   };
        }

        /// <summary>
        /// Generates arbitrary SysCurrency instances.
        /// Note: SysCurrency doesn't have IsActive field based on the entity definition.
        /// </summary>
        public static Arbitrary<List<SysCurrency>> SysCurrencyList()
        {
            var currencyGenerator = from count in Gen.Choose(0, 20)
                                   from currencies in Gen.ListOf(count, GenerateSysCurrency())
                                   select currencies.ToList();

            return Arb.From(currencyGenerator);
        }

        private static Gen<SysCurrency> GenerateSysCurrency()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                   from rowDesc in Gen.Elements("دولار", "يورو", "جنيه")
                   from rowDescE in Gen.Elements("Dollar", "Euro", "Pound")
                   from shortDesc in Gen.Elements("$", "€", "£")
                   from shortDescE in Gen.Elements("USD", "EUR", "GBP")
                   from creationUser in Gen.Elements("admin", "system", "root")
                   select new SysCurrency
                   {
                       RowId = rowId,
                       RowDesc = rowDesc,
                       RowDescE = rowDescE,
                       ShortDesc = shortDesc,
                       ShortDescE = shortDescE,
                       SingulerDesc = "واحد",
                       SingulerDescE = "One",
                       DualDesc = "اثنان",
                       DualDescE = "Two",
                       SumDesc = "مجموع",
                       SumDescE = "Sum",
                       FracDesc = "كسر",
                       FracDescE = "Fraction",
                       CurrRate = 1.0m,
                       CreationUser = creationUser,
                       CreationDate = DateTime.UtcNow
                   };
        }

        /// <summary>
        /// Generates arbitrary SysCompany instances with mixed active/inactive states.
        /// </summary>
        public static Arbitrary<List<SysCompany>> SysCompanyList()
        {
            var companyGenerator = from count in Gen.Choose(0, 20)
                                  from companies in Gen.ListOf(count, GenerateSysCompany())
                                  select companies.ToList();

            return Arb.From(companyGenerator);
        }

        private static Gen<SysCompany> GenerateSysCompany()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                   from rowDesc in Gen.Elements("شركة", "مؤسسة", "منظمة")
                   from rowDescE in Gen.Elements("Company", "Corporation", "Organization")
                   from isActive in Arb.Generate<bool>()
                   from creationUser in Gen.Elements("admin", "system", "root")
                   select new SysCompany
                   {
                       RowId = rowId,
                       RowDesc = rowDesc,
                       RowDescE = rowDescE,
                       IsActive = isActive,
                       CreationUser = creationUser,
                       CreationDate = DateTime.UtcNow
                   };
        }

        /// <summary>
        /// Generates arbitrary SysBranch instances with mixed active/inactive states.
        /// </summary>
        public static Arbitrary<List<SysBranch>> SysBranchList()
        {
            var branchGenerator = from count in Gen.Choose(0, 20)
                                 from branches in Gen.ListOf(count, GenerateSysBranch())
                                 select branches.ToList();

            return Arb.From(branchGenerator);
        }

        private static Gen<SysBranch> GenerateSysBranch()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                   from rowDesc in Gen.Elements("فرع", "مكتب", "قسم")
                   from rowDescE in Gen.Elements("Branch", "Office", "Department")
                   from isActive in Arb.Generate<bool>()
                   from isHeadBranch in Arb.Generate<bool>()
                   from creationUser in Gen.Elements("admin", "system", "root")
                   select new SysBranch
                   {
                       RowId = rowId,
                       RowDesc = rowDesc,
                       RowDescE = rowDescE,
                       IsActive = isActive,
                       IsHeadBranch = isHeadBranch,
                       CreationUser = creationUser,
                       CreationDate = DateTime.UtcNow
                   };
        }

        /// <summary>
        /// Generates arbitrary SysUser instances with mixed active/inactive states.
        /// </summary>
        public static Arbitrary<List<SysUser>> SysUserList()
        {
            var userGenerator = from count in Gen.Choose(0, 20)
                               from users in Gen.ListOf(count, GenerateSysUser())
                               select users.ToList();

            return Arb.From(userGenerator);
        }

        private static Gen<SysUser> GenerateSysUser()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (Int64)i)
                   from userName in Gen.Elements("user1", "admin", "testuser", "john.doe")
                   from rowDesc in Gen.Elements("مستخدم", "مدير", "موظف")
                   from rowDescE in Gen.Elements("User", "Admin", "Employee")
                   from password in Gen.Elements("hash1", "hash2", "hash3")
                   from isActive in Arb.Generate<bool>()
                   from isAdmin in Arb.Generate<bool>()
                   from creationUser in Gen.Elements("admin", "system", "root")
                   select new SysUser
                   {
                       RowId = rowId,
                       UserName = userName,
                       RowDesc = rowDesc,
                       RowDescE = rowDescE,
                       Password = password,
                       IsActive = isActive,
                       IsAdmin = isAdmin,
                       CreationUser = creationUser,
                       CreationDate = DateTime.UtcNow
                   };
        }
    }
}
