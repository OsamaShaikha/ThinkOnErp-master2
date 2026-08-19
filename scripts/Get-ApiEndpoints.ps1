<#
.SYNOPSIS
    Lists all API controllers, routes, HTTP methods, and actions across ThinkOnErp.API
.EXAMPLE
    .\scripts\Get-ApiEndpoints.ps1
#>

$controllersDir = Join-Path $PSScriptRoot "..\src\ThinkOnErp.API\Controllers"
$files = Get-ChildItem -Path $controllersDir -Filter "*Controller.cs"

$results = @()

foreach ($file in $files) {
    $content = Get-Content $file.FullName
    $baseRoute = ""
    
    foreach ($line in $content) {
        if ($line -match '\[Route\("([^"]+)"\)\]') {
            $baseRoute = $matches[1]
            break
        }
    }

    $currentHttp = ""
    $currentRoute = ""

    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i].Trim()

        if ($line -match '\[Http(Get|Post|Put|Delete|Patch)(\("([^"]*)"\))?\]') {
            $currentHttp = $matches[1].ToUpper()
            $subRoute = if ($matches[3]) { $matches[3] } else { "" }

            # Combine routes
            $fullRoute = $baseRoute
            if ($subRoute) {
                if ($fullRoute.EndsWith("/")) {
                    $fullRoute += $subRoute
                } else {
                    $fullRoute += "/" + $subRoute
                }
            }

            # Find action name in next lines
            $actionName = ""
            for ($j = $i + 1; $j -lt [Math]::Min($i + 5, $content.Count); $j++) {
                if ($content[$j] -match 'public\s+async\s+Task<[^>]+>\s+([A-Za-z0-9_]+)\(' -or $content[$j] -match 'public\s+[A-Za-z0-9_<>]+\s+([A-Za-z0-9_]+)\(') {
                    $actionName = $matches[1]
                    break
                }
            }

            $results += [PSCustomObject]@{
                Controller = $file.BaseName
                Method     = $currentHttp
                Route      = $fullRoute
                Action     = $actionName
            }
        }
    }
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "       ThinkOn ERP - Active API Endpoints Catalog       " -ForegroundColor Cyan
Write-Host "========================================================`n" -ForegroundColor Cyan

$results | Format-Table -AutoSize -Property Method, Route, Controller, Action

Write-Host "Total Endpoints Found: $($results.Count)`n" -ForegroundColor Green
