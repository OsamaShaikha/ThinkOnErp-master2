namespace ThinkOnErp.Application.DTOs.SuperAdmin
{
    /// <summary>
    /// DTO for pending requests requiring SuperAdmin action.
    /// Displays ticket information with company, request type, priority, and date.
    /// </summary>
    public class PendingRequestDto
    {
        /// <summary>
        /// Ticket ID
        /// </summary>
        public long TicketId { get; set; }

        /// <summary>
        /// Company name in Arabic
        /// </summary>
        public string CompanyNameAr { get; set; } = string.Empty;

        /// <summary>
        /// Company name in English
        /// </summary>
        public string CompanyNameEn { get; set; } = string.Empty;

        /// <summary>
        /// Request type/title in Arabic
        /// </summary>
        public string RequestTypeAr { get; set; } = string.Empty;

        /// <summary>
        /// Request type/title in English
        /// </summary>
        public string RequestTypeEn { get; set; } = string.Empty;

        /// <summary>
        /// Request description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Priority level (High, Medium, Low)
        /// </summary>
        public string Priority { get; set; } = string.Empty;

        /// <summary>
        /// Priority code for styling (high, medium, low)
        /// </summary>
        public string PriorityCode { get; set; } = string.Empty;

        /// <summary>
        /// Request creation date
        /// </summary>
        public DateTime RequestDate { get; set; }

        /// <summary>
        /// Branch name in Arabic
        /// </summary>
        public string BranchNameAr { get; set; } = string.Empty;

        /// <summary>
        /// Branch name in English
        /// </summary>
        public string BranchNameEn { get; set; } = string.Empty;

        /// <summary>
        /// Ticket status name
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
