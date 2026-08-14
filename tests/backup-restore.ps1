param([string]$BackupDirectory='backups',[int]$RetentionDays=14)
$ErrorActionPreference='Stop'
function Assert($ok,$message){if(-not $ok){throw $message}}
function RunDocker([string[]]$Arguments){& docker @Arguments;if($LASTEXITCODE -ne 0){throw "Docker command failed: docker $($Arguments -join ' ')"}}

Assert ($RetentionDays -ge 1) 'RetentionDays must be at least 1'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path;$backupRoot=[IO.Path]::GetFullPath((Join-Path $root $BackupDirectory));$rootPrefix=$root.TrimEnd('\')+'\'
Assert ($backupRoot.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase)) 'BackupDirectory must remain inside the repository workspace'
if(-not(Test-Path -LiteralPath $backupRoot)){New-Item -ItemType Directory -Path $backupRoot|Out-Null}
$containerId=(& docker-compose ps -q db).Trim();Assert ($LASTEXITCODE -eq 0 -and $containerId) 'PostgreSQL container is not running'

$envValues=@{};Get-Content (Join-Path $root '.env')|ForEach-Object{if($_ -match '^\s*([^#][^=]*)=(.*)$'){$envValues[$matches[1].Trim()]=$matches[2].Trim()}}
$dbUser=if($envValues.POSTGRES_USER){$envValues.POSTGRES_USER}else{'postgres'};$dbName=if($envValues.POSTGRES_DB){$envValues.POSTGRES_DB}else{'lankasaas'}
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ');$file=Join-Path $backupRoot "lankasaas-$stamp.backup";$partial="$file.partial";$containerBackup='/tmp/lankasaas-backup-verify.dump'

RunDocker @('exec',$containerId,'pg_dump','-U',$dbUser,'-d',$dbName,'-Fc','-f',$containerBackup)
RunDocker @('cp',"${containerId}:$containerBackup",$partial)
Assert ((Get-Item -LiteralPath $partial).Length -gt 0) 'Backup file is empty'
Move-Item -LiteralPath $partial -Destination $file
$hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant();[IO.File]::WriteAllText("$file.sha256","$hash  $([IO.Path]::GetFileName($file))`n",[Text.UTF8Encoding]::new($false))

$copiedHash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant();Assert ($copiedHash -eq $hash) 'Backup checksum validation failed'
$verifyDb="lankasaas_restore_verify_$([DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss'))";Assert ($verifyDb -match '^lankasaas_restore_verify_[0-9]{14}$') 'Unsafe restore verification database name'
$containerRestore='/tmp/lankasaas-restore-verify.dump';RunDocker @('cp',$file,"${containerId}:$containerRestore")
try{
 RunDocker @('exec',$containerId,'createdb','-U',$dbUser,$verifyDb)
 RunDocker @('exec',$containerId,'pg_restore','-U',$dbUser,'-d',$verifyDb,'--exit-on-error',$containerRestore)
 $tables=(& docker exec $containerId psql -U $dbUser -d $verifyDb -Atc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';").Trim();Assert ($LASTEXITCODE -eq 0 -and [int]$tables -gt 0) 'Restored database contains no public tables'
 RunDocker @('exec',$containerId,'psql','-U',$dbUser,'-d',$verifyDb,'-v','ON_ERROR_STOP=1','-c','ANALYZE;')
 $migrationOutput=& docker exec $containerId psql -U $dbUser -d $verifyDb -Atc "SELECT n_live_tup::bigint FROM pg_stat_user_tables WHERE relname='__EFMigrationsHistory';";Assert ($LASTEXITCODE -eq 0 -and $null -ne $migrationOutput) 'Could not inspect restored EF migration history';$migrations=("$migrationOutput").Trim();Assert ($migrations -match '^[0-9]+$' -and [int]$migrations -gt 0) 'Restored database contains no EF migration history'
 $tenantOutput=& docker exec $containerId psql -U $dbUser -d $verifyDb -Atc "SELECT n_live_tup::bigint FROM pg_stat_user_tables WHERE relname='Tenants';";Assert ($LASTEXITCODE -eq 0 -and $null -ne $tenantOutput) 'Could not inspect restored tenant data';$tenants=("$tenantOutput").Trim();Assert ($tenants -match '^[0-9]+$' -and [int]$tenants -ge 0) 'Restored tenant count is invalid'
 Write-Host "Restore verification passed: $tables tables, $migrations migrations, $tenants tenants." -ForegroundColor Green
}finally{
 if($verifyDb -match '^lankasaas_restore_verify_[0-9]{14}$'){& docker exec $containerId dropdb -U $dbUser --if-exists $verifyDb|Out-Null}
}

$cutoff=[DateTime]::UtcNow.AddDays(-$RetentionDays);$removed=0
Get-ChildItem -LiteralPath $backupRoot -File|Where-Object{$_.Name -match '^lankasaas-[0-9]{8}T[0-9]{6}Z\.backup(\.sha256)?$' -and $_.LastWriteTimeUtc -lt $cutoff}|ForEach-Object{$resolved=[IO.Path]::GetFullPath($_.FullName);Assert ($resolved.StartsWith($backupRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) 'Unsafe retention target';Remove-Item -LiteralPath $resolved;$removed++}
Write-Host "Backup created and verified: $file" -ForegroundColor Green
Write-Host "Retention removed $removed files older than $RetentionDays days."
