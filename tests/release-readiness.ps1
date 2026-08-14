param(
 [Parameter(Mandatory=$true)][string]$PlatformEmail,
 [Parameter(Mandatory=$true)][string]$PlatformPassword,
 [string]$BaseUrl='http://localhost:8080',
 [string]$WebUrl='http://localhost:3001',
 [int]$MaximumBackupAgeHours=30,
 [switch]$AllowWorkingTreeChanges
)
$ErrorActionPreference='Stop';$results=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Action){try{$detail=&$Action;$script:results.Add([pscustomobject]@{Check=$Name;Result='PASS';Detail=[string]$detail});Write-Host "PASS  $Name" -ForegroundColor Green}catch{$script:results.Add([pscustomobject]@{Check=$Name;Result='FAIL';Detail=$_.Exception.Message});Write-Host "FAIL  $Name - $($_.Exception.Message)" -ForegroundColor Red}}
function Assert($ok,$message){if(-not $ok){throw $message}}
function Compose([string[]]$Arguments){if(Get-Command docker-compose -ErrorAction SilentlyContinue){& docker-compose @Arguments}else{& docker compose @Arguments};if($LASTEXITCODE -ne 0){throw "Docker Compose command failed"}}

Check 'Tracked working tree' {if(-not $AllowWorkingTreeChanges){$changes=git status --porcelain --untracked-files=no;Assert (-not $changes) 'Tracked changes must be committed'};'clean or explicitly allowed'}
Check 'Compose configuration' {Compose @('config','--quiet');'valid'}
Check 'Container state' {$ids=@();foreach($service in 'db','api','web'){if(Get-Command docker-compose -ErrorAction SilentlyContinue){$id=(& docker-compose ps -q $service).Trim()}else{$id=(& docker compose ps -q $service).Trim()};Assert $id "$service container is missing";$state=(& docker inspect -f '{{.State.Status}}' $id).Trim();Assert ($state -eq 'running') "$service is $state";$ids+=$id};"$($ids.Count) required containers running"}
Check 'API liveness' {$response=Invoke-WebRequest "$BaseUrl/health/live" -UseBasicParsing;Assert ($response.StatusCode -eq 200) 'Liveness did not return 200';Assert $response.Headers['X-Correlation-ID'] 'Correlation header is missing';'live with correlation ID'}
Check 'API readiness' {$ready=Invoke-RestMethod "$BaseUrl/health/ready";Assert ($ready.status -eq 'ready' -and $ready.database -eq 'available') 'Database readiness failed';'database available'}
Check 'Web availability' {$response=Invoke-WebRequest $WebUrl -UseBasicParsing;Assert ($response.StatusCode -eq 200) 'Web application did not return 200';'HTTP 200'}
Check 'Tenant isolation smoke tests' {& "$PSScriptRoot/api-smoke.ps1" -BaseUrl $BaseUrl;'passed'}
Check 'Department access matrix' {& "$PSScriptRoot/department-access-matrix.ps1" -BaseUrl $BaseUrl;'passed'}
Check 'Platform security matrix' {& "$PSScriptRoot/platform-administration.ps1" -BaseUrl $BaseUrl -PlatformEmail $PlatformEmail -PlatformPassword $PlatformPassword;'passed'}
Check 'Backup and isolated restore' {& "$PSScriptRoot/backup-restore.ps1" -RetentionDays 14;'passed'}
Check 'Backup freshness and checksum' {$backupRoot=Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'backups';$latest=Get-ChildItem -LiteralPath $backupRoot -Filter 'lankasaas-*.backup' -File|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1;Assert $latest 'No backup found';$age=([DateTime]::UtcNow-$latest.LastWriteTimeUtc).TotalHours;Assert ($age -le $MaximumBackupAgeHours) "Latest backup is $([Math]::Round($age,1)) hours old";$sidecar="$($latest.FullName).sha256";Assert (Test-Path -LiteralPath $sidecar) 'Checksum sidecar is missing';$expected=((Get-Content -LiteralPath $sidecar -Raw).Trim() -split '\s+')[0];$actual=(Get-FileHash -LiteralPath $latest.FullName -Algorithm SHA256).Hash;Assert ($actual.Equals($expected,[StringComparison]::OrdinalIgnoreCase)) 'Backup checksum mismatch';"$([Math]::Round($age,1)) hours old and valid"}

$failed=@($results|Where-Object Result -eq 'FAIL');$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ');$outputRoot=Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) 'outputs';if(-not(Test-Path $outputRoot)){New-Item -ItemType Directory -Path $outputRoot|Out-Null};$report=Join-Path $outputRoot "release-readiness-$stamp.md";$lines=@('# LankaSaaS release readiness','',"Generated UTC: $([DateTimeOffset]::UtcNow.ToString('o'))",'',"Decision: **$(if($failed.Count){'NO-GO'}else{'GO'})**",'','| Check | Result | Detail |','|---|---|---|')+@($results|ForEach-Object{"| $($_.Check) | $($_.Result) | $($_.Detail.Replace('|','/')) |"});[IO.File]::WriteAllLines($report,$lines,[Text.UTF8Encoding]::new($false));Write-Host "Report: $report";if($failed.Count){throw "Release readiness failed $($failed.Count) checks. Decision: NO-GO"};Write-Host 'All release gates passed. Decision: GO' -ForegroundColor Green
