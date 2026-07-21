param(
    [string]$BaseUrl = "http://127.0.0.1:5160",
    [string]$OutputDirectory = "docs/openapi"
)

$ErrorActionPreference = "Stop"
$resolvedOutput = Join-Path (Get-Location) $OutputDirectory
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

$surfaces = @("superadmin", "company")
$documents = @{}

foreach ($surface in $surfaces) {
    $uri = "$($BaseUrl.TrimEnd('/'))/swagger/$surface/swagger.json"
    $outputPath = Join-Path $resolvedOutput "$surface.json"
    Invoke-WebRequest -Uri $uri -UseBasicParsing -OutFile $outputPath
    $documents[$surface] = Get-Content -Raw $outputPath | ConvertFrom-Json
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# Current API endpoint catalog")
$lines.Add("")
$lines.Add("Generated from the running application's Swagger documents. Re-run ``scripts/generate-api-docs.ps1`` while the API is running to refresh this catalog and the OpenAPI JSON files.")
$lines.Add("")
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
$lines.Add("")

foreach ($surface in $surfaces) {
    $document = $documents[$surface]
    $operations = [System.Collections.Generic.List[object]]::new()

    foreach ($pathProperty in @($document.paths.PSObject.Properties)) {
        foreach ($methodProperty in @($pathProperty.Value.PSObject.Properties)) {
            if ($methodProperty.Name -notin @("get", "post", "put", "delete", "patch")) {
                continue
            }

            $operation = $methodProperty.Value
            $requestType = "-"
            if ($operation.requestBody) {
                $contentTypes = @($operation.requestBody.content.PSObject.Properties.Name)
                $requestType = if ($contentTypes.Count -gt 0) { $contentTypes -join ", " } else { "body" }
            }

            $responses = @($operation.responses.PSObject.Properties.Name) -join ", "
            $summary = if ([string]::IsNullOrWhiteSpace($operation.summary)) { "-" } else { $operation.summary }
            $summary = ($summary -replace "\|", "\\|") -replace "[\r\n]+", " "

            $operations.Add([pscustomobject]@{
                Method = $methodProperty.Name.ToUpperInvariant()
                Path = $pathProperty.Name
                Summary = $summary
                Request = $requestType
                Responses = $responses
            })
        }
    }

    $lines.Add("## $($document.info.title)")
    $lines.Add("")
    $lines.Add("$($operations.Count) operations across $(@($document.paths.PSObject.Properties).Count) paths. Full schemas and examples are in [$surface.json](openapi/$surface.json).")
    $lines.Add("")
    $lines.Add("| Method | Path | Summary | Request content | Documented responses |")
    $lines.Add("|---|---|---|---|---|")
    foreach ($operation in $operations | Sort-Object Path, Method) {
        $lines.Add("| $($operation.Method) | ``$($operation.Path)`` | $($operation.Summary) | $($operation.Request) | $($operation.Responses) |")
    }
    $lines.Add("")
}

$catalogPath = Join-Path (Split-Path $resolvedOutput -Parent) "API_ENDPOINT_CATALOG.md"
[System.IO.File]::WriteAllLines($catalogPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $catalogPath"
foreach ($surface in $surfaces) {
    Write-Output "Generated $(Join-Path $resolvedOutput "$surface.json")"
}
