$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\QuotaPet.App\QuotaPet.App.csproj'
$deployRoot = Join-Path (Split-Path -Parent $projectRoot) 'QuotaPet-deploy'
$output = Join-Path $deployRoot 'win-x64'
$assetsSource = Join-Path $projectRoot 'src\QuotaPet.App\Assets'
$assetsOutput = Join-Path $output 'Assets'

if (-not $output.StartsWith($deployRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an output directory outside QuotaPet-deploy: $output"
}
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$sdkRoot = Join-Path $env:ProgramFiles 'dotnet\sdk'
$sdkVersion = Get-ChildItem -LiteralPath $sdkRoot -Directory |
    Where-Object Name -Match '^\d+\.' |
    Sort-Object { [Version]$_.Name } |
    Select-Object -Last 1 -ExpandProperty Name
if (-not $sdkVersion) { throw "No .NET SDK was found under $sdkRoot" }

$msbuild = Join-Path $sdkRoot "$sdkVersion\MSBuild.dll"
dotnet exec $msbuild $project -restore -t:Publish `
    -p:Configuration=Release `
    -p:RuntimeIdentifier=win-x64 `
    -p:SelfContained=true `
    "-p:RestoreSources=$env:USERPROFILE\.nuget\packages" `
    -p:RestoreIgnoreFailedSources=true `
    "-p:PublishDir=$output" `
    -nodeReuse:false `
    -m:1
if ($LASTEXITCODE -ne 0) { throw "MSBuild publish failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Path $assetsOutput -Force | Out-Null
Get-ChildItem -LiteralPath $assetsSource -Filter 'pet*.png' -File |
    Copy-Item -Destination $assetsOutput -Force

Write-Output "Published: $output"
