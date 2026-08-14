param([string]$BaseUrl='http://localhost:8080')

$ErrorActionPreference='Stop'
function Assert($ok,$message){if(-not $ok){throw $message}}
function Headers($token){@{Authorization="Bearer $token"}}
function ExpectStatus([int]$status,[scriptblock]$action,[string]$message){try{&$action|Out-Null;throw "$message (request unexpectedly succeeded)"}catch{if($_.Exception.Response.StatusCode.value__ -ne $status){throw "$message (expected $status, received $($_.Exception.Response.StatusCode.value__))"}}}
function Rank([string]$level){switch($level){'Viewer'{1}'Member'{2}'Manager'{3}default{0}}}
function InvokeAuth([string]$path,[hashtable]$body){$maximumAttempts=20;for($attempt=1;$attempt -le $maximumAttempts;$attempt++){try{return Invoke-RestMethod "$BaseUrl$path" -Method Post -ContentType application/json -Body ($body|ConvertTo-Json)}catch{$status=[int]$_.Exception.Response.StatusCode;if($status -ne 429 -or $attempt -eq $maximumAttempts){throw};$retryText=[string]$_.Exception.Response.Headers['Retry-After'];$delay=10;$seconds=0;$retryAt=[DateTimeOffset]::MinValue;if([int]::TryParse($retryText,[ref]$seconds)){$delay=[Math]::Max(1,[Math]::Min(120,$seconds+1))}elseif([DateTimeOffset]::TryParse($retryText,[ref]$retryAt)){$delay=[Math]::Max(1,[Math]::Min(120,[int][Math]::Ceiling(($retryAt-[DateTimeOffset]::UtcNow).TotalSeconds)+1))};Write-Host "Authentication rate limit reached; retrying in $delay seconds ($attempt/$maximumAttempts)..." -ForegroundColor Yellow;Start-Sleep -Seconds $delay}}}

$stamp=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();$password='SafePass!123'
function Register([string]$name){InvokeAuth '/api/auth/register' @{businessName="Matrix $name";email="matrix-$name-$stamp@example.com";password=$password;firstName='Matrix';lastName='Admin'}}
function Login([string]$email){InvokeAuth '/api/auth/login' @{email=$email;password=$password}}

$tenantA=Register 'a';$tenantB=Register 'b';$adminA=Headers $tenantA.accessToken;$adminB=Headers $tenantB.accessToken
$staffEmail="matrix-staff-$stamp@example.com"
$staff=Invoke-RestMethod "$BaseUrl/api/users" -Method Post -Headers $adminA -ContentType application/json -Body (@{firstName='Matrix';lastName='Staff';email=$staffEmail;password=$password;role='Staff'}|ConvertTo-Json)
$departments=Invoke-RestMethod "$BaseUrl/api/departments" -Headers $adminA;$tenantBDepartments=Invoke-RestMethod "$BaseUrl/api/departments" -Headers $adminB
$standardCodes=@('ADMINISTRATION','EVENTS','LOGISTICS','FINANCE','PURCHASING','PEOPLE','GENERAL');$availableCodes=@($departments|ForEach-Object {$_.code});$missingCodes=@($standardCodes|Where-Object {$availableCodes -notcontains $_})
Assert ($missingCodes.Count -eq 0) "Permission matrix is missing standard departments. Count=$($departments.Count), Codes=$($availableCodes -join ','), Missing=$($missingCodes -join ',')"
$departments=@($departments|Where-Object {$standardCodes -contains $_.code})

$templateExpectations=@{
 ADMINISTRATION=@('administration.audit|Manager','administration.billing|Manager','administration.settings|Manager','administration.users|Manager');
 EVENTS=@('attendance.self|Viewer','contacts.manage|Member','contacts.view|Viewer','events.change_status|Manager','events.manage|Member','events.view|Viewer','finance.quotations|Member','finance.view|Viewer','logistics.view|Viewer','staffing.manage|Member','staffing.view|Viewer');
 LOGISTICS=@('attendance.self|Viewer','events.view|Viewer','logistics.manage|Manager','logistics.operate|Member','logistics.view|Viewer');
 FINANCE=@('accounting.post_journals|Manager','accounting.view|Viewer','attendance.self|Viewer','events.view|Viewer','finance.manage|Manager','finance.payments|Member','finance.quotations|Member','finance.view|Viewer','purchasing.view|Viewer');
 PURCHASING=@('attendance.self|Viewer','events.view|Viewer','logistics.view|Viewer','purchasing.manage|Manager','purchasing.operate|Member','purchasing.view|Viewer');
 PEOPLE=@('attendance.override|Manager','attendance.self|Viewer','events.view|Viewer','staffing.manage|Member','staffing.view|Viewer');
 GENERAL=@('attendance.self|Viewer','events.view|Viewer','staffing.view|Viewer')
}
foreach($department in $departments){$expectedTemplate=@($templateExpectations[$department.code]|Sort-Object);$actualTemplate=@($department.permissions|ForEach-Object {"$($_.permissionCode)|$($_.minimumAccessLevel)"}|Sort-Object);Assert (($expectedTemplate -join ',') -eq ($actualTemplate -join ',')) "Standard template changed for $($department.code). Expected=$($expectedTemplate -join ','), Actual=$($actualTemplate -join ',')"}

$catalogue=Invoke-RestMethod "$BaseUrl/api/departments/permissions" -Headers $adminA;$matrixPermissions=@($catalogue|Where-Object {-not $_.StartsWith('administration.')}|ForEach-Object {$level=if($_ -match '\.view$' -or $_ -eq 'attendance.self'){'Viewer'}elseif($_ -match '\.manage$' -or $_ -in @('events.change_status','accounting.post_journals','attendance.override')){'Manager'}else{'Member'};@{permissionCode=$_;minimumAccessLevel=$level}})
$matrixDepartment=Invoke-RestMethod "$BaseUrl/api/departments" -Method Post -Headers $adminA -ContentType application/json -Body (@{name='Release Matrix';code="MATRIX_$stamp";isActive=$true;permissions=$matrixPermissions}|ConvertTo-Json -Depth 8)
$levels=@('Viewer','Member','Manager');$lastToken=(Login $staffEmail).accessToken;$checks=0
foreach($level in $levels){
 Invoke-RestMethod "$BaseUrl/api/users/$($staff.id)/departments" -Method Put -Headers $adminA -ContentType application/json -Body (@{departments=@(@{departmentId=$matrixDepartment.id;accessLevel=$level;isPrimary=$true})}|ConvertTo-Json -Depth 5)|Out-Null
 ExpectStatus 401 {Invoke-RestMethod "$BaseUrl/api/profile" -Headers (Headers $lastToken)} "Old token survived $level reassignment"
 $lastToken=(Login $staffEmail).accessToken;$access=Invoke-RestMethod "$BaseUrl/api/departments/my-access" -Headers (Headers $lastToken)
 $expected=@($matrixPermissions|Where-Object {(Rank $level) -ge (Rank $_.minimumAccessLevel)}|ForEach-Object {$_.permissionCode}|Sort-Object -Unique);$actual=@($access.permissions|Sort-Object -Unique)
 Assert (-not $access.isAdministrator) 'Staff account was treated as an administrator';Assert (($expected -join '|') -eq ($actual -join '|')) "Incorrect effective permissions for $level. Expected=$($expected -join ','), Actual=$($actual -join ',')";$checks++
}

$logistics=$departments|Where-Object {$_.code -eq 'LOGISTICS'}|Select-Object -First 1;$finance=$departments|Where-Object {$_.code -eq 'FINANCE'}|Select-Object -First 1
$logisticsId=[Guid]::Empty;$financeId=[Guid]::Empty;Assert ([Guid]::TryParse([string]$logistics.id,[ref]$logisticsId) -and [Guid]::TryParse([string]$finance.id,[ref]$financeId)) "Could not resolve one Logistics and Finance department. Codes=$(@($departments|ForEach-Object {$_.code}) -join ','), LogisticsId=$($logistics.id), FinanceId=$($finance.id)"
Invoke-RestMethod "$BaseUrl/api/users/$($staff.id)/departments" -Method Put -Headers $adminA -ContentType application/json -Body (@{departments=@(@{departmentId=$logistics.id;accessLevel='Member';isPrimary=$true},@{departmentId=$finance.id;accessLevel='Viewer';isPrimary=$false})}|ConvertTo-Json -Depth 5)|Out-Null
$lastToken=(Login $staffEmail).accessToken;$combined=Invoke-RestMethod "$BaseUrl/api/departments/my-access" -Headers (Headers $lastToken)
Assert ($combined.permissions -contains 'logistics.operate' -and $combined.permissions -contains 'accounting.view' -and -not ($combined.permissions -contains 'finance.manage')) 'Multi-department permission union is incorrect'

Invoke-RestMethod "$BaseUrl/api/departments/$($logistics.id)" -Method Put -Headers $adminA -ContentType application/json -Body (@{name=$logistics.name;code=$logistics.code;isActive=$false;permissions=$logistics.permissions}|ConvertTo-Json -Depth 8)|Out-Null
$afterDisable=Invoke-RestMethod "$BaseUrl/api/departments/my-access" -Headers (Headers $lastToken)
Assert (-not ($afterDisable.permissions -contains 'logistics.view') -and $afterDisable.permissions -contains 'accounting.view') 'Inactive department permissions remained effective'
Invoke-RestMethod "$BaseUrl/api/departments/$($logistics.id)" -Method Put -Headers $adminA -ContentType application/json -Body (@{name=$logistics.name;code=$logistics.code;isActive=$true;permissions=$logistics.permissions}|ConvertTo-Json -Depth 8)|Out-Null
Invoke-RestMethod "$BaseUrl/api/departments/$($matrixDepartment.id)" -Method Delete -Headers $adminA|Out-Null

ExpectStatus 404 {Invoke-RestMethod "$BaseUrl/api/users/$($staff.id)/departments" -Method Put -Headers $adminB -ContentType application/json -Body (@{departments=@(@{departmentId=$tenantBDepartments[0].id;accessLevel='Manager';isPrimary=$true})}|ConvertTo-Json -Depth 5)} 'Cross-tenant department assignment was not rejected'
ExpectStatus 403 {Invoke-RestMethod "$BaseUrl/api/departments" -Headers (Headers $lastToken)} 'Staff accessed department administration'

Write-Host "Department access matrix passed: 7 standard templates, $checks runtime access levels, multi-department union, inactive department, session revocation and tenant isolation." -ForegroundColor Green
