using ThinkOnErp.Application.DTOs.Accounting.Parties;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Mappings.Accounting;

public static class CustomerMapper
{
    public static CustomerDto ToDto(Customer customer)
    {
        return new CustomerDto
        {
            Id = customer.Id,
            CustomerCode = customer.CustomerCode,
            NameLocal = customer.NameLocal,
            NameEn = customer.NameEn,
            ArControlAccountCode = customer.ArControlAccountCode,
            ArControlAccountNameLocal = customer.ArControlAccount?.AccountNameLocal,
            ArControlAccountNameEn = customer.ArControlAccount?.AccountNameEn,
            DefaultCurrencyId = customer.DefaultCurrencyId,
            DefaultCurrencyNameLocal = customer.DefaultCurrency?.CurrencyNameLocal,
            DefaultCurrencyNameEn = customer.DefaultCurrency?.CurrencyNameEn,
            CreditLimit = customer.CreditLimit,
            PaymentTermsDays = customer.PaymentTermsDays,
            BranchId = customer.BranchId,
            BranchNameLocal = customer.Branch?.BranchNameLocal,
            BranchNameEn = customer.Branch?.BranchNameEn,
            TaxNumber = customer.TaxNumber,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            IsActive = customer.IsActive,
            CreationUser = customer.CreationUser,
            CreationDate = customer.CreationDate
        };
    }
}

public static class VendorMapper
{
    public static VendorDto ToDto(Vendor vendor)
    {
        return new VendorDto
        {
            Id = vendor.Id,
            VendorCode = vendor.VendorCode,
            NameLocal = vendor.NameLocal,
            NameEn = vendor.NameEn,
            ApControlAccountCode = vendor.ApControlAccountCode,
            ApControlAccountNameLocal = vendor.ApControlAccount?.AccountNameLocal,
            ApControlAccountNameEn = vendor.ApControlAccount?.AccountNameEn,
            DefaultCurrencyId = vendor.DefaultCurrencyId,
            DefaultCurrencyNameLocal = vendor.DefaultCurrency?.CurrencyNameLocal,
            DefaultCurrencyNameEn = vendor.DefaultCurrency?.CurrencyNameEn,
            PaymentTermsDays = vendor.PaymentTermsDays,
            BranchId = vendor.BranchId,
            BranchNameLocal = vendor.Branch?.BranchNameLocal,
            BranchNameEn = vendor.Branch?.BranchNameEn,
            TaxNumber = vendor.TaxNumber,
            Phone = vendor.Phone,
            Email = vendor.Email,
            Address = vendor.Address,
            IsActive = vendor.IsActive,
            CreationUser = vendor.CreationUser,
            CreationDate = vendor.CreationDate
        };
    }
}
