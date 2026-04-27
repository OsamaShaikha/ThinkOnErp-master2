# Search Analytics Usage Guide

## Overview

The Search Analytics system automatically tracks all ticket search queries to provide insights into search patterns, performance, and user behavior. This guide explains how to use the analytics data.

---

## Automatic Logging

### What Gets Logged

Every search query to `GET /api/tickets` automatically logs:

- **Search Term:** The text search term (if provided)
- **Search Criteria:** Complete JSON of all filters applied
- **Filter Logic:** AND or OR combination logic
- **Result Count:** Number of tickets returned
- **Execution Time:** Query execution time in milliseconds
- **User Context:** User ID, Company ID, Branch ID
- **Timestamp:** When the search was performed

### How It Works

The logging happens asynchronously (fire-and-forget) so it doesn't impact search performance. If logging fails, it's logged as a warning but doesn't affect the search results.

---

## Analytics Queries

### 1. Top Searches

**Purpose:** Identify the most popular search terms

**Stored Procedure:**
```sql
SP_SYS_SEARCH_ANALYTICS_GET_TOP_SEARCHES(
    P_DAYS_BACK IN NUMBER DEFAULT 30,
    P_TOP_COUNT IN NUMBER DEFAULT 10,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
```

**Example Usage:**
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_SEARCH_ANALYTICS_GET_TOP_SEARCHES(
        P_DAYS_BACK => 30,
        P_TOP_COUNT => 10,
        P_RESULT_CURSOR => v_cursor
    );
END;
```

**Returns:**
- `SEARCH_TERM` - The search term
- `SEARCH_COUNT` - Number of times searched
- `AVG_RESULTS` - Average number of results
- `AVG_EXECUTION_TIME` - Average execution time (ms)

**Use Cases:**
- Identify common search patterns
- Optimize frequently searched terms
- Create suggested searches
- Improve search relevance

---

### 2. User Search History

**Purpose:** View search history for a specific user

**Stored Procedure:**
```sql
SP_SYS_SEARCH_ANALYTICS_GET_USER_HISTORY(
    P_USER_ID IN NUMBER,
    P_DAYS_BACK IN NUMBER DEFAULT 30,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
```

**Example Usage:**
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_SEARCH_ANALYTICS_GET_USER_HISTORY(
        P_USER_ID => 123,
        P_DAYS_BACK => 30,
        P_RESULT_CURSOR => v_cursor
    );
END;
```

**Returns:**
- `ROW_ID` - Analytics record ID
- `SEARCH_TERM` - Search term used
- `SEARCH_CRITERIA` - Complete filter criteria (JSON)
- `FILTER_LOGIC` - AND/OR logic
- `RESULT_COUNT` - Number of results
- `EXECUTION_TIME_MS` - Query execution time
- `SEARCH_DATE` - When search was performed

**Use Cases:**
- User behavior analysis
- Personalized search suggestions
- Support troubleshooting
- Audit trail

---

### 3. Search Performance Metrics

**Purpose:** Monitor search performance over time

**Stored Procedure:**
```sql
SP_SYS_SEARCH_ANALYTICS_GET_PERFORMANCE(
    P_DAYS_BACK IN NUMBER DEFAULT 7,
    P_RESULT_CURSOR OUT SYS_REFCURSOR
)
```

**Example Usage:**
```sql
DECLARE
    v_cursor SYS_REFCURSOR;
BEGIN
    SP_SYS_SEARCH_ANALYTICS_GET_PERFORMANCE(
        P_DAYS_BACK => 7,
        P_RESULT_CURSOR => v_cursor
    );
END;
```

**Returns:**
- `SEARCH_DAY` - Date (truncated to day)
- `TOTAL_SEARCHES` - Number of searches that day
- `AVG_RESULTS` - Average results per search
- `AVG_EXECUTION_TIME` - Average execution time (ms)
- `MAX_EXECUTION_TIME` - Slowest query (ms)
- `MIN_EXECUTION_TIME` - Fastest query (ms)

**Use Cases:**
- Performance monitoring
- Capacity planning
- Identify performance degradation
- Optimize slow queries

---

## C# Repository Methods

### Using ISearchAnalyticsRepository

```csharp
public class MyService
{
    private readonly ISearchAnalyticsRepository _analyticsRepo;

    public MyService(ISearchAnalyticsRepository analyticsRepo)
    {
        _analyticsRepo = analyticsRepo;
    }

    // Get top searches
    public async Task<List<TopSearchResult>> GetPopularSearches()
    {
        return await _analyticsRepo.GetTopSearchesAsync(
            daysBack: 30,
            topCount: 10
        );
    }

    // Get user history
    public async Task<List<SysSearchAnalytics>> GetUserHistory(Int64 userId)
    {
        return await _analyticsRepo.GetUserSearchHistoryAsync(
            userId: userId,
            daysBack: 30
        );
    }

    // Get performance metrics
    public async Task<List<SearchPerformanceMetric>> GetPerformance()
    {
        return await _analyticsRepo.GetSearchPerformanceAsync(
            daysBack: 7
        );
    }
}
```

---

## Analytics Dashboard Ideas

### 1. Search Volume Dashboard

**Metrics to Display:**
- Total searches today/this week/this month
- Search volume trend chart (last 30 days)
- Peak search hours
- Searches by company/branch

**SQL Query Example:**
```sql
SELECT 
    TRUNC(SEARCH_DATE) AS SEARCH_DAY,
    COUNT(*) AS TOTAL_SEARCHES,
    COUNT(DISTINCT USER_ID) AS UNIQUE_USERS
FROM SYS_SEARCH_ANALYTICS
WHERE SEARCH_DATE >= SYSDATE - 30
GROUP BY TRUNC(SEARCH_DATE)
ORDER BY SEARCH_DAY DESC;
```

---

### 2. Popular Searches Widget

**Display:**
- Top 10 search terms
- Search count
- Average results
- Trend indicator (↑↓)

**Implementation:**
```csharp
var topSearches = await _analyticsRepo.GetTopSearchesAsync(30, 10);
// Display in UI with charts
```

---

### 3. Performance Monitoring

**Alerts:**
- Average execution time > 1000ms
- Search volume spike (>200% of average)
- High failure rate (0 results)

**SQL Query Example:**
```sql
SELECT 
    AVG(EXECUTION_TIME_MS) AS AVG_TIME,
    MAX(EXECUTION_TIME_MS) AS MAX_TIME,
    COUNT(CASE WHEN RESULT_COUNT = 0 THEN 1 END) AS ZERO_RESULTS
FROM SYS_SEARCH_ANALYTICS
WHERE SEARCH_DATE >= SYSDATE - 1;
```

---

## Optimization Insights

### 1. Identify Slow Searches

```sql
SELECT 
    SEARCH_TERM,
    SEARCH_CRITERIA,
    EXECUTION_TIME_MS,
    RESULT_COUNT,
    SEARCH_DATE
FROM SYS_SEARCH_ANALYTICS
WHERE EXECUTION_TIME_MS > 1000
ORDER BY EXECUTION_TIME_MS DESC
FETCH FIRST 20 ROWS ONLY;
```

**Actions:**
- Add missing indexes
- Optimize stored procedures
- Consider caching popular searches

---

### 2. Identify Zero-Result Searches

```sql
SELECT 
    SEARCH_TERM,
    COUNT(*) AS FREQUENCY
FROM SYS_SEARCH_ANALYTICS
WHERE RESULT_COUNT = 0
    AND SEARCH_TERM IS NOT NULL
    AND SEARCH_DATE >= SYSDATE - 30
GROUP BY SEARCH_TERM
ORDER BY COUNT(*) DESC
FETCH FIRST 20 ROWS ONLY;
```

**Actions:**
- Improve search relevance
- Add synonyms/aliases
- Provide search suggestions
- Update documentation

---

### 3. Identify Complex Searches

```sql
SELECT 
    SEARCH_CRITERIA,
    EXECUTION_TIME_MS,
    RESULT_COUNT
FROM SYS_SEARCH_ANALYTICS
WHERE FILTER_LOGIC = 'OR'
    AND EXECUTION_TIME_MS > 500
ORDER BY EXECUTION_TIME_MS DESC
FETCH FIRST 20 ROWS ONLY;
```

**Actions:**
- Optimize OR logic queries
- Consider query rewriting
- Add compound indexes

---

## Data Retention

### Recommended Retention Policy

- **Hot Data:** Last 30 days (keep in main table)
- **Warm Data:** 31-90 days (archive to separate table)
- **Cold Data:** >90 days (delete or move to data warehouse)

### Archive Query Example

```sql
-- Archive old analytics data
INSERT INTO SYS_SEARCH_ANALYTICS_ARCHIVE
SELECT * FROM SYS_SEARCH_ANALYTICS
WHERE SEARCH_DATE < SYSDATE - 90;

-- Delete archived data
DELETE FROM SYS_SEARCH_ANALYTICS
WHERE SEARCH_DATE < SYSDATE - 90;

COMMIT;
```

---

## Privacy Considerations

### What to Consider

1. **User Privacy:** Search terms may contain sensitive information
2. **Data Retention:** Don't keep analytics data longer than necessary
3. **Access Control:** Restrict analytics access to authorized users
4. **Anonymization:** Consider anonymizing old data

### Recommended Practices

- Implement data retention policies
- Restrict analytics queries to AdminOnly users
- Anonymize user IDs in long-term archives
- Document analytics data usage in privacy policy

---

## Integration with Saved Searches

### Automatic Usage Tracking

When a saved search is used, the system:
1. Logs the search query (via normal analytics)
2. Increments the saved search usage count
3. Updates the last used date

This provides insights into:
- Which saved searches are most valuable
- Unused saved searches (candidates for cleanup)
- Popular search patterns to promote

---

## Future Enhancements

### Planned Features

1. **Search Suggestions API**
   - Auto-complete based on popular searches
   - Typo correction
   - Related searches

2. **Real-time Analytics Dashboard**
   - Live search metrics
   - Performance alerts
   - Trend visualization

3. **Machine Learning Integration**
   - Personalized search ranking
   - Anomaly detection
   - Predictive search

4. **Advanced Rate Limiting**
   - Per-user quotas based on search patterns
   - Throttling for complex queries
   - Abuse detection

---

## Troubleshooting

### Analytics Not Logging

**Check:**
1. Repository is registered in DependencyInjection
2. Database table and procedures exist
3. User has permissions to insert into SYS_SEARCH_ANALYTICS
4. Check application logs for warnings

### Slow Analytics Queries

**Solutions:**
1. Verify indexes exist and are being used
2. Implement data archival for old records
3. Add additional indexes for specific query patterns
4. Consider materialized views for aggregations

### High Storage Usage

**Solutions:**
1. Implement data retention policy
2. Archive old analytics data
3. Compress SEARCH_CRITERIA JSON
4. Truncate SEARCH_TERM to reasonable length

---

## Support

For questions or issues with search analytics:
1. Check application logs for errors
2. Verify database procedures are compiled
3. Review this guide for usage examples
4. Contact system administrator for access issues
