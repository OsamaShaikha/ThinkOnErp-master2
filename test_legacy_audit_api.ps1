# Test Legacy Audit API Endpoint
# This script tests if the column mismatch fix is working

$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1c2VySWQiOiI1IiwidXNlck5hbWUiOiJtb2UiLCJ1c2VyVHlwZSI6IlN1cGVyQWRtaW4iLCJpc0FkbWluIjoidHJ1ZSIsImlzU3VwZXJBZG1pbiI6InRydWUiLCJleHAiOjE3NzgwMTc2MTEsImlzcyI6IlRoaW5rT25FcnBBUEkiLCJhdWQiOiJUaGlua09uRXJwQ2xpZW50In0.NE-B5lyt2qJ-aD5Pb7Lrsm6yYKvot0HqI9fo5tCoJA0"

Write-Host "Testing Legacy Audit API Endpoint..." -ForegroundColor Cyan
Write-Host "URL: https://localhost:7136/api/auditlogs/legacy?pageNumber=1&pageSize=50" -ForegroundColor Yellow
Write-Host ""

try {
    $response = Invoke-RestMethod `
        -Uri "https://localhost:7136/api/auditlogs/legacy" `
        -Method Get `
        -Headers @{
            "Authorization" = "Bearer $token"
            "Accept" = "application/json"
        } `
        -Body @{
            pageNumber = 1
            pageSize = 50
        } `
        -SkipCertificateCheck
    
    Write-Host "✓ SUCCESS!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 5
    
} catch {
    Write-Host "✗ FAILED!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body:" -ForegroundColor Yellow
        Write-Host $responseBody
    }
}
