using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class EmployeeDocumentService : IEmployeeDocumentService
{
    private readonly IEmployeeDocumentRepository _documentRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ILogger<EmployeeDocumentService> _logger;

    public EmployeeDocumentService(
        IEmployeeDocumentRepository documentRepository,
        IEmployeeRepository employeeRepository,
        ILogger<EmployeeDocumentService> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<EmployeeDocumentDto>> GetByEmployeeCodeAsync(string employeeCode)
    {
        var list = await _documentRepository.GetByEmployeeCodeAsync(employeeCode);
        return list.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDocumentDto?> GetByIdAsync(long id)
    {
        var doc = await _documentRepository.GetByIdAsync(id);
        return doc == null ? null : MapToDto(doc);
    }

    public async Task<List<DocumentExpiryReportDto>> GetExpiringDocumentsAsync(int withinDays = 30)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(withinDays);
        var docs = await _documentRepository.GetExpiringDocumentsAsync(thresholdDate);
        var now = DateTime.UtcNow;

        return docs.Select(d => new DocumentExpiryReportDto
        {
            DocumentId = d.Id,
            EmployeeCode = d.EmployeeCode,
            EmployeeNameAr = d.Employee?.NameAr ?? string.Empty,
            EmployeeNameEn = d.Employee?.NameEn ?? string.Empty,
            DepartmentName = d.Employee?.Department?.NameEn,
            DocumentType = d.DocumentType,
            DocumentNumber = d.DocumentNumber,
            ExpiryDate = d.ExpiryDate,
            DaysUntilExpiry = d.ExpiryDate.HasValue ? (int)(d.ExpiryDate.Value.Date - now.Date).TotalDays : 0,
            IsExpired = d.ExpiryDate.HasValue && d.ExpiryDate.Value.Date < now.Date
        }).ToList();
    }

    public async Task<EmployeeDocumentDto> AddDocumentAsync(string employeeCode, CreateEmployeeDocumentDto dto, string currentUser)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var document = new EmployeeDocument
        {
            EmployeeCode = employeeCode,
            DocumentType = dto.DocumentType.Trim().ToUpperInvariant(),
            DocumentNumber = dto.DocumentNumber?.Trim(),
            FileReference = dto.FileReference.Trim(),
            FileName = dto.FileName?.Trim(),
            IssuedDate = dto.IssuedDate,
            ExpiryDate = dto.ExpiryDate,
            Notes = dto.Notes,
            IsActive = true,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        await _documentRepository.AddAsync(document);
        await _documentRepository.SaveChangesAsync();

        _logger.LogInformation("Added document {Type} for employee {Code} by {User}", document.DocumentType, employeeCode, currentUser);
        return MapToDto(document);
    }

    public async Task<bool> DeleteDocumentAsync(long id)
    {
        var doc = await _documentRepository.GetByIdAsync(id);
        if (doc == null)
        {
            return false;
        }

        _documentRepository.Remove(doc);
        await _documentRepository.SaveChangesAsync();
        _logger.LogInformation("Deleted document {Id}", id);
        return true;
    }

    private static EmployeeDocumentDto MapToDto(EmployeeDocument doc)
    {
        return new EmployeeDocumentDto
        {
            Id = doc.Id,
            EmployeeCode = doc.EmployeeCode,
            DocumentType = doc.DocumentType,
            DocumentNumber = doc.DocumentNumber,
            FileReference = doc.FileReference,
            FileName = doc.FileName,
            IssuedDate = doc.IssuedDate,
            ExpiryDate = doc.ExpiryDate,
            Notes = doc.Notes,
            IsActive = doc.IsActive
        };
    }
}
