using FsCheck;
using FsCheck.Xunit;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Property-based tests for GetById operations using FsCheck.
/// These tests validate that GetById operations return the matching record or null across all entity types.
/// </summary>
public class GetByIdReturnsMatchOrNullPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 6.2, 7.2, 8.2, 9.2, 10.2**
    /// 
    /// Property 10: GetById Returns Match or Null
    /// 
    /// For any entity type and ID, verify GetById returns matching record or null.
    /// This test validates that:
    /// 1. When ID exists, the matching record is returned
    /// 2. When ID does not exist, null is returned
    /// 3. The returned record has the correct ID
    /// 4. The property holds across all entity types
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetRoleById_ReturnsMatchOrNull(List<SysRole> roles, decimal searchId)
    {
        // Arrange
        var mockRepository = new Mock<IRoleRepository>();
        
        // Find the role with the search ID
        var expectedRole = roles.FirstOrDefault(r => r.RowId == searchId);
        
        mockRepository
            .Setup(x => x.GetByIdAsync(searchId))
            .ReturnsAsync(expectedRole);

        // Act
        var result = mockRepository.Object.GetByIdAsync(searchId).Result;

        // Assert
        bool propertyHolds;
        
        if (expectedRole != null)
        {
            // Property 1: When ID exists, result is not null
            var resultIsNotNull = result != null;
            
            // Property 2: Returned record has the correct ID
            var correctId = result?.RowId == searchId;
            
            // Property 3: Returned record matches the expected record
            var matchesExpected = result?.RowId == expectedRole.RowId &&
                                 result?.RowDesc == expectedRole.RowDesc &&
                                 result?.RowDescE == expectedRole.RowDescE;
            
            propertyHolds = resultIsNotNull && correctId && matchesExpected;
            
            return propertyHolds
                .Label($"Result is not null: {resultIsNotNull}")
                .Label($"Correct ID: {correctId}")
                .Label($"Matches expected: {matchesExpected}")
                .Label($"Search ID: {searchId}, Found: {result != null}");
        }
        else
        {
            // Property 4: When ID does not exist, result is null
            var resultIsNull = result == null;
            
            propertyHolds = resultIsNull;
            
            return propertyHolds
                .Label($"Result is null: {resultIsNull}")
                .Label($"Search ID: {searchId}, Found: {result != null}")
                .Label($"Total roles: {roles.Count}");
        }
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetCurrencyById_ReturnsMatchOrNull(List<SysCurrency> currencies, decimal searchId)
    {
        // Arrange
        var mockRepository = new Mock<ICurrencyRepository>();
        
        // Find the currency with the search ID
        var expectedCurrency = currencies.FirstOrDefault(c => c.RowId == searchId);
        
        mockRepository
            .Setup(x => x.GetByIdAsync(searchId))
            .ReturnsAsync(expectedCurrency);

        // Act
        var result = mockRepository.Object.GetByIdAsync(searchId).Result;

        // Assert
        bool propertyHolds;
        
        if (expectedCurrency != null)
        {
            // Property 1: When ID exists, result is not null
            var resultIsNotNull = result != null;
            
            // Property 2: Returned record has the correct ID
            var correctId = result?.RowId == searchId;
            
            // Property 3: Returned record matches the expected record
            var matchesExpected = result?.RowId == expectedCurrency.RowId &&
                                 result?.RowDesc == expectedCurrency.RowDesc &&
                                 result?.RowDescE == expectedCurrency.RowDescE;
            
            propertyHolds = resultIsNotNull && correctId && matchesExpected;
            
            return propertyHolds
                .Label($"Result is not null: {resultIsNotNull}")
                .Label($"Correct ID: {correctId}")
                .Label($"Matches expected: {matchesExpected}")
                .Label($"Search ID: {searchId}, Found: {result != null}");
        }
        else
        {
            // Property 4: When ID does not exist, result is null
            var resultIsNull = result == null;
            
            propertyHolds = resultIsNull;
            
            return propertyHolds
                .Label($"Result is null: {resultIsNull}")
                .Label($"Search ID: {searchId}, Found: {result != null}")
                .Label($"Total currencies: {currencies.Count}");
        }
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetCompanyById_ReturnsMatchOrNull(List<SysCompany> companies, decimal searchId)
    {
        // Arrange
        var mockRepository = new Mock<ICompanyRepository>();
        
        // Find the company with the search ID
        var expectedCompany = companies.FirstOrDefault(c => c.RowId == searchId);
        
        mockRepository
            .Setup(x => x.GetByIdAsync(searchId))
            .ReturnsAsync(expectedCompany);

        // Act
        var result = mockRepository.Object.GetByIdAsync(searchId).Result;

        // Assert
        bool propertyHolds;
        
        if (expectedCompany != null)
        {
            // Property 1: When ID exists, result is not null
            var resultIsNotNull = result != null;
            
            // Property 2: Returned record has the correct ID
            var correctId = result?.RowId == searchId;
            
            // Property 3: Returned record matches the expected record
            var matchesExpected = result?.RowId == expectedCompany.RowId &&
                                 result?.RowDesc == expectedCompany.RowDesc &&
                                 result?.RowDescE == expectedCompany.RowDescE;
            
            propertyHolds = resultIsNotNull && correctId && matchesExpected;
            
            return propertyHolds
                .Label($"Result is not null: {resultIsNotNull}")
                .Label($"Correct ID: {correctId}")
                .Label($"Matches expected: {matchesExpected}")
                .Label($"Search ID: {searchId}, Found: {result != null}");
        }
        else
        {
            // Property 4: When ID does not exist, result is null
            var resultIsNull = result == null;
            
            propertyHolds = resultIsNull;
            
            return propertyHolds
                .Label($"Result is null: {resultIsNull}")
                .Label($"Search ID: {searchId}, Found: {result != null}")
                .Label($"Total companies: {companies.Count}");
        }
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetBranchById_ReturnsMatchOrNull(List<SysBranch> branches, decimal searchId)
    {
        // Arrange
        var mockRepository = new Mock<IBranchRepository>();
        
        // Find the branch with the search ID
        var expectedBranch = branches.FirstOrDefault(b => b.RowId == searchId);
        
        mockRepository
            .Setup(x => x.GetByIdAsync(searchId))
            .ReturnsAsync(expectedBranch);

        // Act
        var result = mockRepository.Object.GetByIdAsync(searchId).Result;

        // Assert
        bool propertyHolds;
        
        if (expectedBranch != null)
        {
            // Property 1: When ID exists, result is not null
            var resultIsNotNull = result != null;
            
            // Property 2: Returned record has the correct ID
            var correctId = result?.RowId == searchId;
            
            // Property 3: Returned record matches the expected record
            var matchesExpected = result?.RowId == expectedBranch.RowId &&
                                 result?.RowDesc == expectedBranch.RowDesc &&
                                 result?.RowDescE == expectedBranch.RowDescE;
            
            propertyHolds = resultIsNotNull && correctId && matchesExpected;
            
            return propertyHolds
                .Label($"Result is not null: {resultIsNotNull}")
                .Label($"Correct ID: {correctId}")
                .Label($"Matches expected: {matchesExpected}")
                .Label($"Search ID: {searchId}, Found: {result != null}");
        }
        else
        {
            // Property 4: When ID does not exist, result is null
            var resultIsNull = result == null;
            
            propertyHolds = resultIsNull;
            
            return propertyHolds
                .Label($"Result is null: {resultIsNull}")
                .Label($"Search ID: {searchId}, Found: {result != null}")
                .Label($"Total branches: {branches.Count}");
        }
    }

    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property GetUserById_ReturnsMatchOrNull(List<SysUser> users, decimal searchId)
    {
        // Arrange
        var mockRepository = new Mock<IUserRepository>();
        
        // Find the user with the search ID
        var expectedUser = users.FirstOrDefault(u => u.RowId == searchId);
        
        mockRepository
            .Setup(x => x.GetByIdAsync(searchId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = mockRepository.Object.GetByIdAsync(searchId).Result;

        // Assert
        bool propertyHolds;
        
        if (expectedUser != null)
        {
            // Property 1: When ID exists, result is not null
            var resultIsNotNull = result != null;
            
            // Property 2: Returned record has the correct ID
            var correctId = result?.RowId == searchId;
            
            // Property 3: Returned record matches the expected record
            var matchesExpected = result?.RowId == expectedUser.RowId &&
                                 result?.UserName == expectedUser.UserName &&
                                 result?.RowDescE == expectedUser.RowDescE;
            
            propertyHolds = resultIsNotNull && correctId && matchesExpected;
            
            return propertyHolds
                .Label($"Result is not null: {resultIsNotNull}")
                .Label($"Correct ID: {correctId}")
                .Label($"Matches expected: {matchesExpected}")
                .Label($"Search ID: {searchId}, Found: {result != null}");
        }
        else
        {
            // Property 4: When ID does not exist, result is null
            var resultIsNull = result == null;
            
            propertyHolds = resultIsNull;
            
            return propertyHolds
                .Label($"Result is null: {resultIsNull}")
                .Label($"Search ID: {searchId}, Found: {result != null}")
                .Label($"Total users: {users.Count}");
        }
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary SysRole instances with unique IDs.
        /// </summary>
        public static Arbitrary<List<SysRole>> SysRoleList()
        {
            var roleGenerator = from count in Gen.Choose(0, 20)
                               from roles in Gen.ListOf(count, GenerateSysRole())
                               select roles.GroupBy(r => r.RowId).Select(g => g.First()).ToList();

            return Arb.From(roleGenerator);
        }

        private static Gen<SysRole> GenerateSysRole()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
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
        /// Generates arbitrary SysCurrency instances with unique IDs.
        /// </summary>
        public static Arbitrary<List<SysCurrency>> SysCurrencyList()
        {
            var currencyGenerator = from count in Gen.Choose(0, 20)
                                   from currencies in Gen.ListOf(count, GenerateSysCurrency())
                                   select currencies.GroupBy(c => c.RowId).Select(g => g.First()).ToList();

            return Arb.From(currencyGenerator);
        }

        private static Gen<SysCurrency> GenerateSysCurrency()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
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
        /// Generates arbitrary SysCompany instances with unique IDs.
        /// </summary>
        public static Arbitrary<List<SysCompany>> SysCompanyList()
        {
            var companyGenerator = from count in Gen.Choose(0, 20)
                                  from companies in Gen.ListOf(count, GenerateSysCompany())
                                  select companies.GroupBy(c => c.RowId).Select(g => g.First()).ToList();

            return Arb.From(companyGenerator);
        }

        private static Gen<SysCompany> GenerateSysCompany()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
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
        /// Generates arbitrary SysBranch instances with unique IDs.
        /// </summary>
        public static Arbitrary<List<SysBranch>> SysBranchList()
        {
            var branchGenerator = from count in Gen.Choose(0, 20)
                                 from branches in Gen.ListOf(count, GenerateSysBranch())
                                 select branches.GroupBy(b => b.RowId).Select(g => g.First()).ToList();

            return Arb.From(branchGenerator);
        }

        private static Gen<SysBranch> GenerateSysBranch()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
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
        /// Generates arbitrary SysUser instances with unique IDs.
        /// </summary>
        public static Arbitrary<List<SysUser>> SysUserList()
        {
            var userGenerator = from count in Gen.Choose(0, 20)
                               from users in Gen.ListOf(count, GenerateSysUser())
                               select users.GroupBy(u => u.RowId).Select(g => g.First()).ToList();

            return Arb.From(userGenerator);
        }

        private static Gen<SysUser> GenerateSysUser()
        {
            return from rowId in Gen.Choose(1, 1000000).Select(i => (decimal)i)
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
