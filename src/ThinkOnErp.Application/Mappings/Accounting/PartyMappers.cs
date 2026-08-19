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
            NameAr = customer.NameAr,
            NameEn = customer.NameEn,
            ArControlAccountCode = customer.ArControlAccountCode,
            ArControlAccountNameAr = customer.ArControlAccount?.AccountNameAr,
            ArControlAccountNameEn = customer.ArControlAccount?.AccountNameEn,
            DefaultCurrencyId = customer.DefaultCurrencyId,
            DefaultCurrencyNameAr = customer.DefaultCurrency?.CurrencyNameAr,
            DefaultCurrencyNameEn = customer.DefaultCurrency?.CurrencyNameEn,
            CreditLimit = customer.CreditLimit,
            PaymentTermsDays = customer.PaymentTermsDays,
            BranchId = customer.BranchId,
            BranchNameAr = customer.Branch?.BranchNameAr,
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
            NameAr = vendor.NameAr,
            NameEn = vendor.NameEn,
            ApControlAccountCode = vendor.ApControlAccountCode,
            ApControlAccountNameAr = vendor.ApControlAccount?.AccountNameAr,
            ApControlAccountNameEn = vendor.ApControlAccount?.AccountNameEn,
            DefaultCurrencyId = vendor.DefaultCurrencyId,
            DefaultCurrencyNameAr = vendor.DefaultCurrency?.CurrencyNameAr,
            DefaultCurrencyNameEn = vendor.DefaultCurrency?.CurrencyNameEn,
            PaymentTermsDays = vendor.PaymentTermsDays,
            BranchId = vendor.BranchId,
            BranchNameAr = vendor.Branch?.BranchNameAr,
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
