# External Storage Integration Tests

This directory contains comprehensive integration tests for the external storage functionality (S3 and Azure Blob Storage) used by the ArchivalService for cold storage of archived audit data.

## Test Files

### 1. ExternalStorageIntegrationTests.cs
**Primary integration tests covering end-to-end functionality:**
- Upload, download, delete operations for both S3 and Azure
- Data integrity verification with checksums
- Large file handling (1MB+ files)
- Cross-provider compatibility tests
- ArchivalService integration

### 2. ExternalStorageErrorHandlingIntegrationTests.cs
**Error handling and resilience tests:**
- Network failure scenarios
- Authentication and authorization errors
- Service unavailable conditions
- Concurrent access patterns
- Invalid configuration handling

### 3. ExternalStoragePerformanceIntegrationTests.cs
**Performance and load testing:**
- Upload/download latency measurements
- Throughput testing with concurrent operations
- Large file performance (10MB+ files)
- Memory usage monitoring
- Sustained load testing

### 4. ExternalStorageConfigurationIntegrationTests.cs
**Configuration validation and management:**
- Connection string parsing and validation
- Environment-specific configurations
- Configuration override scenarios
- Runtime configuration changes

## Running the Tests

### Mock Tests (Default)
By default, all tests run with mocked external storage providers and do not require actual S3 or Azure accounts:

```bash
# Run all external storage integration tests
dotnet test --filter "FullyQualifiedName~ExternalStorage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ExternalStorageIntegrationTests"

# Run with verbose output
dotnet test --filter "FullyQualifiedName~ExternalStorage" --logger "console;verbosity=detailed"
```

### Real External Storage Tests
To run tests against actual S3 and Azure storage services, set the following environment variables:

#### For S3 Tests:
```bash
# Enable real S3 testing
export THINKONERP_TEST_TestStorage__S3__UseReal=true

# S3 Configuration (replace with your values)
export THINKONERP_TEST_TestStorage__S3__ConnectionString="BucketName=your-test-bucket;Region=us-east-1;AccessKey=YOUR_ACCESS_KEY;SecretKey=YOUR_SECRET_KEY;Prefix=integration-tests/"

# Or use IAM roles (recommended for EC2/ECS)
export THINKONERP_TEST_TestStorage__S3__ConnectionString="BucketName=your-test-bucket;Region=us-east-1;Prefix=integration-tests/"
```

#### For Azure Tests:
```bash
# Enable real Azure testing
export THINKONERP_TEST_TestStorage__Azure__UseReal=true

# Azure Configuration (replace with your values)
export THINKONERP_TEST_TestStorage__Azure__ConnectionString="AccountName=youraccount;AccountKey=YOUR_ACCOUNT_KEY;ContainerName=test-audit-archives;Prefix=integration-tests/"

# Or use connection string format
export THINKONERP_TEST_TestStorage__Azure__ConnectionString="DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=YOUR_ACCOUNT_KEY;EndpointSuffix=core.windows.net;ContainerName=test-audit-archives;Prefix=integration-tests/"
```

#### For Performance Tests:
```bash
# Enable performance tests (requires real storage)
export THINKONERP_PERF_TEST_TestStorage__RunPerformanceTests=true
export THINKONERP_PERF_TEST_TestStorage__S3__ConnectionString="BucketName=your-perf-test-bucket;Region=us-east-1;Prefix=perf-tests/"
export THINKONERP_PERF_TEST_TestStorage__Azure__ConnectionString="AccountName=youraccount;AccountKey=YOUR_KEY;ContainerName=perf-test-archives;Prefix=perf-tests/"
```

### Running with Real Storage:
```bash
# Run with real S3 and Azure (make sure environment variables are set)
dotnet test --filter "FullyQualifiedName~ExternalStorageIntegrationTests" --logger "console;verbosity=detailed"

# Run performance tests (requires real storage)
dotnet test --filter "FullyQualifiedName~ExternalStoragePerformanceIntegrationTests" --logger "console;verbosity=detailed"
```

## Test Configuration

### S3 Connection String Format:
```
BucketName=your-bucket;Region=us-east-1[;AccessKey=key;SecretKey=secret][;Prefix=path/]
```

**Required Parameters:**
- `BucketName`: S3 bucket name
- `Region`: AWS region (e.g., us-east-1, eu-west-1)

**Optional Parameters:**
- `AccessKey`: AWS access key (if not using IAM roles)
- `SecretKey`: AWS secret key (if not using IAM roles)
- `Prefix`: Path prefix for all objects (recommended for test isolation)

### Azure Connection String Format:
```
AccountName=account;AccountKey=key;ContainerName=container[;Prefix=path/]
```

**Required Parameters:**
- `ContainerName`: Azure blob container name

**Optional Parameters:**
- `AccountName`: Storage account name
- `AccountKey`: Storage account key
- `Prefix`: Path prefix for all blobs (recommended for test isolation)

## Test Data and Cleanup

### Automatic Cleanup
All tests automatically clean up created storage objects in their `Dispose()` methods. Test objects are tracked and deleted when tests complete.

### Test Data Isolation
- All tests use unique archive IDs based on timestamps
- Tests use prefixes (`integration-tests/`, `perf-tests/`) to isolate data
- Mock tests don't create any real storage objects

### Manual Cleanup
If tests fail and don't clean up properly, you can manually delete test objects:

**S3:**
```bash
aws s3 rm s3://your-bucket/integration-tests/ --recursive
aws s3 rm s3://your-bucket/perf-tests/ --recursive
```

**Azure:**
```bash
az storage blob delete-batch --account-name youraccount --source test-audit-archives --pattern "integration-tests/*"
az storage blob delete-batch --account-name youraccount --source test-audit-archives --pattern "perf-tests/*"
```

## Performance Test Results

When running performance tests, expect output similar to:

```
S3 Upload Performance Results:
  Average Latency: 245.67ms
  P95 Latency: 456ms
  P99 Latency: 789ms
  Max Latency: 1234ms

S3 Throughput Performance Results:
  Total Time: 12.34 seconds
  Uploads per Second: 8.10
  Total Data: 1.00 MB
  Throughput: 0.81 MB/s
```

### Performance Thresholds
Tests include performance assertions that may need adjustment based on your environment:

- **Average Upload Latency**: < 1000ms
- **P95 Latency**: < 2000ms
- **P99 Latency**: < 5000ms
- **Throughput**: > 5 uploads/second, > 0.5 MB/s
- **Large File Upload**: < 60 seconds for 10MB files

## Troubleshooting

### Common Issues

1. **Authentication Errors**
   - Verify AWS credentials or IAM role permissions
   - Check Azure storage account key
   - Ensure bucket/container exists and is accessible

2. **Network Timeouts**
   - Check internet connectivity
   - Verify firewall settings
   - Consider increasing timeout values for slow networks

3. **Permission Errors**
   - S3: Ensure IAM user/role has s3:GetObject, s3:PutObject, s3:DeleteObject permissions
   - Azure: Ensure storage account key has appropriate permissions

4. **Performance Test Failures**
   - Performance thresholds may need adjustment for different environments
   - Network latency affects results significantly
   - Consider running tests multiple times for consistent results

### Debug Output
Enable detailed logging to troubleshoot issues:

```bash
export THINKONERP_TEST_Logging__LogLevel__Default=Debug
dotnet test --filter "FullyQualifiedName~ExternalStorage" --logger "console;verbosity=detailed"
```

## Security Considerations

### Credentials Management
- **Never commit real credentials to source control**
- Use environment variables or secure credential stores
- Prefer IAM roles over access keys when possible
- Use separate test accounts/buckets from production

### Test Data
- All test data is synthetic and contains no real sensitive information
- Tests use random data or mock audit log entries
- Test objects are prefixed to avoid conflicts with real data

### Network Security
- Tests may fail in environments with restrictive network policies
- Ensure outbound HTTPS access to AWS/Azure endpoints
- Consider using VPC endpoints or private endpoints for enhanced security

## Contributing

When adding new external storage integration tests:

1. Follow the existing test patterns and naming conventions
2. Include both mock and real storage test scenarios
3. Add appropriate cleanup in `Dispose()` methods
4. Document any new configuration requirements
5. Update performance thresholds if needed
6. Add error handling test cases for new scenarios

## Related Documentation

- [External Storage Implementation Summary](../../../../TASK_10_8_EXTERNAL_STORAGE_IMPLEMENTATION_SUMMARY.md)
- [ArchivalService Documentation](../../../../src/ThinkOnErp.Infrastructure/Services/README.md)
- [Configuration Guide](../../../../docs/CONFIGURATION.md)
- [Deployment Guide](../../../../DEPLOYMENT.md)