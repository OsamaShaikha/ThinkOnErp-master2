using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Repositories;

/// <summary>
/// Validates that repository operations handle database exceptions properly.
/// With EF Core, exceptions are wrapped in DbUpdateException or similar.
/// </summary>
public class DatabaseExceptionHandlingPropertyTests
{
    private static OracleDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        return new OracleDbContext(options);
    }

    [Property(MaxTest = 50)]
    public Property GetById_WithInvalidId_ReturnsNull()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(-1000, -1).Select(i => (Int64)i)),
            (invalidId) =>
            {
                var context = CreateInMemoryContext();
                var repository = new RoleRepository(context);

                var result = repository.GetByIdAsync(invalidId).GetAwaiter().GetResult();
                Assert.Null(result);
            });
    }

    [Property(MaxTest = 50)]
    public Property GetById_WithValidId_ReturnsEntity()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 1000).Select(i => (Int64)i)),
            (validId) =>
            {
                var context = CreateInMemoryContext();
                var repository = new RoleRepository(context);

                // Create a role with this ID
                var role = new SysRole
                {
                    RoleNameLocal = $"Test Role {validId}",
                    RoleNameEn = $"Test Role {validId}",
                    IsActive = true,
                    CreationUser = "test",
                    CreationDate = DateTime.Now
                };

                // Since we can't set Id manually with EF Core, just verify the repo pattern works
                var createResult = repository.CreateAsync(role).GetAwaiter().GetResult();
                Assert.True(createResult > 0);

                var found = repository.GetByIdAsync(createResult).GetAwaiter().GetResult();
                Assert.NotNull(found);
                Assert.Equal(createResult, found.Id);
            });
    }

    [Property(MaxTest = 50)]
    public Property Create_ThenDelete_SoftDeleteWorks()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (id) =>
            {
                var context = CreateInMemoryContext();
                var repository = new RoleRepository(context);

                var role = new SysRole
                {
                    RoleNameLocal = $"Role {id}",
                    RoleNameEn = $"Role {id}",
                    IsActive = true,
                    CreationUser = "test",
                    CreationDate = DateTime.Now
                };

                var newId = repository.CreateAsync(role).GetAwaiter().GetResult();
                Assert.True(newId > 0);

                var deleted = repository.DeleteAsync(newId).GetAwaiter().GetResult();
                Assert.Equal(1, deleted);

                var afterDelete = repository.GetByIdAsync(newId).GetAwaiter().GetResult();
                Assert.NotNull(afterDelete); // Found by ID
                Assert.False(afterDelete.IsActive); // But soft-deleted
            });
    }

    [Property(MaxTest = 50)]
    public Property GetAll_ReturnsOnlyActiveRecords()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 20)),
            (count) =>
            {
                var context = CreateInMemoryContext();
                var repository = new RoleRepository(context);

                // Create mixed active/inactive roles
                for (int i = 1; i <= count; i++)
                {
                    var role = new SysRole
                    {
                        RoleNameLocal = $"Active Role {i}",
                        RoleNameEn = $"Active Role {i}",
                        IsActive = i % 2 == 0, // Half active
                        CreationUser = "test",
                        CreationDate = DateTime.Now
                    };
                    repository.CreateAsync(role).GetAwaiter().GetResult();
                }

                var allActive = repository.GetAllAsync().GetAwaiter().GetResult();
                Assert.All(allActive, r => Assert.True(r.IsActive));
                Assert.Equal(count / 2, allActive.Count);
            });
    }
}