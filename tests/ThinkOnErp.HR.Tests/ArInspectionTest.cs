using System;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Xunit;
using Xunit.Abstractions;

namespace ThinkOnErp.HR.Tests;

public class ArInspectionTest
{
    private readonly ITestOutputHelper _output;

    public ArInspectionTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InspectPosAndStockData()
    {
        var connStr = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1539))(CONNECT_DATA=(SERVICE_NAME=free)));User Id=THINKON_ERP;Password=thinkon_erp;";
        using var conn = new OracleConnection(connStr);
        await conn.OpenAsync();

        using var alterCmd = conn.CreateCommand();
        alterCmd.CommandText = "ALTER SESSION SET CURRENT_SCHEMA = THINKONERP_1122";
        await alterCmd.ExecuteNonQueryAsync();

        // 1. Settings (50, 51)
        using var setCmd = conn.CreateCommand();
        setCmd.CommandText = "SELECT SETTING_CODE, SETTING_DESC, SETTING_VALUE FROM SYS_SETTINGS WHERE SETTING_CODE IN (50, 51)";
        using var rSet = await setCmd.ExecuteReaderAsync();
        _output.WriteLine("--- SYS_SETTINGS (50, 51) ---");
        while (await rSet.ReadAsync())
        {
            _output.WriteLine($"Code: {rSet.GetValue(0)}, Desc: {rSet.GetValue(1)}, Value: {rSet.GetValue(2)}");
        }

        // 2. POS_BRANCH_SETTING
        try
        {
            using var bsCmd = conn.CreateCommand();
            bsCmd.CommandText = "SELECT ID, BRANCH_ID, DEFAULT_CASH_ACCOUNT, DEFAULT_EXPENSE_ACCOUNT, DEFAULT_RECEIPT_ACCOUNT, DEFAULT_WAREHOUSE_ID FROM POS_BRANCH_SETTING";
            using var rBs = await bsCmd.ExecuteReaderAsync();
            _output.WriteLine("--- POS_BRANCH_SETTING ---");
            while (await rBs.ReadAsync())
            {
                _output.WriteLine($"Id: {rBs.GetValue(0)}, Branch: {rBs.GetValue(1)}, CashAcc: {rBs.GetValue(2)}, ExpAcc: {rBs.GetValue(3)}, RecAcc: {rBs.GetValue(4)}, WhId: {rBs.GetValue(5)}");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"POS_BRANCH_SETTING error: {ex.Message}");
        }

        // 3. Stock balances for item 201
        using var balCmd = conn.CreateCommand();
        balCmd.CommandText = "SELECT ITEM_ID, WAREHOUSE_ID, ON_HAND_QTY, RESERVED_QTY, AVG_COST FROM INV_STOCK_BALANCE WHERE ITEM_ID = 201";
        using var rBal = await balCmd.ExecuteReaderAsync();
        _output.WriteLine("--- INV_STOCK_BALANCE (Item 201) ---");
        while (await rBal.ReadAsync())
        {
            _output.WriteLine($"Item: {rBal.GetValue(0)}, Wh: {rBal.GetValue(1)}, OnHand: {rBal.GetValue(2)}, Reserved: {rBal.GetValue(3)}, AvgCost: {rBal.GetValue(4)}");
        }

        // 4. Stock ledger entries for item 201
        using var ledCmd = conn.CreateCommand();
        ledCmd.CommandText = "SELECT ID, TRANSACTION_DATE, TRANSACTION_TYPE, DIRECTION, QUANTITY, WAREHOUSE_ID, SOURCE_DOC_ID, NOTES FROM INV_STOCK_LEDGER WHERE ITEM_ID = 201 ORDER BY ID DESC";
        using var rLed = await ledCmd.ExecuteReaderAsync();
        _output.WriteLine("--- INV_STOCK_LEDGER (Item 201) ---");
        while (await rLed.ReadAsync())
        {
            _output.WriteLine($"Ledger ID: {rLed.GetValue(0)}, Date: {rLed.GetValue(1)}, Type: {rLed.GetValue(2)}, Dir: {rLed.GetValue(3)}, Qty: {rLed.GetValue(4)}, Wh: {rLed.GetValue(5)}, Doc: {rLed.GetValue(6)}, Notes: {rLed.GetValue(7)}");
        }

        // 5. Recent POS orders
        using var ordCmd = conn.CreateCommand();
        ordCmd.CommandText = "SELECT ID, ORDER_NUMBER, INVOICE_NUMBER, BRANCH_ID, SHIFT_ID, STATUS, TOTAL_AMOUNT, PAID_AMOUNT, IS_PAID, CREATION_DATE FROM POS_ORDER_HEADER ORDER BY ID DESC FETCH FIRST 10 ROWS ONLY";
        using var rOrd = await ordCmd.ExecuteReaderAsync();
        _output.WriteLine("--- RECENT POS ORDERS ---");
        while (await rOrd.ReadAsync())
        {
            _output.WriteLine($"Order ID: {rOrd.GetValue(0)}, No: {rOrd.GetValue(1)}, Inv: {rOrd.GetValue(2)}, Branch: {rOrd.GetValue(3)}, Shift: {rOrd.GetValue(4)}, Status: {rOrd.GetValue(5)}, Total: {rOrd.GetValue(6)}, Paid: {rOrd.GetValue(7)}, IsPaid: {rOrd.GetValue(8)}, Date: {rOrd.GetValue(9)}");
        }

        // 6. Recent POS order lines for item 201
        using var lineCmd = conn.CreateCommand();
        lineCmd.CommandText = "SELECT ID, ORDER_ID, ITEM_ID, QUANTITY, UNIT_PRICE, LINE_TOTAL, IS_VOIDED FROM POS_ORDER_LINE WHERE ITEM_ID = 201 ORDER BY ID DESC FETCH FIRST 10 ROWS ONLY";
        using var rLine = await lineCmd.ExecuteReaderAsync();
        _output.WriteLine("--- POS ORDER LINES (Item 201) ---");
        while (await rLine.ReadAsync())
        {
            _output.WriteLine($"Line ID: {rLine.GetValue(0)}, OrderId: {rLine.GetValue(1)}, ItemId: {rLine.GetValue(2)}, Qty: {rLine.GetValue(3)}, Price: {rLine.GetValue(4)}, Total: {rLine.GetValue(5)}, Voided: {rLine.GetValue(6)}");
        }
    }
}
