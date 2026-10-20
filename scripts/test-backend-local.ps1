param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
$workspace = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$localRoot = [System.IO.Path]::GetFullPath((Join-Path $workspace '.local'))
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $localRoot ('postgres-tests-' + [Guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($localRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Test data path escaped the workspace local directory.'
}
$previousConnection = $env:OURSPACE_TEST_POSTGRES
$previousPassword = $env:PGPASSWORD
$started = $false
$exitStatus = 1
Push-Location $workspace
try {
    foreach ($tool in @('initdb.exe', 'pg_ctl.exe', 'createdb.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PostgresBin $tool))) { throw "PostgreSQL tool missing: $tool" }
    }
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $data = Join-Path $testRoot 'data'
    $passwordFile = Join-Path $testRoot 'password.txt'
    $password = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
    [System.IO.File]::WriteAllText($passwordFile, $password, (New-Object System.Text.UTF8Encoding($false)))
    & (Join-Path $PostgresBin 'initdb.exe') -D $data -U ourspace_test --auth=scram-sha-256 --pwfile=$passwordFile --encoding=UTF8 --locale=C
    if ($LASTEXITCODE -ne 0) { throw 'Disposable PostgreSQL initialization failed.' }
    Remove-Item -LiteralPath $passwordFile -Force
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    & (Join-Path $PostgresBin 'pg_ctl.exe') start -D $data -l (Join-Path $testRoot 'server.log') -o "-h 127.0.0.1 -p $port" -w -t 30
    if ($LASTEXITCODE -ne 0) { throw 'Disposable PostgreSQL startup failed.' }
    $started = $true
    $env:PGPASSWORD = $password
    & (Join-Path $PostgresBin 'createdb.exe') -h 127.0.0.1 -p $port -U ourspace_test ourspace_test_admin
    if ($LASTEXITCODE -ne 0) { throw 'Test administration database creation failed.' }
    $env:OURSPACE_TEST_POSTGRES = "Host=127.0.0.1;Port=$port;Database=ourspace_test_admin;Username=ourspace_test;Password=$password"
    if ($Filter) {
        & dotnet test OurSpace.slnx --no-restore --filter $Filter
    } else {
        & node scripts/check-backend.mjs
    }
    $exitStatus = $LASTEXITCODE
}
finally {
    $safeToRemove = -not $started
    if ($started) {
        & (Join-Path $PostgresBin 'pg_ctl.exe') stop -D $data -m fast -w -t 30
        $safeToRemove = $LASTEXITCODE -eq 0
    }
    $env:OURSPACE_TEST_POSTGRES = $previousConnection
    $env:PGPASSWORD = $previousPassword
    if ($safeToRemove -and (Test-Path -LiteralPath $testRoot)) {
        # Only the absolute, verified, newly generated cluster directory is removed.
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
    Pop-Location
}
if ($exitStatus -ne 0) { throw "Backend tests failed (exit $exitStatus)." }
