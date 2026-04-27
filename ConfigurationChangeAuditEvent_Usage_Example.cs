using ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Usage example demonstrating ConfigurationChangeAuditEvent functionality
/// </summary>
public class ConfigurationChangeAuditEventUsageExample
{
    public static void DemonstrateUsage()
    {
        // Example 1: Environment variable change
        var envVarChange = new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SYSTEM",
            ActorId = 0, // System actor
            CompanyId = null, // System-wide change
            BranchId = null,
            Action = "UPDATE",
            EntityType = "Configuration",
            EntityId = null,
            IpAddress = "127.0.0.1",
            UserAgent = "System Process",
            SettingName = "DATABASE_CONNECTION_TIMEOUT",
            OldValue = "30",
            NewValue = "60",
            Source = "EnvironmentVariable"
        };

        // Example 2: Config file change by admin
        var configFileChange = new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "COMPANY_ADMIN",
            ActorId = 123,
            CompanyId = 456,
            BranchId = 789,
            Action = "UPDATE",
            EntityType = "Configuration",
            EntityId = null,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            SettingName = "MaxLoginAttempts",
            OldValue = "3",
            NewValue = "5",
            Source = "ConfigFile"
        };

        // Example 3: Database configuration change
        var dbConfigChange = new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SUPER_ADMIN",
            ActorId = 1,
            CompanyId = null, // Super admin can make system-wide changes
            BranchId = null,
            Action = "INSERT",
            EntityType = "Configuration",
            EntityId = null,
            IpAddress = "10.0.0.50",
            UserAgent = "Admin Dashboard v2.1",
            SettingName = "EnableAuditLogging",
            OldValue = null, // New setting
            NewValue = "true",
            Source = "Database"
        };

        // Example 4: Feature flag toggle
        var featureFlagChange = new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "COMPANY_ADMIN",
            ActorId = 456,
            CompanyId = 789,
            BranchId = null, // Company-wide feature flag
            Action = "UPDATE",
            EntityType = "FeatureFlag",
            EntityId = null,
            IpAddress = "172.16.0.25",
            UserAgent = "Feature Management Portal",
            SettingName = "EnableAdvancedReporting",
            OldValue = "false",
            NewValue = "true",
            Source = "Database"
        };

        // Example 5: Configuration deletion
        var configDeletion = new ConfigurationChangeAuditEvent
        {
            CorrelationId = Guid.NewGuid().ToString(),
            ActorType = "SUPER_ADMIN",
            ActorId = 1,
            CompanyId = null,
            BranchId = null,
            Action = "DELETE",
            EntityType = "Configuration",
            EntityId = null,
            IpAddress = "10.0.0.50",
            UserAgent = "Admin Dashboard v2.1",
            SettingName = "ObsoleteFeatureFlag",
            OldValue = "false",
            NewValue = null, // Setting removed
            Source = "Database"
        };

        Console.WriteLine("ConfigurationChangeAuditEvent examples created successfully!");
        Console.WriteLine($"Environment Variable Change: {envVarChange.SettingName} from {envVarChange.OldValue} to {envVarChange.NewValue}");
        Console.WriteLine($"Config File Change: {configFileChange.SettingName} from {configFileChange.OldValue} to {configFileChange.NewValue}");
        Console.WriteLine($"Database Config Change: {dbConfigChange.SettingName} = {dbConfigChange.NewValue} (new setting)");
        Console.WriteLine($"Feature Flag Change: {featureFlagChange.SettingName} from {featureFlagChange.OldValue} to {featureFlagChange.NewValue}");
        Console.WriteLine($"Config Deletion: {configDeletion.SettingName} removed (was {configDeletion.OldValue})");
    }
}