using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

// Request-scoped, populated from the existing screen/feature/branch permissions.
// Uninitialized instances deny. Null in manually constructed services is reserved
// for trusted internal jobs/tools; HTTP requires this scoped instance in its filter.
public sealed class EmployeeFieldAccess
{
    public const string ViewIdentity="view-identity", EditIdentity="edit-identity", ViewBank="view-bank", EditBank="edit-bank", ViewSalary="view-salary", EditSalary="edit-salary";
    public static readonly string[] Features={ViewIdentity,EditIdentity,ViewBank,EditBank,ViewSalary,EditSalary};
    private readonly Dictionary<string,HashSet<long>> branches=new(StringComparer.Ordinal);
    private bool administrator;
    public void Reset(bool admin=false) { branches.Clear(); administrator=admin; }
    public void Grant(string feature,long branch) { if(!branches.TryGetValue(feature,out var allowed)) branches[feature]=allowed=new(); allowed.Add(branch); }
    public bool AllBranches=>administrator;
    public long[] Branches(string feature)=>branches.TryGetValue(feature,out var allowed)?allowed.ToArray():Array.Empty<long>();
    public bool Can(string feature,long? branch)=>administrator || branch.HasValue && branches.TryGetValue(feature,out var allowed) && allowed.Contains(branch.Value);
    public void Require(string feature,long? branch) { if(!Can(feature,branch)) throw new UnauthorizedAccessException("Additional employee field permission is required: "+feature+"."); }
    public void RequireImport(long? branch) { foreach(var feature in Features) Require(feature,branch); }
    public EmployeeFieldVisibilityDto Visibility(long? branch)=>new(Can(ViewIdentity,branch),Can(ViewBank,branch),Can(ViewSalary,branch),Can(EditIdentity,branch),Can(EditBank,branch),Can(EditSalary,branch));
    public EmployeeSummaryDto Protect(EmployeeSummaryDto value,long? branch)
    {
        var access=Visibility(branch);
        return value with { NationalId=access.Identity?value.NationalId:null,Phone=access.Identity?value.Phone:null,Email=access.Identity?value.Email:null,
            UserId=access.Identity?value.UserId:null,BasicSalary=access.Salary?value.BasicSalary:null,FieldVisibility=access };
    }
    public EmployeeDetailsDto Protect(EmployeeDetailsDto value)
    {
        var access=Visibility(value.BranchId);
        return value with { NationalId=access.Identity?value.NationalId:null,Nationality=access.Identity?value.Nationality:null,
            CitizenshipCountryCode=access.Identity?value.CitizenshipCountryCode:null,
            PassportNumber=access.Identity?value.PassportNumber:null,DateOfBirth=access.Identity?value.DateOfBirth:null,
            Gender=access.Identity?value.Gender:null,MaritalStatus=access.Identity?value.MaritalStatus:null,Email=access.Identity?value.Email:null,
            Phone=access.Identity?value.Phone:null,SscNumber=access.Identity?value.SscNumber:null,TaxExemptionCount=access.Identity?value.TaxExemptionCount:null,
            IsHighRiskRole=access.Identity?value.IsHighRiskRole:null,UserId=access.Identity?value.UserId:null,
            Dependents=access.Identity?value.Dependents:new(),BankName=access.Bank?value.BankName:null,
            BankAccountNumber=access.Bank?value.BankAccountNumber:null,BankIban=access.Bank?value.BankIban:null,
            ActiveSalaryStructure=access.Salary?value.ActiveSalaryStructure:null,FieldVisibility=access };
    }
}
