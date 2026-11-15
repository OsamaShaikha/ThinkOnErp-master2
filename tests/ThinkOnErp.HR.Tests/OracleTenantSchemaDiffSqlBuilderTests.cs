using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class OracleTenantSchemaDiffSqlBuilderTests
{
    [Fact]
    public void Build_UsesCharacterSemanticsForUnicodeAndPreservesByteOrCharMode()
    {
        var sql = OracleTenantSchemaDiffSqlBuilder.Build("DEV_TEMPLATE", "TENANT_TEST");

        Assert.Contains("src.char_length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src.char_used", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v_length := r.char_length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v_length := r.data_length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("' CHAR'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("' BYTE'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src.char_length>tgt.char_length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src.data_type='RAW' AND src.data_length>tgt.data_length", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAW(' || r.src_length || '))", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_WidensNumberPrecisionAndScaleOnlyWhenTheSourceRangeIsAtLeastAsWide()
    {
        var sql = OracleTenantSchemaDiffSqlBuilder.Build("DEV_TEMPLATE", "TENANT_TEST");

        Assert.Contains("src.data_type='NUMBER'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("src.data_precision-NVL(src.data_scale,0)>=tgt.data_precision-NVL(tgt.data_scale,0)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NVL(src.data_scale,0)>=NVL(tgt.data_scale,0)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NUMBER(' || r.src_precision || ',' || r.src_scale || ')", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_DoesNotSwallowDdlErrorsOrRetryRequiredColumnsAsNullable()
    {
        var sql = OracleTenantSchemaDiffSqlBuilder.Build("DEV_TEMPLATE", "TENANT_TEST");

        Assert.DoesNotContain("EXCEPTION WHEN OTHERS THEN NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("v_sql := v_sql || ' NULL';\n                            EXECUTE IMMEDIATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF r.nullable='N' AND r.data_default IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("DEV_TEMPLATE; DROP USER X")]
    [InlineData("bad-name")]
    [InlineData("")]
    public void Build_RejectsUnquotedSchemaIdentifiers(string schema)
    {
        Assert.Throws<ArgumentException>(() => OracleTenantSchemaDiffSqlBuilder.Build(schema, "TENANT_TEST"));
    }

    [Fact]
    public void Build_RejectsUsingTheSameSchemaAsSourceAndTarget()
    {
        var error = Assert.Throws<ArgumentException>(() => OracleTenantSchemaDiffSqlBuilder.Build("DEV_TEMPLATE", "dev_template"));

        Assert.Equal("targetSchema", error.ParamName);
    }
}
