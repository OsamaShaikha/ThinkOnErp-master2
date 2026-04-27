# OpenTelemetry URI Configuration Fix

## Problem

The application was crashing on startup with the following error:

```
System.UriFormatException: Invalid URI: The format of the URI could not be determined.
at Program.<>c__DisplayClass0_0.<<Main>$>b__16(OtlpExporterOptions options) in /src/src/ThinkOnErp.API/Program.cs:line 216
```

**Root Cause**: The OpenTelemetry OTLP (OpenTelemetry Protocol) endpoint configuration was reading an empty or invalid value from configuration, and the code was attempting to create a URI from it without proper validation.

## Solution

Updated `src/ThinkOnErp.API/Program.cs` to add proper URI validation before attempting to create the endpoint:

### Before (Line ~186):
```csharp
if (!string.IsNullOrEmpty(otlpEndpoint))
{
    tracing.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);  // ❌ Crashes if otlpEndpoint is invalid
    });
}
```

### After (Line ~186):
```csharp
if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
{
    tracing.AddOtlpExporter(options =>
    {
        options.Endpoint = otlpUri;  // ✅ Only sets if valid URI
    });
}
```

The same fix was applied to both the tracing and metrics OTLP exporter configurations.

## Changes Made

1. **Added `Uri.TryCreate` validation**: Ensures the endpoint string is a valid absolute URI before attempting to use it
2. **Changed from `IsNullOrEmpty` to `IsNullOrWhiteSpace`**: Better handles whitespace-only strings
3. **Used `out var otlpUri`**: Captures the parsed URI for reuse

## How to Apply the Fix

### Option 1: Rebuild and Restart (Recommended)
```bash
chmod +x rebuild-and-restart.sh
./rebuild-and-restart.sh
```

### Option 2: Manual Steps
```bash
# Stop container
docker-compose -f docker-compose.simple.yml down

# Rebuild image (no cache to ensure changes are picked up)
docker-compose -f docker-compose.simple.yml build --no-cache

# Start container
docker-compose -f docker-compose.simple.yml up -d

# Check logs
docker logs -f thinkonerp-api
```

## Configuration (Optional)

If you want to enable OpenTelemetry OTLP export in the future, add this to your `.env` file or `appsettings.json`:

### .env
```bash
OPENTELEMETRY__OTLPENDPOINT=http://your-otlp-collector:4317
```

### appsettings.json
```json
{
  "OpenTelemetry": {
    "ServiceName": "ThinkOnErp.API",
    "ServiceVersion": "1.0.0",
    "OtlpEndpoint": "http://your-otlp-collector:4317",
    "EnableConsoleExporter": false,
    "EnablePrometheusExporter": true
  }
}
```

## Verification

After applying the fix, the application should start successfully. Verify with:

```bash
# Check container is running
docker ps | grep thinkonerp-api

# Check logs for successful startup
docker logs thinkonerp-api | grep "Now listening"

# Test API endpoint
curl http://localhost:5000/health
```

Expected output:
```json
{"status":"Healthy"}
```

## Why This Happened

The OpenTelemetry configuration was reading from `builder.Configuration["OpenTelemetry:OtlpEndpoint"]`, which returns `null` when the configuration key doesn't exist. The original code checked for `!string.IsNullOrEmpty()` but still attempted to create a URI, which failed because:

1. The configuration value was likely an empty string or whitespace
2. `IsNullOrEmpty("")` returns `true`, but the check was inverted with `!`
3. The `new Uri(otlpEndpoint)` constructor throws an exception for invalid URIs

The fix ensures that:
- Only valid, non-empty, absolute URIs are used
- Invalid or missing configuration doesn't crash the application
- The OTLP exporter is simply skipped if not properly configured

## Impact

- ✅ Application now starts successfully without OTLP endpoint configured
- ✅ Prometheus exporter still works (default enabled)
- ✅ No functionality lost - OTLP export was optional
- ✅ Can still enable OTLP export by providing valid configuration

## Related Files

- `src/ThinkOnErp.API/Program.cs` - Fixed OpenTelemetry configuration
- `rebuild-and-restart.sh` - Script to apply the fix
- `docker-compose.simple.yml` - Docker configuration
