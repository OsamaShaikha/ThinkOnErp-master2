# Task 6.5: Request/Response Payload Capture Implementation

## Summary

Successfully implemented comprehensive request and response payload capture with size limits, sensitive data masking, and configurable logging levels in the `RequestTracingMiddleware`.

## Implementation Details

### Features Implemented

1. **Request Payload Capture**
   - ✅ Captures request body with configurable size limits (default: 10KB)
   - ✅ Masks sensitive data using `ISensitiveDataMasker`
   - ✅ Truncates payloads exceeding maximum size with indicator
   - ✅ Supports three logging levels: None, MetadataOnly, Full

2. **Response Payload Capture**
   - ✅ Captures response body using stream replacement technique
   - ✅ Masks sensitive data in responses
   - ✅ Truncates large responses with indicator
   - ✅ Detects and skips binary content (images, PDFs, etc.)
   - ✅ Only captures text-based responses (JSON, XML, HTML, plain text)

3. **Payload Logging Levels**
   - **None**: No payload logging (only metadata like status code, execution time)
   - **MetadataOnly**: Logs only size and content type (e.g., `[Metadata: Size=1234 bytes, ContentType=application/json]`)
   - **Full**: Logs complete payload with sensitive data masked and truncation applied

4. **Size Limits**
   - Configurable maximum payload size (default: 10KB, range: 1KB-1MB)
   - Payloads exceeding limit show: `[Payload too large: X bytes, max: Y bytes]`
   - Truncation indicator: `... [TRUNCATED: X characters removed]`

5. **Sensitive Data Masking**
   - Automatically masks configured sensitive fields (password, token, refreshToken, creditCard, ssn)
   - Uses `ISensitiveDataMasker.MaskSensitiveFields()` for JSON masking
   - Uses `ISensitiveDataMasker.TruncateIfNeeded()` for size enforcement

## Code Changes

### Modified Files

1. **src/ThinkOnErp.API/Middleware/RequestTracingMiddleware.cs**
   - Enhanced `InvokeAsync` to capture response body using stream replacement
   - Updated `CaptureRequestContextAsync` to support MetadataOnly logging level
   - Enhanced `CaptureRequestBodyAsync` to use `TruncateIfNeeded` after masking
   - Added `CaptureResponseContextAsync` to capture response with body
   - Added `CaptureResponseBodyAsync` to read and mask response body
   - Added `IsTextBasedContentType` to detect binary vs text responses
   - Updated `LogRequestCompletionAsync` to include response body in audit logs

### New Files

1. **tests/ThinkOnErp.API.Tests/Middleware/RequestTracingMiddlewarePayloadTests.cs**
   - Unit tests for request payload capture with masking
   - Unit tests for large payload handling
   - Unit tests for MetadataOnly logging level
   - Unit tests for None logging level
   - Unit tests for response payload capture
   - Unit tests for binary response handling

## Configuration

### appsettings.json Example

```json
{
  "RequestTracing": {
    "Enabled": true,
    "LogPayloads": true,
    "PayloadLoggingLevel": "Full",
    "MaxPayloadSize": 10240,
    "ExcludedPaths": ["/health", "/metrics", "/swagger"],
    "CorrelationIdHeader": "X-Correlation-ID",
    "IncludeHeaders": true,
    "ExcludedHeaders": ["Authorization", "Cookie", "Set-Cookie"]
  }
}
```

### Configuration Options

- **PayloadLoggingLevel**: "None" | "MetadataOnly" | "Full"
- **MaxPayloadSize**: 1024 to 1048576 bytes (1KB to 1MB)
- **LogPayloads**: true | false (master switch)

## Testing

### Test Results

All 6 unit tests passed successfully:

1. ✅ `InvokeAsync_WithSmallRequestPayload_CapturesAndMasksPayload`
2. ✅ `InvokeAsync_WithLargeRequestPayload_ReturnsPayloadTooLargeMessage`
3. ✅ `InvokeAsync_WithMetadataOnlyLevel_CapturesOnlyMetadata`
4. ✅ `InvokeAsync_WithNoneLevel_DoesNotCapturePayload`
5. ✅ `InvokeAsync_WithResponsePayload_CapturesAndMasksResponse`
6. ✅ `InvokeAsync_WithBinaryResponse_DoesNotCaptureBody`

### Test Coverage

- Request payload capture with size limits
- Response payload capture with size limits
- Sensitive data masking for both request and response
- Payload truncation with indicators
- Logging level configuration (None, MetadataOnly, Full)
- Binary content detection and skipping

## Performance Considerations

1. **Stream Replacement**: Response body capture uses stream replacement to avoid buffering entire response in memory
2. **Lazy Evaluation**: Payload capture only occurs when logging is enabled
3. **Size Limits**: Prevents memory exhaustion from large payloads
4. **Binary Detection**: Skips capturing binary content to save processing time
5. **Async Processing**: Audit logging is fire-and-forget to avoid blocking responses

## Security Features

1. **Sensitive Data Masking**: Automatically masks passwords, tokens, credit cards, SSNs
2. **Configurable Fields**: Sensitive field list is configurable via `AuditLoggingOptions`
3. **Header Exclusion**: Excludes sensitive headers (Authorization, Cookie) from logging
4. **Truncation**: Prevents logging of excessively large payloads

## Compliance

This implementation satisfies:

- **Requirement 5**: Request and Response Payload Logging
  - ✅ Log request and response payloads
  - ✅ Mask sensitive fields in logged payloads
  - ✅ Truncate payloads larger than 10KB with truncation indicator
  - ✅ Support configurable payload logging levels (None, MetadataOnly, Full)

- **Design Specification**: RequestTracingMiddleware
  - ✅ Capture request body with size limits
  - ✅ Use ISensitiveDataMasker to mask sensitive data
  - ✅ Support PayloadLoggingLevel configuration
  - ✅ Truncate payloads exceeding MaxPayloadSize

## Usage Example

### Request with Sensitive Data

**Original Request:**
```json
{
  "username": "john.doe",
  "password": "secret123",
  "email": "john@example.com"
}
```

**Logged Payload (Full Level):**
```json
{
  "username": "john.doe",
  "password": "***MASKED***",
  "email": "john@example.com"
}
```

**Logged Payload (MetadataOnly Level):**
```
[Metadata: Size=87 bytes, ContentType=application/json]
```

### Large Payload

**Original Request:** 20KB JSON payload

**Logged Payload:**
```
[Payload too large: 20480 bytes, max: 10240 bytes]
```

### Binary Response

**Original Response:** PDF file (application/pdf)

**Logged Payload:**
```
[Binary content: application/pdf, Size=524288 bytes]
```

## Next Steps

The following related tasks can now be implemented:

- Task 6.6: Implement excluded paths configuration (already implemented)
- Task 6.7: Implement automatic population of legacy fields (BUSINESS_MODULE, DEVICE_IDENTIFIER, ERROR_CODE, BUSINESS_DESCRIPTION)

## Notes

- Response body capture uses stream replacement which is the recommended approach for ASP.NET Core middleware
- The implementation properly restores the original response stream in the finally block to prevent resource leaks
- Binary content detection prevents unnecessary processing of images, PDFs, and other non-text responses
- The middleware is non-blocking and uses fire-and-forget for audit logging to maintain performance
