using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Helper class for batch database operations.
/// Provides optimized methods for bulk insert, update, and delete operations.
/// 
/// Performance Optimization: Batch operations reduce database round-trips
/// by grouping multiple operations into a single transaction.
/// This is especially beneficial for:
/// - Bulk data imports
/// - Mass updates (e.g., setting IsActive to false for multiple records)
/// - Batch deletions
/// 
/// Usage Guidelines:
/// - Use batch operations when inserting/updating/deleting 10+ records
/// - For smaller batches (< 10 records), individual operations may be faster
/// - Consider memory usage for very large batches (> 1000 records)
/// - Use appropriate batch sizes based on available memory and network latency
/// </summary>
public static class BatchOperations
{
    /// <summary>
    /// Default batch size for bulk operations.
    /// Balances memory usage and performance.
    /// </summary>
    public const int DefaultBatchSize = 100;

    /// <summary>
    /// Inserts multiple entities in batches to optimize performance.
    /// Automatically calls SaveChangesAsync after each batch.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="entities">The entities to insert</param>
    /// <param name="batchSize">The number of entities to insert per batch (default: 100)</param>
    /// <param name="logger">Optional logger for tracking progress</param>
    /// <returns>The total number of entities inserted</returns>
    public static async Task<int> BulkInsertAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        int batchSize = DefaultBatchSize,
        ILogger? logger = null) where TEntity : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            logger?.LogDebug("No entities to insert");
            return 0;
        }

        var totalInserted = 0;
        var batches = entityList.Chunk(batchSize);

        foreach (var batch in batches)
        {
            await context.Set<TEntity>().AddRangeAsync(batch);
            var inserted = await context.SaveChangesAsync();
            totalInserted += inserted;

            logger?.LogDebug("Inserted batch of {Count} {EntityType} entities (Total: {Total})",
                batch.Length, typeof(TEntity).Name, totalInserted);
        }

        logger?.LogInformation("Bulk insert completed: {Total} {EntityType} entities inserted",
            totalInserted, typeof(TEntity).Name);

        return totalInserted;
    }

    /// <summary>
    /// Updates multiple entities in batches to optimize performance.
    /// Automatically calls SaveChangesAsync after each batch.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="entities">The entities to update</param>
    /// <param name="batchSize">The number of entities to update per batch (default: 100)</param>
    /// <param name="logger">Optional logger for tracking progress</param>
    /// <returns>The total number of entities updated</returns>
    public static async Task<int> BulkUpdateAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        int batchSize = DefaultBatchSize,
        ILogger? logger = null) where TEntity : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            logger?.LogDebug("No entities to update");
            return 0;
        }

        var totalUpdated = 0;
        var batches = entityList.Chunk(batchSize);

        foreach (var batch in batches)
        {
            context.Set<TEntity>().UpdateRange(batch);
            var updated = await context.SaveChangesAsync();
            totalUpdated += updated;

            logger?.LogDebug("Updated batch of {Count} {EntityType} entities (Total: {Total})",
                batch.Length, typeof(TEntity).Name, totalUpdated);
        }

        logger?.LogInformation("Bulk update completed: {Total} {EntityType} entities updated",
            totalUpdated, typeof(TEntity).Name);

        return totalUpdated;
    }

    /// <summary>
    /// Deletes multiple entities in batches to optimize performance.
    /// Automatically calls SaveChangesAsync after each batch.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="entities">The entities to delete</param>
    /// <param name="batchSize">The number of entities to delete per batch (default: 100)</param>
    /// <param name="logger">Optional logger for tracking progress</param>
    /// <returns>The total number of entities deleted</returns>
    public static async Task<int> BulkDeleteAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        int batchSize = DefaultBatchSize,
        ILogger? logger = null) where TEntity : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            logger?.LogDebug("No entities to delete");
            return 0;
        }

        var totalDeleted = 0;
        var batches = entityList.Chunk(batchSize);

        foreach (var batch in batches)
        {
            context.Set<TEntity>().RemoveRange(batch);
            var deleted = await context.SaveChangesAsync();
            totalDeleted += deleted;

            logger?.LogDebug("Deleted batch of {Count} {EntityType} entities (Total: {Total})",
                batch.Length, typeof(TEntity).Name, totalDeleted);
        }

        logger?.LogInformation("Bulk delete completed: {Total} {EntityType} entities deleted",
            totalDeleted, typeof(TEntity).Name);

        return totalDeleted;
    }

    /// <summary>
    /// Performs a soft delete on multiple entities by setting IsActive to false.
    /// This method assumes entities have an IsActive property.
    /// Automatically calls SaveChangesAsync after each batch.
    /// </summary>
    /// <typeparam name="TEntity">The entity type with IsActive property</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="entities">The entities to soft delete</param>
    /// <param name="batchSize">The number of entities to process per batch (default: 100)</param>
    /// <param name="logger">Optional logger for tracking progress</param>
    /// <returns>The total number of entities soft deleted</returns>
    public static async Task<int> BulkSoftDeleteAsync<TEntity>(
        this DbContext context,
        IEnumerable<TEntity> entities,
        int batchSize = DefaultBatchSize,
        ILogger? logger = null) where TEntity : class
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");

        var entityList = entities.ToList();
        if (entityList.Count == 0)
        {
            logger?.LogDebug("No entities to soft delete");
            return 0;
        }

        // Verify that TEntity has IsActive property
        var isActiveProperty = typeof(TEntity).GetProperty("IsActive");
        if (isActiveProperty == null)
        {
            throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} does not have an IsActive property");
        }

        var totalSoftDeleted = 0;
        var batches = entityList.Chunk(batchSize);

        foreach (var batch in batches)
        {
            foreach (var entity in batch)
            {
                // Set IsActive to false
                isActiveProperty.SetValue(entity, false);
            }

            context.Set<TEntity>().UpdateRange(batch);
            var updated = await context.SaveChangesAsync();
            totalSoftDeleted += updated;

            logger?.LogDebug("Soft deleted batch of {Count} {EntityType} entities (Total: {Total})",
                batch.Length, typeof(TEntity).Name, totalSoftDeleted);
        }

        logger?.LogInformation("Bulk soft delete completed: {Total} {EntityType} entities soft deleted",
            totalSoftDeleted, typeof(TEntity).Name);

        return totalSoftDeleted;
    }

    /// <summary>
    /// Executes a batch operation within a transaction.
    /// Rolls back all changes if any operation fails.
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="operation">The batch operation to execute</param>
    /// <param name="logger">Optional logger for tracking progress</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this DbContext context,
        Func<Task<T>> operation,
        ILogger? logger = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (operation == null) throw new ArgumentNullException(nameof(operation));

        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            logger?.LogDebug("Starting batch transaction");

            var result = await operation();

            await transaction.CommitAsync();
            logger?.LogInformation("Batch transaction committed successfully");

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Batch transaction failed, rolling back");
            await transaction.RollbackAsync();
            throw;
        }
    }
}
