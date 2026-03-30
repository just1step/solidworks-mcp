$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot '..')
$pipeName = 'SolidWorksMcpBridge'

function Test-BridgePipe {
    param(
        [string]$Name,
        [int]$TimeoutMs = 500
    )

    $client = $null
    try {
        $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $Name, [System.IO.Pipes.PipeDirection]::InOut)
        $client.Connect($TimeoutMs)
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $client) {
            $client.Dispose()
        }
    }
}

function Resolve-BridgeLaunchTarget {
    $releaseExe = Join-Path $repoRoot 'bridge\SolidWorksBridge\bin\Release\net8.0-windows\win-x64\SolidWorksBridge.exe'
    if (Test-Path $releaseExe) {
        return [PSCustomObject]@{
            FilePath = $releaseExe
            Arguments = @()
            WorkingDirectory = Split-Path -Parent $releaseExe
        }
    }

    $debugExe = Join-Path $repoRoot 'bridge\SolidWorksBridge\bin\Debug\net8.0-windows\win-x64\SolidWorksBridge.exe'
    if (Test-Path $debugExe) {
        return [PSCustomObject]@{
            FilePath = $debugExe
            Arguments = @()
            WorkingDirectory = Split-Path -Parent $debugExe
        }
    }

    $projectFile = Join-Path $repoRoot 'bridge\SolidWorksBridge\SolidWorksBridge.csproj'
    if (Test-Path $projectFile) {
        return [PSCustomObject]@{
            FilePath = 'dotnet'
            Arguments = @('run', '--project', $projectFile)
            WorkingDirectory = Split-Path -Parent $projectFile
        }
    }

    throw 'SolidWorksBridge launch target was not found. Run scripts\deploy-local.bat first.'
}

if (Test-BridgePipe -Name $pipeName) {
    exit 0
}

$launchTarget = Resolve-BridgeLaunchTarget
Start-Process -FilePath $launchTarget.FilePath -ArgumentList $launchTarget.Arguments -WorkingDirectory $launchTarget.WorkingDirectory -WindowStyle Hidden | Out-Null

$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) {
    if (Test-BridgePipe -Name $pipeName) {
        exit 0
    }

    Start-Sleep -Milliseconds 500
}

throw 'Bridge start command was issued, but SolidWorksMcpBridge was not ready within 15 seconds.'