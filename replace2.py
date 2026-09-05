import os
import re

replacements = [
    ("ItemNameAr", "ItemNameLocal"),
    ("WarehouseNameAr", "WarehouseNameLocal"),
    ("GroupNameAr", "GroupNameLocal"),
    ("BomNameAr", "BomNameLocal"),
    ("TypeNameAr", "TypeNameLocal"),
    ("TrxNameAr", "TrxNameLocal"),
    ("AccountNameAr", "AccountNameLocal"),
    ("PeriodNameAr", "PeriodNameLocal"),
    ("EventNameAr", "EventNameLocal"),
    ("ExemptionReasonAr", "ExemptionReasonLocal"),
    ("LevelNameAr", "LevelNameLocal"),
    ("BranchNameAr", "BranchNameLocal"),
    ("CompanyNameAr", "CompanyNameLocal"),
    ("LegalNameAr", "LegalNameLocal"),
    ("CurrencyNameAr", "CurrencyNameLocal"),
    ("FiscalYearNameAr", "FiscalYearNameLocal"),
    ("RoleNameAr", "RoleNameLocal"),
    ("FullNameAr", "FullNameLocal"),
    ("CategoryNameAr", "CategoryNameLocal"),
    ("PriorityNameAr", "PriorityNameLocal"),
    ("StatusNameAr", "StatusNameLocal"),
    ("DescriptionAr", "DescriptionLocal"),
    ("NameAr", "NameLocal"),
    ("ITEM_NAME_AR", "ITEM_NAME_LOCAL"),
    ("WAREHOUSE_NAME_AR", "WAREHOUSE_NAME_LOCAL"),
    ("GROUP_NAME_AR", "GROUP_NAME_LOCAL"),
    ("BOM_NAME_AR", "BOM_NAME_LOCAL"),
    ("TYPE_NAME_AR", "TYPE_NAME_LOCAL"),
    ("TRX_NAME_AR", "TRX_NAME_LOCAL"),
    ("ACCOUNT_NAME_AR", "ACCOUNT_NAME_LOCAL"),
    ("PERIOD_NAME_AR", "PERIOD_NAME_LOCAL"),
    ("EVENT_NAME_AR", "EVENT_NAME_LOCAL"),
    ("EXEMPTION_REASON_AR", "EXEMPTION_REASON_LOCAL"),
    ("LEVEL_NAME_AR", "LEVEL_NAME_LOCAL"),
    ("BRANCH_NAME_AR", "BRANCH_NAME_LOCAL"),
    ("COMPANY_NAME_AR", "COMPANY_NAME_LOCAL"),
    ("LEGAL_NAME_AR", "LEGAL_NAME_LOCAL"),
    ("CURRENCY_NAME_AR", "CURRENCY_NAME_LOCAL"),
    ("FISCAL_YEAR_NAME_AR", "FISCAL_YEAR_NAME_LOCAL"),
    ("ROLE_NAME_AR", "ROLE_NAME_LOCAL"),
    ("FULL_NAME_AR", "FULL_NAME_LOCAL"),
    ("CATEGORY_NAME_AR", "CATEGORY_NAME_LOCAL"),
    ("PRIORITY_NAME_AR", "PRIORITY_NAME_LOCAL"),
    ("STATUS_NAME_AR", "STATUS_NAME_LOCAL"),
    ("DESCRIPTION_AR", "DESCRIPTION_LOCAL"),
    ("NAME_AR", "NAME_LOCAL")
]

directories = [
    r"d:\ThinkOnErp\tests"
]

for directory in directories:
    for root, _, files in os.walk(directory):
        for file in files:
            if file.endswith(".cs"):
                filepath = os.path.join(root, file)
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                original_content = content
                for old, new in replacements:
                    content = re.sub(r'\b' + old + r'\b', new, content)
                
                if content != original_content:
                    with open(filepath, 'w', encoding='utf-8') as f:
                        f.write(content)
                    print(f"Updated {filepath}")
