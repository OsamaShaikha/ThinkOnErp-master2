using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SensitiveDataMasker service.
/// Tests masking of sensitive fields in JSON data.
/// </summary>
public class SensitiveDataMaskerTests
{
    private readonly ISensitiveDataMasker _masker;
    private readonly AuditLoggingOptions _options;

    public SensitiveDataMaskerTests()
    {
        _options = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "password", "token", "refreshToken", "creditCard", "ssn" },
            MaskingPattern = "***MASKED***",
            MaxPayloadSize = 100
        };

        var optionsWrapper = Options.Create(_options);
        _masker = new SensitiveDataMasker(optionsWrapper);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Mask_Password_Field()
    {
        // Arrange
        var json = "{\"username\":\"john\",\"password\":\"secret123\",\"email\":\"john@example.com\"}";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("john@example.com", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Mask_Multiple_Sensitive_Fields()
    {
        // Arrange
        var json = "{\"username\":\"john\",\"password\":\"secret123\",\"token\":\"abc123\",\"refreshToken\":\"xyz789\"}";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc123", result);
        Assert.DoesNotContain("xyz789", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Nested_Objects()
    {
        // Arrange
        var json = "{\"user\":{\"name\":\"john\",\"password\":\"secret123\"},\"settings\":{\"token\":\"abc123\"}}";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Arrays()
    {
        // Arrange
        var json = "[{\"name\":\"john\",\"password\":\"secret1\"},{\"name\":\"jane\",\"password\":\"secret2\"}]";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("jane", result);
        Assert.DoesNotContain("secret1", result);
        Assert.DoesNotContain("secret2", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Return_Original_For_Invalid_Json()
    {
        // Arrange
        var invalidJson = "not valid json {";

        // Act
        var result = _masker.MaskSensitiveFields(invalidJson);

        // Assert
        Assert.Equal(invalidJson, result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Return_Null_For_Null_Input()
    {
        // Act
        var result = _masker.MaskSensitiveFields(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Return_Empty_For_Empty_Input()
    {
        // Act
        var result = _masker.MaskSensitiveFields("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Be_Case_Insensitive()
    {
        // Arrange
        var json = "{\"USERNAME\":\"john\",\"PASSWORD\":\"secret123\",\"Token\":\"abc123\"}";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc123", result);
    }

    [Fact]
    public void TruncateIfNeeded_Should_Truncate_Long_Strings()
    {
        // Arrange
        var longString = new string('a', 150); // Longer than MaxPayloadSize (100)

        // Act
        var result = _masker.TruncateIfNeeded(longString);

        // Assert
        Assert.True(result!.Length < longString.Length);
        Assert.Contains("[TRUNCATED:", result);
        Assert.Contains("50 characters removed]", result);
    }

    [Fact]
    public void TruncateIfNeeded_Should_Not_Truncate_Short_Strings()
    {
        // Arrange
        var shortString = "short string";

        // Act
        var result = _masker.TruncateIfNeeded(shortString);

        // Assert
        Assert.Equal(shortString, result);
    }

    [Fact]
    public void TruncateIfNeeded_Should_Handle_Null_Input()
    {
        // Act
        var result = _masker.TruncateIfNeeded(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Support_Regex_Patterns()
    {
        // Arrange - Create masker with regex patterns
        var optionsWithRegex = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "password", ".*Token", "credit.*" },
            MaskingPattern = "***MASKED***"
        };
        var masker = new SensitiveDataMasker(Options.Create(optionsWithRegex));
        
        var json = "{\"username\":\"john\",\"password\":\"secret123\",\"accessToken\":\"abc123\",\"refreshToken\":\"xyz789\",\"creditCard\":\"1234-5678-9012-3456\"}";

        // Act
        var result = masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc123", result); // Matches .*Token pattern
        Assert.DoesNotContain("xyz789", result); // Matches .*Token pattern
        Assert.DoesNotContain("1234-5678-9012-3456", result); // Matches credit.* pattern
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_Credit_Card_Numbers()
    {
        // Arrange
        var text = "Payment made with card 1234-5678-9012-3456 for $100";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("1234-5678-9012-3456", result);
        Assert.Contains("$100", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_SSN()
    {
        // Arrange
        var text = "SSN: 123-45-6789 for employee John Doe";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("123-45-6789", result);
        Assert.Contains("John Doe", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_Bearer_Tokens()
    {
        // Arrange
        var text = "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("Bearer ***MASKED***", result);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_API_Keys()
    {
        // Arrange
        var text = "Connect using apikey=sk_test_1234567890abcdef or api_key=pk_live_abcdef1234567890";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("apikey=***MASKED***", result);
        Assert.Contains("api_key=***MASKED***", result);
        Assert.DoesNotContain("sk_test_1234567890abcdef", result);
        Assert.DoesNotContain("pk_live_abcdef1234567890", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Fallback_To_PlainText_For_Invalid_Json()
    {
        // Arrange
        var invalidJson = "This is not JSON but contains password=secret123 and token=abc456";

        // Act
        var result = _masker.MaskSensitiveFields(invalidJson);

        // Assert - Should mask patterns in plain text
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("abc456", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Handle_Null_Input()
    {
        // Act
        var result = _masker.MaskSensitiveInPlainText(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Handle_Empty_Input()
    {
        // Act
        var result = _masker.MaskSensitiveInPlainText("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Deeply_Nested_Objects()
    {
        // Arrange
        var json = @"{
            ""level1"": {
                ""level2"": {
                    ""level3"": {
                        ""level4"": {
                            ""password"": ""deep-secret"",
                            ""username"": ""john""
                        }
                    }
                }
            }
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("deep-secret", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Mixed_Arrays_With_Objects()
    {
        // Arrange
        var json = @"{
            ""users"": [
                {""name"": ""john"", ""password"": ""secret1""},
                {""name"": ""jane"", ""token"": ""token123""},
                {""name"": ""bob"", ""creditCard"": ""1234567890123456""}
            ]
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("jane", result);
        Assert.Contains("bob", result);
        Assert.DoesNotContain("secret1", result);
        Assert.DoesNotContain("token123", result);
        Assert.DoesNotContain("1234567890123456", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Different_Data_Types_In_Sensitive_Fields()
    {
        // Arrange - Testing that masking works even when sensitive fields contain non-string values
        var json = @"{
            ""username"": ""john"",
            ""password"": 12345,
            ""token"": true,
            ""refreshToken"": null,
            ""age"": 30
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("30", result);
        // Sensitive fields should be masked regardless of their original type
        Assert.DoesNotContain("12345", result);
        Assert.DoesNotContain("true", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Preserve_Non_Sensitive_Fields_With_Similar_Names()
    {
        // Arrange - Fields that contain "password" but aren't exactly "password"
        var json = @"{
            ""password"": ""secret123"",
            ""passwordHint"": ""your pet name"",
            ""lastPasswordChange"": ""2024-01-01"",
            ""username"": ""john""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("your pet name", result); // passwordHint should NOT be masked (exact match only)
        Assert.Contains("2024-01-01", result); // lastPasswordChange should NOT be masked
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Empty_Objects_And_Arrays()
    {
        // Arrange
        var json = @"{
            ""emptyObject"": {},
            ""emptyArray"": [],
            ""username"": ""john""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("john", result);
        Assert.Contains("emptyObject", result);
        Assert.Contains("emptyArray", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Special_Characters_In_Values()
    {
        // Arrange
        var json = @"{
            ""username"": ""john@example.com"",
            ""password"": ""p@$$w0rd!#%"",
            ""description"": ""User with special chars: <>&\""""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john@example.com", result);
        Assert.Contains("User with special chars", result);
        Assert.DoesNotContain("p@$$w0rd!#%", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_Multiple_Credit_Card_Formats()
    {
        // Arrange - Testing various credit card formats
        var text = @"
            Card 1: 1234567890123456
            Card 2: 1234-5678-9012-3456
            Card 3: 1234 5678 9012 3456
            Card 4: 1234567890123
        ";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("1234567890123456", result);
        Assert.DoesNotContain("1234-5678-9012-3456", result);
        Assert.DoesNotContain("1234 5678 9012 3456", result);
        Assert.DoesNotContain("1234567890123", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_Email_When_Configured()
    {
        // Arrange
        var optionsWithEmail = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "email", "password" },
            MaskingPattern = "***MASKED***"
        };
        var masker = new SensitiveDataMasker(Options.Create(optionsWithEmail));
        
        var text = "Contact user at john.doe@example.com or jane_smith+tag@company.co.uk for details";

        // Act
        var result = masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("john.doe@example.com", result);
        Assert.DoesNotContain("jane_smith+tag@company.co.uk", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Not_Mask_Email_When_Not_Configured()
    {
        // Arrange - Default options don't include "email"
        var text = "Contact user at john.doe@example.com for details";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("john.doe@example.com", result); // Email should NOT be masked
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Mask_Various_API_Key_Formats()
    {
        // Arrange
        var text = @"
            apikey=sk_test_1234567890
            api_key=pk_live_abcdef
            api-key=secret_key_xyz
            key=simple_key_123
            secret=my_secret_value
        ";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("sk_test_1234567890", result);
        Assert.DoesNotContain("pk_live_abcdef", result);
        Assert.DoesNotContain("secret_key_xyz", result);
        Assert.DoesNotContain("simple_key_123", result);
        Assert.DoesNotContain("my_secret_value", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Unicode_Characters()
    {
        // Arrange
        var json = @"{
            ""username"": ""用户名"",
            ""password"": ""密码123"",
            ""description"": ""Ñoño's café""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("用户名", result);
        Assert.Contains("Ñoño's café", result);
        Assert.DoesNotContain("密码123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Very_Large_JSON_Objects()
    {
        // Arrange - Create a large JSON with many fields
        var fields = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            fields.Add($"\"field{i}\": \"value{i}\"");
        }
        fields.Add("\"password\": \"secret123\"");
        var json = "{" + string.Join(",", fields) + "}";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("secret123", result);
        // Verify some non-sensitive fields are preserved
        Assert.Contains("value0", result);
        Assert.Contains("value99", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Arrays_Of_Primitives()
    {
        // Arrange
        var json = @"{
            ""numbers"": [1, 2, 3, 4, 5],
            ""strings"": [""a"", ""b"", ""c""],
            ""booleans"": [true, false, true],
            ""password"": ""secret123""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("1", result);
        Assert.Contains("\"a\"", result);
        Assert.Contains("true", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Complex_Regex_Patterns()
    {
        // Arrange - Test complex regex patterns
        var optionsWithComplexRegex = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "^auth.*", ".*[Tt]oken$", "secret_.*" },
            MaskingPattern = "***MASKED***"
        };
        var masker = new SensitiveDataMasker(Options.Create(optionsWithComplexRegex));
        
        var json = @"{
            ""authKey"": ""key123"",
            ""authToken"": ""token456"",
            ""accessToken"": ""access789"",
            ""refreshtoken"": ""refresh012"",
            ""secret_key"": ""secret345"",
            ""secret_value"": ""value678"",
            ""normalField"": ""normal""
        }";

        // Act
        var result = masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("normal", result); // Non-sensitive field preserved
        Assert.DoesNotContain("key123", result); // Matches ^auth.*
        Assert.DoesNotContain("token456", result); // Matches ^auth.*
        Assert.DoesNotContain("access789", result); // Matches .*[Tt]oken$
        Assert.DoesNotContain("refresh012", result); // Matches .*[Tt]oken$
        Assert.DoesNotContain("secret345", result); // Matches secret_.*
        Assert.DoesNotContain("value678", result); // Matches secret_.*
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Invalid_Regex_Patterns_Gracefully()
    {
        // Arrange - Test with invalid regex pattern
        var optionsWithInvalidRegex = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "password", "[invalid(regex" }, // Invalid regex
            MaskingPattern = "***MASKED***"
        };
        var masker = new SensitiveDataMasker(Options.Create(optionsWithInvalidRegex));
        
        var json = @"{""username"": ""john"", ""password"": ""secret123""}";

        // Act - Should not throw exception
        var result = masker.MaskSensitiveFields(json);

        // Assert - Should still mask valid patterns
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void TruncateIfNeeded_Should_Handle_Exact_MaxPayloadSize()
    {
        // Arrange
        var exactSizeString = new string('a', 100); // Exactly MaxPayloadSize

        // Act
        var result = _masker.TruncateIfNeeded(exactSizeString);

        // Assert
        Assert.Equal(exactSizeString, result); // Should not truncate
        Assert.DoesNotContain("[TRUNCATED:", result);
    }

    [Fact]
    public void TruncateIfNeeded_Should_Handle_One_Character_Over_Limit()
    {
        // Arrange
        var slightlyOverString = new string('a', 101); // One character over MaxPayloadSize

        // Act
        var result = _masker.TruncateIfNeeded(slightlyOverString);

        // Assert
        Assert.True(result!.Length < slightlyOverString.Length);
        Assert.Contains("[TRUNCATED:", result);
        Assert.Contains("1 characters removed]", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Preserve_Non_Sensitive_Numbers()
    {
        // Arrange - Numbers that look like credit cards but aren't (too short/long)
        var text = "Order ID: 12345, Amount: 1234567, Phone: 123-456-7890";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("12345", result); // Too short to be credit card
        Assert.Contains("1234567", result); // Too short to be credit card
        Assert.Contains("123-456-7890", result); // Phone number, not SSN format
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Handle_Multiple_Patterns_In_Same_Text()
    {
        // Arrange
        var text = @"
            User SSN: 123-45-6789
            Credit Card: 1234-5678-9012-3456
            API Key: apikey=sk_test_abc123
            Bearer Token: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
        ";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.DoesNotContain("123-45-6789", result);
        Assert.DoesNotContain("1234-5678-9012-3456", result);
        Assert.DoesNotContain("sk_test_abc123", result);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Null_Values_In_JSON()
    {
        // Arrange
        var json = @"{
            ""username"": ""john"",
            ""password"": null,
            ""token"": null,
            ""email"": ""john@example.com""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("john", result);
        Assert.Contains("john@example.com", result);
        // Null sensitive fields should still be masked
        Assert.Contains("***MASKED***", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Whitespace_In_JSON()
    {
        // Arrange - JSON with various whitespace
        var json = @"
        {
            ""username""  :  ""john""  ,
            ""password""  :  ""secret123""  ,
            ""email""  :  ""john@example.com""
        }
        ";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Escaped_Characters_In_JSON()
    {
        // Arrange
        var json = @"{
            ""username"": ""john\ndoe"",
            ""password"": ""secret\t123\r\n"",
            ""path"": ""C:\\Users\\John\\Documents""
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("Documents", result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Use_Custom_Masking_Pattern()
    {
        // Arrange
        var customOptions = new AuditLoggingOptions
        {
            SensitiveFields = new[] { "password" },
            MaskingPattern = "[REDACTED]"
        };
        var masker = new SensitiveDataMasker(Options.Create(customOptions));
        
        var json = @"{""username"": ""john"", ""password"": ""secret123""}";

        // Act
        var result = masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void MaskSensitiveInPlainText_Should_Handle_URL_Query_Strings()
    {
        // Arrange
        var text = "https://api.example.com/users?apikey=sk_test_123&token=abc456&user=john";

        // Act
        var result = _masker.MaskSensitiveInPlainText(text);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("user=john", result); // Non-sensitive parameter preserved
        Assert.DoesNotContain("sk_test_123", result);
        Assert.DoesNotContain("abc456", result);
    }

    [Fact]
    public void MaskSensitiveFields_Should_Handle_Circular_Reference_Prevention()
    {
        // Arrange - JSON that could cause issues if not handled properly
        var json = @"{
            ""user"": {
                ""name"": ""john"",
                ""password"": ""secret123"",
                ""profile"": {
                    ""bio"": ""Developer"",
                    ""credentials"": {
                        ""token"": ""token456""
                    }
                }
            }
        }";

        // Act
        var result = _masker.MaskSensitiveFields(json);

        // Assert
        Assert.Contains("***MASKED***", result);
        Assert.Contains("john", result);
        Assert.Contains("Developer", result);
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("token456", result);
    }
}
