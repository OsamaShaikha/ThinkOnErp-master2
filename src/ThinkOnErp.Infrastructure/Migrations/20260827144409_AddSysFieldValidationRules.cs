using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThinkOnErp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSysFieldValidationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create SYS_FIELD_VALIDATION_RULE in master schema if not exists
            migrationBuilder.Sql(@"
                DECLARE
                    v_count NUMBER;
                BEGIN
                    SELECT COUNT(*) INTO v_count FROM user_tables WHERE table_name = 'SYS_FIELD_VALIDATION_RULE';
                    IF v_count = 0 THEN
                        EXECUTE IMMEDIATE '
                            CREATE TABLE ""SYS_FIELD_VALIDATION_RULE"" (
                                ""ID"" NUMBER(19) GENERATED ALWAYS AS IDENTITY NOT NULL,
                                ""ENTITY_NAME"" NVARCHAR2(100) NOT NULL,
                                ""FIELD_NAME"" NVARCHAR2(100) NOT NULL,
                                ""RULE_TYPE"" NVARCHAR2(50) NOT NULL,
                                ""RULE_VALUE"" NVARCHAR2(500) NULL,
                                ""ERROR_CODE"" NVARCHAR2(100) NOT NULL,
                                ""COUNTRY_CODE"" NVARCHAR2(10) NULL,
                                ""COMPANY_ID"" NUMBER(19) NULL,
                                ""IS_ACTIVE"" NUMBER(1) DEFAULT 1 NOT NULL,
                                ""CREATION_USER"" NVARCHAR2(100) DEFAULT ''system'' NOT NULL,
                                ""CREATION_DATE"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
                                ""UPDATE_USER"" NVARCHAR2(100) NULL,
                                ""UPDATE_DATE"" TIMESTAMP NULL,
                                CONSTRAINT ""PK_SYS_FIELD_VAL_RULE"" PRIMARY KEY (""ID"")
                            )';
                        EXECUTE IMMEDIATE 'CREATE INDEX ""IX_VAL_RULE_LOOKUP"" ON ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"")';
                    END IF;
                END;
            ");

            // 2. Seed Error Categories & Localized Error Messages in SYS_CODE (CODE_MGR = 30)
            migrationBuilder.Sql(@"
                DECLARE
                    v_seed_user NVARCHAR2(100) := 'SystemSeed';
                    v_now TIMESTAMP := CURRENT_TIMESTAMP;
                BEGIN
                    -- Header definition for ValidationErrors (CODE_MGR = 30, CODE_MNR = 0)
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 0, 1, N'أخطاء وقواعد التحقق', 'ValidationErrors', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=0 AND ""CODE_LANG""=1);

                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 0, 2, 'Validation and Error Rules', 'ValidationErrors', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=0 AND ""CODE_LANG""=2);

                    -- 1. ERR_FIELD_REQUIRED
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 1, 1, N'حقل {0} مطلوب ولا يمكن تركه فارغاً.', 'ERR_FIELD_REQUIRED', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=1 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 1, 2, 'The field {0} is required.', 'ERR_FIELD_REQUIRED', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=1 AND ""CODE_LANG""=2);

                    -- 2. ERR_INVALID_FORMAT
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 2, 1, N'صيغة حقل {0} غير صالحة.', 'ERR_INVALID_FORMAT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=2 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 2, 2, 'The format of field {0} is invalid.', 'ERR_INVALID_FORMAT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=2 AND ""CODE_LANG""=2);

                    -- 3. ERR_OUT_OF_RANGE
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 3, 1, N'قيمة {0} يجب أن تكون بين {1} و {2}.', 'ERR_OUT_OF_RANGE', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=3 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 3, 2, 'The value of {0} must be between {1} and {2}.', 'ERR_OUT_OF_RANGE', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=3 AND ""CODE_LANG""=2);

                    -- 4. ERR_INVALID_TAX_NUMBER
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 4, 1, N'الرقم الضريبي غير مطابق للمعيار الضريبي المعتمد.', 'ERR_INVALID_TAX_NUMBER', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=4 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 4, 2, 'Tax registration number does not match the mandatory standard.', 'ERR_INVALID_TAX_NUMBER', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=4 AND ""CODE_LANG""=2);

                    -- 5. ERR_INVALID_ACCOUNT_CODE
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 5, 1, N'رمز الحساب المالي غير صالح.', 'ERR_INVALID_ACCOUNT_CODE', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=5 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 5, 2, 'Account code format is invalid.', 'ERR_INVALID_ACCOUNT_CODE', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=5 AND ""CODE_LANG""=2);

                    -- 6. ERR_ACCOUNT_BALANCE_TYPE_INVALID
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 6, 1, N'طبيعة رصيد الحساب غير صحيحة (يجب أن تكون مدين ''D'' أو دائن ''C'').', 'ERR_ACCOUNT_BALANCE_TYPE_INVALID', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=6 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 6, 2, 'Normal balance type is invalid (must be Debit ''D'' or Credit ''C'').', 'ERR_ACCOUNT_BALANCE_TYPE_INVALID', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=6 AND ""CODE_LANG""=2);

                    -- 7. ERR_ACCOUNT_TYPE_INVALID
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 7, 1, N'نوع الحساب المالي غير صحيح (يجب أن يكون رئيسي ''HEADER'' أو تحليلي ''DETAIL'').', 'ERR_ACCOUNT_TYPE_INVALID', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=7 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 7, 2, 'Account type is invalid (must be ''HEADER'' or ''DETAIL'').', 'ERR_ACCOUNT_TYPE_INVALID', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=7 AND ""CODE_LANG""=2);

                    -- 8. ERR_UNBALANCED_VOUCHER
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 8, 1, N'القيد المحاسبي غير متوازن (مجموع المدين لا يساوي مجموع الدائن).', 'ERR_UNBALANCED_VOUCHER', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=8 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 8, 2, 'Journal voucher is unbalanced (Total Debit != Total Credit).', 'ERR_UNBALANCED_VOUCHER', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=8 AND ""CODE_LANG""=2);

                    -- 9. ERR_EMPTY_VOUCHER_LINES
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 9, 1, N'لا يمكن حفظ قيد محاسبي بدون أسطر تفصيلية.', 'ERR_EMPTY_VOUCHER_LINES', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=9 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 9, 2, 'Cannot save a voucher without detail lines.', 'ERR_EMPTY_VOUCHER_LINES', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=9 AND ""CODE_LANG""=2);

                    -- 10. ERR_CHEQUE_NUMBER_REQUIRED
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 10, 1, N'رقم الشيك إجباري.', 'ERR_CHEQUE_NUMBER_REQUIRED', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=10 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 10, 2, 'Cheque number is mandatory.', 'ERR_CHEQUE_NUMBER_REQUIRED', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=10 AND ""CODE_LANG""=2);

                    -- 11. ERR_INVALID_CHEQUE_AMOUNT
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 11, 1, N'مبلغ الشيك يجب أن يكون أكبر من الصفر.', 'ERR_INVALID_CHEQUE_AMOUNT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=11 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 11, 2, 'Cheque amount must be greater than zero.', 'ERR_INVALID_CHEQUE_AMOUNT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=11 AND ""CODE_LANG""=2);

                    -- 12. ERR_INVALID_TAX_PERCENT
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 12, 1, N'نسبة الضريبة يجب أن تكون بين 0 و 100%.', 'ERR_INVALID_TAX_PERCENT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=12 AND ""CODE_LANG""=1);
                    INSERT INTO ""SYS_CODE"" (""CODE_MGR"", ""CODE_MNR"", ""CODE_LANG"", ""CODE_DESC"", ""CODE_VALUE"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 30, 12, 2, 'Tax rate percentage must be between 0 and 100%.', 'ERR_INVALID_TAX_PERCENT', 1, v_seed_user, v_now FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM ""SYS_CODE"" WHERE ""CODE_MGR""=30 AND ""CODE_MNR""=12 AND ""CODE_LANG""=2);
                END;
            ");

            // 3. Seed Baseline Validation Rules into SYS_FIELD_VALIDATION_RULE
            migrationBuilder.Sql(@"
                DECLARE
                    v_seed_user NVARCHAR2(100) := 'SystemSeed';
                    v_now TIMESTAMP := CURRENT_TIMESTAMP;
                BEGIN
                    -- Customer Rules
                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'Customer', 'CustomerCode', 'REQUIRED', NULL, 'ERR_FIELD_REQUIRED', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='Customer' AND ""FIELD_NAME""='CustomerCode' AND ""RULE_TYPE""='REQUIRED');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'Customer', 'NameAr', 'REQUIRED', NULL, 'ERR_FIELD_REQUIRED', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='Customer' AND ""FIELD_NAME""='NameAr' AND ""RULE_TYPE""='REQUIRED');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'Customer', 'CreditLimit', 'RANGE', '0..999999999', 'ERR_OUT_OF_RANGE', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='Customer' AND ""FIELD_NAME""='CreditLimit' AND ""RULE_TYPE""='RANGE');

                    -- Saudi Tax Number (ZATCA: 15 digits starting and ending with 3)
                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'Customer', 'TaxNumber', 'REGEX', '^3\d{13}3$', 'ERR_INVALID_TAX_NUMBER', 'SA', NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='Customer' AND ""FIELD_NAME""='TaxNumber' AND ""COUNTRY_CODE""='SA');

                    -- Jordan Tax Number (ISTD: 8 digits)
                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'Customer', 'TaxNumber', 'REGEX', '^\d{8}$', 'ERR_INVALID_TAX_NUMBER', 'JO', NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='Customer' AND ""FIELD_NAME""='TaxNumber' AND ""COUNTRY_CODE""='JO');

                    -- GlAccount Rules
                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'GlAccount', 'AccountCode', 'REQUIRED', NULL, 'ERR_FIELD_REQUIRED', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='GlAccount' AND ""FIELD_NAME""='AccountCode' AND ""RULE_TYPE""='REQUIRED');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'GlAccount', 'AccountCode', 'REGEX', '^[0-9]+(\.[0-9]+)*$', 'ERR_INVALID_ACCOUNT_CODE', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='GlAccount' AND ""FIELD_NAME""='AccountCode' AND ""RULE_TYPE""='REGEX');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'GlAccount', 'AccountNameAr', 'REQUIRED', NULL, 'ERR_FIELD_REQUIRED', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='GlAccount' AND ""FIELD_NAME""='AccountNameAr' AND ""RULE_TYPE""='REQUIRED');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'GlAccount', 'NormalBalance', 'REGEX', '^[DCdc]$', 'ERR_ACCOUNT_BALANCE_TYPE_INVALID', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='GlAccount' AND ""FIELD_NAME""='NormalBalance');

                    INSERT INTO ""SYS_FIELD_VALIDATION_RULE"" (""ENTITY_NAME"", ""FIELD_NAME"", ""RULE_TYPE"", ""RULE_VALUE"", ""ERROR_CODE"", ""COUNTRY_CODE"", ""COMPANY_ID"", ""IS_ACTIVE"", ""CREATION_USER"", ""CREATION_DATE"")
                    SELECT 'GlAccount', 'AccountType', 'REGEX', '^(HEADER|DETAIL)$', 'ERR_ACCOUNT_TYPE_INVALID', NULL, NULL, 1, v_seed_user, v_now FROM DUAL
                    WHERE NOT EXISTS (SELECT 1 FROM ""SYS_FIELD_VALIDATION_RULE"" WHERE ""ENTITY_NAME""='GlAccount' AND ""FIELD_NAME""='AccountType');
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"SYS_CODE\" WHERE \"CODE_MGR\" = 30;");
            migrationBuilder.Sql("DROP TABLE \"SYS_FIELD_VALIDATION_RULE\" CASCADE CONSTRAINTS;");
        }
    }
}
