using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for sensitive data masking in audit logs.
/// Validates that all sensitive fields are properly masked before storage.
/// 
/// **Validates: Requirements 1.5, 5.3**
/// 
/// Property 3: Sensitive Data Masking
/// FOR ALL audit log entries containing sensitive fields (password, token, refreshToken, 
/// creditCard, ssn, etc.), those fields SHALL be masked with the configured masking pattern 
/// before storage.
/// </summary>
public class SensitiveDataMaskingPropertyTests
{
    private const int MinIterations = 100;
    private readonly ISensitiveDataMasker _masker;
    private readonly AuditLoggingOptions _options;

    public SensitiveDataMaskingPropertyTests()
    {
        _options = new AuditLoggingOptions
        {
            SensitiveFields = new[] 
            { 
                "password", 
                "token", 
                "refreshToken", 
                "creditCard", 
                "ssn",
                "apiKey",
                "secret",
                "privateKey",
                "accessToken",
                "authToken"
            },
            MaskingPattern = "***MASKED***",
            MaxPayloadSize = 10240
        };

        var optionsWrapper = Options.Create(_options);
        _masker = new SensitiveDataMasker(optionsWrapper);
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 5.3**
    /// 
    /// Property 3: Sensitive Data Masking
    /// 
    /// FOR ALL audit log entries containing sensitive fields (password, token, refreshToken, 
    /// creditCard, ssn, etc.), those fields SHALL be masked with the configured masking pattern 
    /// before storage.
    /// 
    /// This property verifies that:
    /// 1. All configured sensitive field names are masked in JSON objects
    /// 2. Sensitive field values are replaced with the masking pattern
    /// 3. Non-sensitive fields remain unchanged
    /// 4. Masking works recursively in nested objects
    /// 5. Masking works in arrays of objects
    /// 6. Masking is case-insensitive
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllAuditLogEntries_SensitiveFieldsAreMasked(AuditLogData auditData)
    {
        // Arrange: Create JSON from audit data
        var jsonObject = new Dictionary<string, object?>
        {
            ["id"] = auditData.Id,
            ["entityType"] = auditData.EntityType,
            ["action"] = auditData.Action,
            ["timestamp"] = auditData.Timestamp.ToString("O"),
            ["actorId"] = auditData.ActorId,
            ["companyId"] = auditData.CompanyId
        };

        // Add sensitive fields to the JSON
        foreach (var sensitiveField in auditData.SensitiveFields)
        {
            jsonObject[sensitiveField.Key] = sensitiveField.Value;
        }

        // Add non-sensitive fields to the JSON
        foreach (var nonSensitiveField in auditData.NonSensitiveFields)
        {
            jsonObject[nonSensitiveField.Key] = nonSensitiveField.Value;
        }

        var json = JsonSerializer.Serialize(jsonObject);

        // Act: Mask sensitive fields
        var maskedJson = _masker.MaskSensitiveFields(json);

        // Assert: Parse the masked JSON
        Assert.NotNull(maskedJson);
        var maskedObject = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(maskedJson!);
        Assert.NotNull(maskedObject);

        // Property 1: All sensitive fields must be masked
        var allSensitiveFieldsMasked = true;
        var unmaskedSensitiveFields = new List<string>();

        foreach (var sensitiveField in auditData.SensitiveFields)
        {
            if (maskedObject!.TryGetValue(sensitiveField.Key, out var value))
            {
                var stringValue = value.GetString();
                if (stringValue != _options.MaskingPattern)
                {
                    allSensitiveFieldsMasked = false;
                    unmaskedSensitiveFields.Add(sensitiveField.Key);
                }
            }
        }

        // Property 2: No sensitive values should appear in the masked JSON
        var noSensitiveValuesInOutput = true;
        var foundSensitiveValues = new List<string>();

        foreach (var sensitiveField in auditData.SensitiveFields)
        {
            if (maskedJson!.Contains(sensitiveField.Value))
            {
                noSensitiveValuesInOutput = false;
                foundSensitiveValues.Add($"{sensitiveField.Key}={sensitiveField.Value}");
            }
        }

        // Property 3: All non-sensitive fields must remain unchanged
        var allNonSensitiveFieldsPreserved = true;
        var changedNonSensitiveFields = new List<string>();

        foreach (var nonSensitiveField in auditData.NonSensitiveFields)
        {
            if (maskedObject!.TryGetValue(nonSensitiveField.Key, out var value))
            {
                var stringValue = value.GetString();
                if (stringValue != nonSensitiveField.Value)
                {
                    allNonSensitiveFieldsPreserved = false;
                    changedNonSensitiveFields.Add(nonSensitiveField.Key);
                }
            }
        }

        // Property 4: Standard fields (id, entityType, action, etc.) must be preserved
        var standardFieldsPreserved =
            maskedObject!.ContainsKey("id") &&
            maskedObject.ContainsKey("entityType") &&
            maskedObject.ContainsKey("action") &&
            maskedObject.ContainsKey("timestamp") &&
            maskedObject.ContainsKey("actorId") &&
            maskedObject.ContainsKey("companyId");

        // Property 5: The masking pattern must appear in the output for each sensitive field
        var maskingPatternCount = CountOccurrences(maskedJson!, _options.MaskingPattern);
        var expectedMaskingPatternCount = auditData.SensitiveFields.Count;
        var correctMaskingPatternCount = maskingPatternCount == expectedMaskingPatternCount;

        // Combine all properties
        var result = allSensitiveFieldsMasked
            && noSensitiveValuesInOutput
            && allNonSensitiveFieldsPreserved
            && standardFieldsPreserved
            && correctMaskingPatternCount;

        return result
            .Label($"All sensitive fields masked: {allSensitiveFieldsMasked}")
            .Label($"Unmasked sensitive fields: {string.Join(", ", unmaskedSensitiveFields)}")
            .Label($"No sensitive values in output: {noSensitiveValuesInOutput}")
            .Label($"Found sensitive values: {string.Join(", ", foundSensitiveValues)}")
            .Label($"All non-sensitive fields preserved: {allNonSensitiveFieldsPreserved}")
            .Label($"Changed non-sensitive fields: {string.Join(", ", changedNonSensitiveFields)}")
            .Label($"Standard fields preserved: {standardFieldsPreserved}")
            .Label($"Correct masking pattern count: {correctMaskingPatternCount} (expected: {expectedMaskingPatternCount}, actual: {maskingPatternCount})")
            .Label($"Sensitive fields count: {auditData.SensitiveFields.Count}")
            .Label($"Non-sensitive fields count: {auditData.NonSensitiveFields.Count}");
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 5.3**
    /// 
    /// Property 3: Sensitive Data Masking (Nested Objects)
    /// 
    /// FOR ALL audit log entries with nested objects containing sensitive fields, 
    /// those fields SHALL be masked recursively at all nesting levels.
    /// 
    /// This property verifies that:
    /// 1. Sensitive fields in nested objects are masked
    /// 2. Sensitive fields at multiple nesting levels are all masked
    /// 3. Non-sensitive fields in nested objects remain unchanged
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllNestedObjects_SensitiveFieldsAreMaskedRecursively(NestedAuditData nestedData)
    {
        // Arrange: Create nested JSON structure
        var jsonObject = new Dictionary<string, object?>
        {
            ["id"] = nestedData.Id,
            ["entityType"] = nestedData.EntityType,
            ["user"] = new Dictionary<string, object?>
            {
                ["username"] = nestedData.Username,
                ["password"] = nestedData.Password,
                ["email"] = nestedData.Email
            },
            ["authentication"] = new Dictionary<string, object?>
            {
                ["token"] = nestedData.Token,
                ["refreshToken"] = nestedData.RefreshToken,
                ["expiresAt"] = nestedData.ExpiresAt.ToString("O")
            },
            ["metadata"] = new Dictionary<string, object?>
            {
                ["ipAddress"] = nestedData.IpAddress,
                ["userAgent"] = nestedData.UserAgent
            }
        };

        var json = JsonSerializer.Serialize(jsonObject);

        // Act: Mask sensitive fields
        var maskedJson = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.NotNull(maskedJson);

        // Property 1: Sensitive values must not appear in masked JSON
        var passwordNotInOutput = !maskedJson!.Contains(nestedData.Password);
        var tokenNotInOutput = !maskedJson.Contains(nestedData.Token);
        var refreshTokenNotInOutput = !maskedJson.Contains(nestedData.RefreshToken);

        // Property 2: Non-sensitive values must be preserved
        var usernameInOutput = maskedJson.Contains(nestedData.Username);
        var emailInOutput = maskedJson.Contains(nestedData.Email);
        var ipAddressInOutput = maskedJson.Contains(nestedData.IpAddress);
        var userAgentInOutput = maskedJson.Contains(nestedData.UserAgent);

        // Property 3: Masking pattern must appear for each sensitive field
        var maskingPatternCount = CountOccurrences(maskedJson, _options.MaskingPattern);
        var expectedCount = 3; // password, token, refreshToken
        var correctMaskingCount = maskingPatternCount == expectedCount;

        // Property 4: JSON structure must be valid
        var isValidJson = true;
        try
        {
            JsonDocument.Parse(maskedJson);
        }
        catch (JsonException)
        {
            isValidJson = false;
        }

        // Combine all properties
        var result = passwordNotInOutput
            && tokenNotInOutput
            && refreshTokenNotInOutput
            && usernameInOutput
            && emailInOutput
            && ipAddressInOutput
            && userAgentInOutput
            && correctMaskingCount
            && isValidJson;

        return result
            .Label($"Password not in output: {passwordNotInOutput}")
            .Label($"Token not in output: {tokenNotInOutput}")
            .Label($"RefreshToken not in output: {refreshTokenNotInOutput}")
            .Label($"Username in output: {usernameInOutput}")
            .Label($"Email in output: {emailInOutput}")
            .Label($"IP address in output: {ipAddressInOutput}")
            .Label($"User agent in output: {userAgentInOutput}")
            .Label($"Correct masking count: {correctMaskingCount} (expected: {expectedCount}, actual: {maskingPatternCount})")
            .Label($"Valid JSON: {isValidJson}");
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 5.3**
    /// 
    /// Property 3: Sensitive Data Masking (Arrays)
    /// 
    /// FOR ALL audit log entries with arrays containing objects with sensitive fields, 
    /// those fields SHALL be masked in all array elements.
    /// 
    /// This property verifies that:
    /// 1. Sensitive fields are masked in all array elements
    /// 2. Non-sensitive fields in array elements remain unchanged
    /// 3. Array structure is preserved
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllArrays_SensitiveFieldsAreMaskedInAllElements(ArrayAuditData arrayData)
    {
        // Arrange: Create JSON with array of objects
        var users = arrayData.Users.Select(u => new Dictionary<string, object?>
        {
            ["id"] = u.Id,
            ["username"] = u.Username,
            ["password"] = u.Password,
            ["email"] = u.Email,
            ["token"] = u.Token
        }).ToList();

        var jsonObject = new Dictionary<string, object?>
        {
            ["entityType"] = "UserBatch",
            ["action"] = "CREATE",
            ["users"] = users
        };

        var json = JsonSerializer.Serialize(jsonObject);

        // Act: Mask sensitive fields
        var maskedJson = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.NotNull(maskedJson);

        // Property 1: No sensitive values should appear in output
        var allPasswordsMasked = arrayData.Users.All(u => !maskedJson!.Contains(u.Password));
        var allTokensMasked = arrayData.Users.All(u => !maskedJson!.Contains(u.Token));

        // Property 2: All non-sensitive values should be preserved
        var allUsernamesPreserved = arrayData.Users.All(u => maskedJson!.Contains(u.Username));
        var allEmailsPreserved = arrayData.Users.All(u => maskedJson!.Contains(u.Email));

        // Property 3: Masking pattern count should match sensitive fields count
        var maskingPatternCount = CountOccurrences(maskedJson!, _options.MaskingPattern);
        var expectedCount = arrayData.Users.Count * 2; // password + token per user
        var correctMaskingCount = maskingPatternCount == expectedCount;

        // Property 4: Array length should be preserved
        var maskedObject = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(maskedJson!);
        var arrayLengthPreserved = maskedObject!["users"].GetArrayLength() == arrayData.Users.Count;

        // Combine all properties
        var result = allPasswordsMasked
            && allTokensMasked
            && allUsernamesPreserved
            && allEmailsPreserved
            && correctMaskingCount
            && arrayLengthPreserved;

        return result
            .Label($"All passwords masked: {allPasswordsMasked}")
            .Label($"All tokens masked: {allTokensMasked}")
            .Label($"All usernames preserved: {allUsernamesPreserved}")
            .Label($"All emails preserved: {allEmailsPreserved}")
            .Label($"Correct masking count: {correctMaskingCount} (expected: {expectedCount}, actual: {maskingPatternCount})")
            .Label($"Array length preserved: {arrayLengthPreserved}")
            .Label($"User count: {arrayData.Users.Count}");
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 5.3**
    /// 
    /// Property 3: Sensitive Data Masking (Case Insensitivity)
    /// 
    /// FOR ALL audit log entries containing sensitive fields with varying case 
    /// (Password, PASSWORD, password), those fields SHALL be masked regardless of case.
    /// 
    /// This property verifies that:
    /// 1. Sensitive field names are matched case-insensitively
    /// 2. Mixed case sensitive field names are all masked
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property ForAllCaseVariations_SensitiveFieldsAreMasked(CaseVariationData caseData)
    {
        // Arrange: Create JSON with various case variations
        var jsonObject = new Dictionary<string, object?>
        {
            ["id"] = caseData.Id,
            [caseData.PasswordFieldName] = caseData.PasswordValue,
            [caseData.TokenFieldName] = caseData.TokenValue,
            ["username"] = caseData.Username
        };

        var json = JsonSerializer.Serialize(jsonObject);

        // Act: Mask sensitive fields
        var maskedJson = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.NotNull(maskedJson);

        // Property 1: Sensitive values must not appear in output
        var passwordNotInOutput = !maskedJson!.Contains(caseData.PasswordValue);
        var tokenNotInOutput = !maskedJson.Contains(caseData.TokenValue);

        // Property 2: Non-sensitive values must be preserved
        var usernameInOutput = maskedJson.Contains(caseData.Username);

        // Property 3: Masking pattern must appear
        var maskingPatternCount = CountOccurrences(maskedJson, _options.MaskingPattern);
        var hasMaskingPattern = maskingPatternCount >= 2; // At least password and token

        // Combine all properties
        var result = passwordNotInOutput
            && tokenNotInOutput
            && usernameInOutput
            && hasMaskingPattern;

        return result
            .Label($"Password not in output: {passwordNotInOutput}")
            .Label($"Token not in output: {tokenNotInOutput}")
            .Label($"Username in output: {usernameInOutput}")
            .Label($"Has masking pattern: {hasMaskingPattern} (count: {maskingPatternCount})")
            .Label($"Password field name: {caseData.PasswordFieldName}")
            .Label($"Token field name: {caseData.TokenFieldName}");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return 0;

        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary audit log data with sensitive and non-sensitive fields.
        /// </summary>
        public static Arbitrary<AuditLogData> AuditLogData()
        {
            var dataGenerator =
                from id in Gen.Choose(1, 100000).Select(i => (long)i)
                from entityType in Gen.Elements("User", "Company", "Branch", "Invoice", "Payment")
                from action in Gen.Elements("INSERT", "UPDATE", "DELETE")
                from timestamp in Gen.Elements(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(-1))
                from actorId in Gen.Choose(1, 1000).Select(i => (long)i)
                from companyId in Gen.Choose(1, 100).Select(i => (long)i)
                from sensitiveFieldCount in Gen.Choose(1, 5)
                from nonSensitiveFieldCount in Gen.Choose(1, 5)
                select CreateAuditLogData(
                    id,
                    entityType,
                    action,
                    timestamp,
                    actorId,
                    companyId,
                    sensitiveFieldCount,
                    nonSensitiveFieldCount);

            return Arb.From(dataGenerator);
        }

        /// <summary>
        /// Generates nested audit data with sensitive fields at multiple levels.
        /// </summary>
        public static Arbitrary<NestedAuditData> NestedAuditData()
        {
            var dataGenerator =
                from id in Gen.Choose(1, 100000).Select(i => (long)i)
                from entityType in Gen.Elements("Authentication", "UserSession", "ApiAccess")
                from username in Gen.Elements("john_doe", "jane_smith", "admin_user", "test_user")
                from password in Gen.Elements("P@ssw0rd123", "SecretPass456", "MyPassword789")
                from email in Gen.Elements("john@example.com", "jane@test.com", "admin@company.com")
                from token in Gen.Elements("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", "abc123def456", "token_xyz789")
                from refreshToken in Gen.Elements("refresh_abc123", "refresh_xyz789", "refresh_def456")
                from expiresAt in Gen.Elements(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1))
                from ipAddress in Gen.Elements("192.168.1.100", "10.0.0.50", "172.16.0.25")
                from userAgent in Gen.Elements("Mozilla/5.0", "Chrome/91.0", "Safari/14.0")
                select new ThinkOnErp.Infrastructure.Tests.Services.NestedAuditData
                {
                    Id = id,
                    EntityType = entityType,
                    Username = username,
                    Password = password,
                    Email = email,
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

            return Arb.From(dataGenerator);
        }

        /// <summary>
        /// Generates array audit data with multiple users containing sensitive fields.
        /// </summary>
        public static Arbitrary<ArrayAuditData> ArrayAuditData()
        {
            var userGenerator =
                from id in Gen.Choose(1, 10000).Select(i => (long)i)
                from username in Gen.Elements("user1", "user2", "user3", "admin", "test")
                from password in Gen.Elements("pass123", "secret456", "pwd789")
                from email in Gen.Elements("user1@test.com", "user2@test.com", "admin@test.com")
                from token in Gen.Elements("token_abc", "token_xyz", "token_123")
                select new UserData
                {
                    Id = id,
                    Username = username,
                    Password = password,
                    Email = email,
                    Token = token
                };

            var dataGenerator =
                from userCount in Gen.Choose(2, 10)
                from users in Gen.ListOf(userCount, userGenerator)
                select new ThinkOnErp.Infrastructure.Tests.Services.ArrayAuditData
                {
                    Users = users.ToList()
                };

            return Arb.From(dataGenerator);
        }

        /// <summary>
        /// Generates case variation data for testing case-insensitive masking.
        /// </summary>
        public static Arbitrary<CaseVariationData> CaseVariationData()
        {
            var dataGenerator =
                from id in Gen.Choose(1, 100000).Select(i => (long)i)
                from passwordCase in Gen.Elements("password", "Password", "PASSWORD", "PaSsWoRd")
                from tokenCase in Gen.Elements("token", "Token", "TOKEN", "ToKeN")
                from passwordValue in Gen.Elements("secret123", "mypassword", "pwd456")
                from tokenValue in Gen.Elements("abc123", "xyz789", "token_value")
                from username in Gen.Elements("john", "jane", "admin")
                select new ThinkOnErp.Infrastructure.Tests.Services.CaseVariationData
                {
                    Id = id,
                    PasswordFieldName = passwordCase,
                    TokenFieldName = tokenCase,
                    PasswordValue = passwordValue,
                    TokenValue = tokenValue,
                    Username = username
                };

            return Arb.From(dataGenerator);
        }

        private static ThinkOnErp.Infrastructure.Tests.Services.AuditLogData CreateAuditLogData(
            long id,
            string entityType,
            string action,
            DateTime timestamp,
            long actorId,
            long companyId,
            int sensitiveFieldCount,
            int nonSensitiveFieldCount)
        {
            var sensitiveFieldNames = new[] { "password", "token", "refreshToken", "creditCard", "ssn", "apiKey", "secret" };
            var nonSensitiveFieldNames = new[] { "username", "email", "firstName", "lastName", "phoneNumber", "address", "city" };

            var sensitiveFields = new Dictionary<string, string>();
            for (int i = 0; i < Math.Min(sensitiveFieldCount, sensitiveFieldNames.Length); i++)
            {
                sensitiveFields[sensitiveFieldNames[i]] = $"sensitive_value_{i}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            var nonSensitiveFields = new Dictionary<string, string>();
            for (int i = 0; i < Math.Min(nonSensitiveFieldCount, nonSensitiveFieldNames.Length); i++)
            {
                nonSensitiveFields[nonSensitiveFieldNames[i]] = $"value_{i}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            return new ThinkOnErp.Infrastructure.Tests.Services.AuditLogData
            {
                Id = id,
                EntityType = entityType,
                Action = action,
                Timestamp = timestamp,
                ActorId = actorId,
                CompanyId = companyId,
                SensitiveFields = sensitiveFields,
                NonSensitiveFields = nonSensitiveFields
            };
        }
    }
}

/// <summary>
/// Represents audit log data for property-based testing.
/// </summary>
public class AuditLogData
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long ActorId { get; set; }
    public long CompanyId { get; set; }
    public Dictionary<string, string> SensitiveFields { get; set; } = new();
    public Dictionary<string, string> NonSensitiveFields { get; set; } = new();
}

/// <summary>
/// Represents nested audit data for property-based testing.
/// </summary>
public class NestedAuditData
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

/// <summary>
/// Represents array audit data for property-based testing.
/// </summary>
public class ArrayAuditData
{
    public List<UserData> Users { get; set; } = new();
}

/// <summary>
/// Represents user data for array testing.
/// </summary>
public class UserData
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Represents case variation data for property-based testing.
/// </summary>
public class CaseVariationData
{
    public long Id { get; set; }
    public string PasswordFieldName { get; set; } = string.Empty;
    public string TokenFieldName { get; set; } = string.Empty;
    public string PasswordValue { get; set; } = string.Empty;
    public string TokenValue { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
