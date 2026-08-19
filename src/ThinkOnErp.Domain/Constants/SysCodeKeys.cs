namespace ThinkOnErp.Domain.Constants;

public static class SysCodeKeys
{
    public static class ActorTypes
    {
        public const int Mgr = 7;
        public const int User = 1;
        public const int SuperAdmin = 2;
        public const int System = 3;
        public const int Anonymous = 4;
        public const int CompanyAdmin = 5;
        public const int Admin = 6;
    }

    public static class EventCategories
    {
        public const int Mgr = 6;
        public const int Authentication = 1;
        public const int Authorization = 2;
        public const int DataChange = 3;
        public const int Configuration = 4;
        public const int Security = 5;
        public const int System = 6;
        public const int Integration = 7;
        public const int Permission = 8;
        public const int Exception = 9;
        public const int Request = 10;
    }

    public static class AuditSeverity
    {
        public const int Mgr = 5;
        public const int Info = 1;
        public const int Warning = 2;
        public const int Error = 3;
        public const int Critical = 4;
    }

    public static class DocumentCategories
    {
        public const int Mgr = 1;
        public const int Contracts = 1;
        public const int Reports = 2;
        public const int Invoices = 3;
        public const int Receipts = 4;
        public const int Identification = 5;
        public const int Certificates = 6;
        public const int Financial = 7;
        public const int HR = 8;
        public const int Legal = 9;
        public const int Technical = 10;
        public const int Marketing = 11;
        public const int Other = 12;
    }

    public static class OwnerTypes
    {
        public const int Mgr = 2;
        public const int Company = 1;
        public const int Branch = 2;
        public const int SuperAdmin = 3;
    }

    public static class ThreatTypes
    {
        public const int Mgr = 3;
        public const int BruteForceAttack = 1;
        public const int SqlInjection = 2;
        public const int CrossSiteScripting = 3;
        public const int DenialOfService = 4;
        public const int SuspiciousLogin = 5;
        public const int UnauthorizedAccess = 6;
        public const int DataExfiltration = 7;
        public const int MalwareDetected = 8;
    }

    public static class ThreatSeverity
    {
        public const int Mgr = 4;
        public const int Low = 1;
        public const int Medium = 2;
        public const int High = 3;
        public const int Critical = 4;
    }

    public static class AuditEventTypes
    {
        public const int Mgr = 8;
        public const int Request = 1;
        public const int Exception = 2;
        public const int Security = 3;
    }

    public static class PayloadLoggingLevels
    {
        public const int Mgr = 9;
        public const int None = 1;
        public const int MetadataOnly = 2;
        public const int Full = 3;
    }

    public static class HealthStatus
    {
        public const int Mgr = 10;
        public const int Healthy = 1;
        public const int Degraded = 2;
        public const int Unhealthy = 3;
        public const int Unresponsive = 4;
    }

    public static class MemoryPressure
    {
        public const int Mgr = 11;
        public const int Normal = 1;
        public const int Warning = 2;
        public const int Critical = 3;
        public const int Severe = 4;
        public const int Unknown = 5;
    }

    public static class KeyTypes
    {
        public const int Mgr = 12;
        public const int ApiKey = 1;
        public const int SigningKey = 2;
        public const int EncryptionKey = 3;
        public const int InternalKey = 4;
        public const int ExternalKey = 5;
    }

    public static class AlertTypes
    {
        public const int Mgr = 13;
        public const int Security = 1;
        public const int Performance = 2;
        public const int System = 3;
        public const int Business = 4;
    }

    public static class AuditStatus
    {
        public const int Mgr = 15;
        public const int Unresolved = 1;
        public const int InProgress = 2;
        public const int Resolved = 3;
        public const int Critical = 4;
    }

    public static class Languages
    {
        public const int Mgr = 14;
        public const int Arabic = 1;
        public const int English = 2;
    }

    public static class PartyTypes
    {
        public const int Mgr = 16;
        public const int Customer = 1;
        public const int Vendor = 2;
        public const int Employee = 3;
        public const int Other = 4;
    }
}