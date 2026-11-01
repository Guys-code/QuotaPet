param(
    [int]$WarmupSeconds = 12,
    [int]$SampleSeconds = 10
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $projectRoot 'artifacts\win-x64\QuotaPet.App.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Published executable not found: $exe" }

$process = Start-Process -FilePath $exe -PassThru
try {
    Start-Sleep -Seconds $WarmupSeconds
    $process.Refresh()
    if ($process.HasExited) { throw "Application exited during warmup." }
    $cpuStart = $process.TotalProcessorTime.TotalSeconds
    Start-Sleep -Seconds $SampleSeconds
    $process.Refresh()
    $cpuEnd = $process.TotalProcessorTime.TotalSeconds
    $cpuPercent = (($cpuEnd - $cpuStart) / $SampleSeconds / [Environment]::ProcessorCount) * 100
    $result = [pscustomobject]@{
        PrivateMemoryMB = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        WorkingSetMB = [math]::Round($process.WorkingSet64 / 1MB, 1)
        IdleCpuPercent = [math]::Round($cpuPercent, 3)
        MemoryTargetMet = ($process.PrivateMemorySize64 -le 60MB)
        CpuTargetMet = ($cpuPercent -le 0.2)
    }
    $result | Format-List
    if (-not $result.MemoryTargetMet -or -not $result.CpuTargetMet) { exit 1 }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
}
