using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Accounting.Parties;
using ThinkOnErp.Application.Mappings.Accounting;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;

namespace ThinkOnErp.Application.Services.Accounting;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        ICustomerRepository customerRepository,
        IGlAccountRepository accountRepository,
        ICurrentTenantContext tenantContext,
        ILogger<CustomerService> logger)
    {
        _customerRepository = customerRepository;
        _accountRepository = accountRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(PartyFilterDto filter, CancellationToken cancellationToken = default)
    {
        var customers = await _customerRepository.GetAllAsync(filter.SearchTerm, filter.BranchId, filter.IsActive, cancellationToken);
        return customers.Select(CustomerMapper.ToDto).ToList();
    }

    public async Task<CustomerDto> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByCodeAsync(code, cancellationToken);
        if (customer == null)
        {
            throw new AccountingNotFoundException($"العميل برمز ({code}) غير موجود.", "CUSTOMER_NOT_FOUND");
        }
        return CustomerMapper.ToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerCode))
        {
            throw new AccountingException("رمز العميل مطلوب.", "CUSTOMER_CODE_REQUIRED");
        }

        var exists = await _customerRepository.ExistsByCodeAsync(dto.CustomerCode, cancellationToken);
        if (exists)
        {
            throw new AccountingException($"رمز العميل ({dto.CustomerCode}) مستخدم مسبقاً.", "CUSTOMER_CODE_DUPLICATE");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        var arAccountCode = string.IsNullOrWhiteSpace(dto.ArControlAccountCode) ? "112101" : dto.ArControlAccountCode;
        var account = await _accountRepository.GetByCodeAsync(companyId, arAccountCode, cancellationToken);
        if (account == null)
        {
            throw new AccountingException($"حساب مراقبة العملاء ({arAccountCode}) غير موجود في دليل الحسابات.", "AR_ACCOUNT_NOT_FOUND");
        }

        var customer = new Customer
        {
            CustomerCode = dto.CustomerCode.Trim().ToUpper(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            ArControlAccountCode = arAccountCode,
            DefaultCurrencyId = dto.DefaultCurrencyId,
            CreditLimit = dto.CreditLimit,
            PaymentTermsDays = dto.PaymentTermsDays > 0 ? dto.PaymentTermsDays : 30,
            BranchId = dto.BranchId,
            TaxNumber = dto.TaxNumber?.Trim(),
            Phone = dto.Phone?.Trim(),
            Email = dto.Email?.Trim(),
            Address = dto.Address?.Trim(),
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerCode} created by {User}", customer.CustomerCode, username);

        return await GetByCodeAsync(customer.CustomerCode, cancellationToken);
    }

    public async Task<CustomerDto> UpdateAsync(string code, UpdateCustomerDto dto, string username, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByCodeAsync(code, cancellationToken);
        if (customer == null)
        {
            throw new AccountingNotFoundException($"العميل برمز ({code}) غير موجود.", "CUSTOMER_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.ArControlAccountCode))
        {
            var companyId = _tenantContext.GetRequiredCompanyId();
            var account = await _accountRepository.GetByCodeAsync(companyId, dto.ArControlAccountCode, cancellationToken);
            if (account == null)
            {
                throw new AccountingException($"حساب مراقبة العملاء ({dto.ArControlAccountCode}) غير موجود.", "AR_ACCOUNT_NOT_FOUND");
            }
            customer.ArControlAccountCode = dto.ArControlAccountCode;
        }

        customer.NameLocal = dto.NameLocal.Trim();
        customer.NameEn = dto.NameEn.Trim();
        customer.DefaultCurrencyId = dto.DefaultCurrencyId;
        customer.CreditLimit = dto.CreditLimit;
        customer.PaymentTermsDays = dto.PaymentTermsDays > 0 ? dto.PaymentTermsDays : 30;
        customer.BranchId = dto.BranchId;
        customer.TaxNumber = dto.TaxNumber?.Trim();
        customer.Phone = dto.Phone?.Trim();
        customer.Email = dto.Email?.Trim();
        customer.Address = dto.Address?.Trim();
        customer.IsActive = dto.IsActive;
        customer.UpdateUser = username;
        customer.UpdateDate = DateTime.UtcNow;

        _customerRepository.Update(customer);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerCode} updated by {User}", code, username);

        return await GetByCodeAsync(code, cancellationToken);
    }

    public async Task<bool> SetStatusAsync(string code, bool isActive, string username, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByCodeAsync(code, cancellationToken);
        if (customer == null)
        {
            throw new AccountingNotFoundException($"العميل برمز ({code}) غير موجود.", "CUSTOMER_NOT_FOUND");
        }

        customer.IsActive = isActive;
        customer.UpdateUser = username;
        customer.UpdateDate = DateTime.UtcNow;

        _customerRepository.Update(customer);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Customer {CustomerCode} status changed to {Status} by {User}", code, isActive, username);
        return true;
    }
}

public sealed class VendorService : IVendorService
{
    private readonly IVendorRepository _vendorRepository;
    private readonly IGlAccountRepository _accountRepository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly ILogger<VendorService> _logger;

    public VendorService(
        IVendorRepository vendorRepository,
        IGlAccountRepository accountRepository,
        ICurrentTenantContext tenantContext,
        ILogger<VendorService> logger)
    {
        _vendorRepository = vendorRepository;
        _accountRepository = accountRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VendorDto>> GetAllAsync(PartyFilterDto filter, CancellationToken cancellationToken = default)
    {
        var vendors = await _vendorRepository.GetAllAsync(filter.SearchTerm, filter.BranchId, filter.IsActive, cancellationToken);
        return vendors.Select(VendorMapper.ToDto).ToList();
    }

    public async Task<VendorDto> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByCodeAsync(code, cancellationToken);
        if (vendor == null)
        {
            throw new AccountingNotFoundException($"المورد برمز ({code}) غير موجود.", "VENDOR_NOT_FOUND");
        }
        return VendorMapper.ToDto(vendor);
    }

    public async Task<VendorDto> CreateAsync(CreateVendorDto dto, string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.VendorCode))
        {
            throw new AccountingException("رمز المورد مطلوب.", "VENDOR_CODE_REQUIRED");
        }

        var exists = await _vendorRepository.ExistsByCodeAsync(dto.VendorCode, cancellationToken);
        if (exists)
        {
            throw new AccountingException($"رمز المورد ({dto.VendorCode}) مستخدم مسبقاً.", "VENDOR_CODE_DUPLICATE");
        }

        var companyId = _tenantContext.GetRequiredCompanyId();
        var apAccountCode = string.IsNullOrWhiteSpace(dto.ApControlAccountCode) ? "211101" : dto.ApControlAccountCode;
        var account = await _accountRepository.GetByCodeAsync(companyId, apAccountCode, cancellationToken);
        if (account == null)
        {
            throw new AccountingException($"حساب مراقبة الموردين ({apAccountCode}) غير موجود في دليل الحسابات.", "AP_ACCOUNT_NOT_FOUND");
        }

        var vendor = new Vendor
        {
            VendorCode = dto.VendorCode.Trim().ToUpper(),
            NameLocal = dto.NameLocal.Trim(),
            NameEn = dto.NameEn.Trim(),
            ApControlAccountCode = apAccountCode,
            DefaultCurrencyId = dto.DefaultCurrencyId,
            PaymentTermsDays = dto.PaymentTermsDays > 0 ? dto.PaymentTermsDays : 30,
            BranchId = dto.BranchId,
            TaxNumber = dto.TaxNumber?.Trim(),
            Phone = dto.Phone?.Trim(),
            Email = dto.Email?.Trim(),
            Address = dto.Address?.Trim(),
            IsActive = true,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        await _vendorRepository.AddAsync(vendor, cancellationToken);
        await _vendorRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vendor {VendorCode} created by {User}", vendor.VendorCode, username);

        return await GetByCodeAsync(vendor.VendorCode, cancellationToken);
    }

    public async Task<VendorDto> UpdateAsync(string code, UpdateVendorDto dto, string username, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByCodeAsync(code, cancellationToken);
        if (vendor == null)
        {
            throw new AccountingNotFoundException($"المورد برمز ({code}) غير موجود.", "VENDOR_NOT_FOUND");
        }

        if (!string.IsNullOrWhiteSpace(dto.ApControlAccountCode))
        {
            var companyId = _tenantContext.GetRequiredCompanyId();
            var account = await _accountRepository.GetByCodeAsync(companyId, dto.ApControlAccountCode, cancellationToken);
            if (account == null)
            {
                throw new AccountingException($"حساب مراقبة الموردين ({dto.ApControlAccountCode}) غير موجود.", "AP_ACCOUNT_NOT_FOUND");
            }
            vendor.ApControlAccountCode = dto.ApControlAccountCode;
        }

        vendor.NameLocal = dto.NameLocal.Trim();
        vendor.NameEn = dto.NameEn.Trim();
        vendor.DefaultCurrencyId = dto.DefaultCurrencyId;
        vendor.PaymentTermsDays = dto.PaymentTermsDays > 0 ? dto.PaymentTermsDays : 30;
        vendor.BranchId = dto.BranchId;
        vendor.TaxNumber = dto.TaxNumber?.Trim();
        vendor.Phone = dto.Phone?.Trim();
        vendor.Email = dto.Email?.Trim();
        vendor.Address = dto.Address?.Trim();
        vendor.IsActive = dto.IsActive;
        vendor.UpdateUser = username;
        vendor.UpdateDate = DateTime.UtcNow;

        _vendorRepository.Update(vendor);
        await _vendorRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vendor {VendorCode} updated by {User}", code, username);

        return await GetByCodeAsync(code, cancellationToken);
    }

    public async Task<bool> SetStatusAsync(string code, bool isActive, string username, CancellationToken cancellationToken = default)
    {
        var vendor = await _vendorRepository.GetByCodeAsync(code, cancellationToken);
        if (vendor == null)
        {
            throw new AccountingNotFoundException($"المورد برمز ({code}) غير موجود.", "VENDOR_NOT_FOUND");
        }

        vendor.IsActive = isActive;
        vendor.UpdateUser = username;
        vendor.UpdateDate = DateTime.UtcNow;

        _vendorRepository.Update(vendor);
        await _vendorRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Vendor {VendorCode} status changed to {Status} by {User}", code, isActive, username);
        return true;
    }
}
