namespace ThinkOnErp.Application.DTOs.SuperAdmin
{
    /// <summary>
    /// DTO for pending requests requiring SuperAdmin action.
    /// This is a placeholder for future implementation.
    /// </summary>
    public class PendingRequestDto
    {
        public long TicketId { get; set; }
        public string CompanyNameAr { get; set; }= string.Empty;
        public string CompanyNameEn { get; set; }= string.Empty;
        public string RequestTypeAr { get; set; }= string.Empty;
        public string RequestTypeEn { get; set; }= string.Empty;
        public string Description { get; set; }= string.Empty;
        public string Priority { get; set; }= string.Empty;
        public string PriorityCode { get; set; }= string.Empty; 
        public DateTime RequestDate { get; set; }
        public string BranchNameAr { get; set; }  = string.Empty;
        public string BranchNameEn { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

