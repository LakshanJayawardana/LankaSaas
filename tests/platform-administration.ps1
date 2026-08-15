param(
    [string]$BaseUrl='http://localhost:8080',
    [Parameter(Mandatory=$true)][string]$PlatformEmail,
    [Parameter(Mandatory=$true)][string]$PlatformPassword
)
$ErrorActionPreference='Stop'
function Assert($ok,$message){if(-not $ok){throw $message}}
function StatusOf($block){try{& $block|Out-Null;return 200}catch{return [int]$_.Exception.Response.StatusCode}}

$stamp=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();$tenantPassword='SafePass!123'
function RegisterTenant($suffix){Invoke-RestMethod "$BaseUrl/api/auth/register" -Method Post -Headers @{'X-LankaSaaS-Test-Run'='true'} -ContentType application/json -Body (@{businessName="Platform test $suffix";email="platform-$suffix-$stamp@example.com";password=$tenantPassword;firstName='Test';lastName='Owner'}|ConvertTo-Json)}
$tenantA=RegisterTenant 'a';$tenantB=RegisterTenant 'b';$tenantHeaders=@{Authorization="Bearer $($tenantA.accessToken)"}
$platform=Invoke-RestMethod "$BaseUrl/api/platform/auth/login" -Method Post -ContentType application/json -Body (@{email=$PlatformEmail;password=$PlatformPassword}|ConvertTo-Json)
Assert $platform.accessToken 'Platform login failed'
$platformHeaders=@{Authorization="Bearer $($platform.accessToken)"}
$plans=@(Invoke-RestMethod "$BaseUrl/api/platform/plans" -Headers $platformHeaders|ForEach-Object{$_});Assert ($plans.Count -ge 3) 'Platform subscription plans were not available'
$plan=$plans|Select-Object -First 1
$updatedPlan=Invoke-RestMethod "$BaseUrl/api/platform/plans/$($plan.code)" -Method Put -Headers $platformHeaders -ContentType application/json -Body (@{name=$plan.name;monthlyPriceLkr=$plan.monthlyPriceLkr;userLimit=$plan.userLimit;description=$plan.description;isActive=$plan.isActive;reason='Verify protected platform plan management'}|ConvertTo-Json)
Assert ($updatedPlan.code -eq $plan.code -and $updatedPlan.monthlyPriceLkr -eq $plan.monthlyPriceLkr) 'Platform subscription plan update failed'

$ownersBefore=@(Invoke-RestMethod "$BaseUrl/api/platform/owners" -Headers $platformHeaders|ForEach-Object{$_});$ownerPassword='OwnerSafe!12345';$ownerEmail="platform-owner-$stamp@example.com"
$newOwner=Invoke-RestMethod "$BaseUrl/api/platform/owners" -Method Post -Headers $platformHeaders -ContentType application/json -Body (@{email=$ownerEmail;password=$ownerPassword}|ConvertTo-Json)
Assert ($newOwner.email -eq $ownerEmail -and $newOwner.isActive) 'A second platform owner could not be created'
$newOwnerLogin=Invoke-RestMethod "$BaseUrl/api/platform/auth/login" -Method Post -ContentType application/json -Body (@{email=$ownerEmail;password=$ownerPassword}|ConvertTo-Json);$newOwnerHeaders=@{Authorization="Bearer $($newOwnerLogin.accessToken)"}
Invoke-RestMethod "$BaseUrl/api/platform/owners/$($newOwner.id)/access" -Method Put -Headers $platformHeaders -ContentType application/json -Body (@{isActive=$false;reason='Verify immediate platform session revocation'}|ConvertTo-Json)|Out-Null
$revokedStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/platform/owners" -Headers $newOwnerHeaders}
Assert ($revokedStatus -eq 401) "Deactivated platform owner session remained valid. Status=$revokedStatus"
$activeOwners=@($ownersBefore|Where-Object {$_.isActive});if($activeOwners.Count -eq 1){$currentOwner=$activeOwners[0];Assert $currentOwner.id 'Active platform owner ID was missing from the owner listing';$lastOwnerStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/platform/owners/$($currentOwner.id)/access" -Method Put -Headers $platformHeaders -ContentType application/json -Body (@{isActive=$false;reason='Verify final-owner safety protection'}|ConvertTo-Json)};Assert ($lastOwnerStatus -eq 409) "Final active platform owner was not protected. Status=$lastOwnerStatus"}

$tenantStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/platform/tenants" -Headers $tenantHeaders}
Assert ($tenantStatus -in 401,403) "Tenant administrator accessed platform API. Status=$tenantStatus"
$platformStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/customers" -Headers $platformHeaders}
Assert ($platformStatus -eq 401) "Platform token accessed tenant API. Status=$platformStatus"

$tenants=Invoke-RestMethod "$BaseUrl/api/platform/tenants" -Headers $platformHeaders
$recordA=$tenants|Where-Object {$_.email -eq "platform-a-$stamp@example.com"};$recordB=$tenants|Where-Object {$_.email -eq "platform-b-$stamp@example.com"}
Assert ($recordA -and $recordB) 'Platform tenant listing did not return both isolated tenants'
Assert ($recordA.isTestTenant -and $recordB.isTestTenant) 'Automated test tenants were not explicitly marked'
$beforeB="$($recordB.plan)/$($recordB.status)/$($recordB.userLimit)"
$suspended=Invoke-RestMethod "$BaseUrl/api/platform/tenants/$($recordA.id)/subscription" -Method Put -Headers $platformHeaders -ContentType application/json -Body (@{plan='Starter';status='Suspended';userLimit=5;trialEndsAt=$null;subscriptionEndsAt=$null;graceEndsAt=$null;reason='Automated platform boundary verification'}|ConvertTo-Json)
Assert ($suspended.status -eq 'Suspended') 'Platform could not suspend Tenant A'
$writeStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/customers" -Method Post -Headers $tenantHeaders -ContentType application/json -Body (@{name='Should be blocked'}|ConvertTo-Json)}
Assert ($writeStatus -eq 402) "Suspended tenant mutation was not blocked. Status=$writeStatus"
$recordBAfter=Invoke-RestMethod "$BaseUrl/api/platform/tenants/$($recordB.id)" -Headers $platformHeaders
Assert ("$($recordBAfter.plan)/$($recordBAfter.status)/$($recordBAfter.userLimit)" -eq $beforeB) 'Changing Tenant A altered Tenant B subscription'
$audit=Invoke-RestMethod "$BaseUrl/api/platform/audit" -Headers $platformHeaders
Assert (@($audit|Where-Object {$_.targetTenantId -eq $recordA.id -and $_.action -eq 'tenant.subscription.updated'}).Count -ge 1) 'Platform subscription change was not audited'

$trialEndsAt=[DateTimeOffset]::UtcNow.AddDays(14).ToString('o')
Invoke-RestMethod "$BaseUrl/api/platform/tenants/$($recordA.id)/subscription" -Method Put -Headers $platformHeaders -ContentType application/json -Body (@{plan='Trial';status='Trialing';userLimit=3;trialEndsAt=$trialEndsAt;subscriptionEndsAt=$null;graceEndsAt=$null;reason='Restore automated test tenant after verification'}|ConvertTo-Json)|Out-Null
$cleanup=Invoke-RestMethod "$BaseUrl/api/platform/test-tenants/archive" -Method Post -Headers $platformHeaders -ContentType application/json -Body (@{confirmation='ARCHIVE TEST TENANTS';reason='Archive explicitly marked tenants after automated verification'}|ConvertTo-Json)
Assert ($cleanup.archivedCount -ge 2) 'Marked automated test tenants were not archived'
$archivedSessionStatus=StatusOf {Invoke-RestMethod "$BaseUrl/api/profile" -Headers $tenantHeaders}
Assert ($archivedSessionStatus -eq 401) "Archived tenant session remained valid. Status=$archivedSessionStatus"
Write-Host 'All platform administration security tests passed.' -ForegroundColor Green
