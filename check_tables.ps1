Add-Type -Path "src/ThinkOnErp.API/bin/Debug/net8.0/Oracle.ManagedDataAccess.dll"
$connStr = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1539))(CONNECT_DATA=(SERVICE_NAME=free)));User Id=THINKON_ERP;Password=thinkon_erp;"
$conn = New-Object Oracle.ManagedDataAccess.Client.OracleConnection($connStr)
$conn.Open()

# Check what tables have AUDIT or LOG in all schemas
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT OWNER, TABLE_NAME FROM ALL_TABLES WHERE TABLE_NAME LIKE '%AUDIT%' OR TABLE_NAME LIKE '%LOG%' ORDER BY OWNER, TABLE_NAME"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['OWNER']).$($reader['TABLE_NAME'])"
}
$conn.Close()
