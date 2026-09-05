using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEmployeeDocumentService
{
    Task<List<EmployeeDocumentDto>> GetByEmployeeCodeAsync(string employeeCode);
    Task<EmployeeDocumentDto?> GetByIdAsync(long id);
    Task<List<DocumentExpiryReportDto>> GetExpiringDocumentsAsync(int withinDays = 30);
    Task<EmployeeDocumentDto> AddDocumentAsync(string employeeCode, CreateEmployeeDocumentDto dto, string currentUser);
    Task<bool> DeleteDocumentAsync(long id);
}
