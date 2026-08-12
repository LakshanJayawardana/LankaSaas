param([string]$BaseUrl='http://localhost:8080')
$ErrorActionPreference='Stop'; function Assert($ok,$message){if(-not $ok){throw $message}}
try { Invoke-RestMethod "$BaseUrl/api/customers" | Out-Null; throw 'Unauthorized endpoint accepted request' } catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 401) 'Expected 401' }
$stamp=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds(); $pass='SafePass!123'
function Register($n){Invoke-RestMethod "$BaseUrl/api/auth/register" -Method Post -ContentType application/json -Body (@{businessName="Tenant $n";email="tenant$n-$stamp@example.com";password=$pass;firstName='Test';lastName='Admin'}|ConvertTo-Json)}
$a=Register 'A'; $b=Register 'B'; Assert ($a.accessToken -and $b.accessToken) 'Registration failed'
$login=Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -ContentType application/json -Body (@{email="tenantA-$stamp@example.com";password=$pass}|ConvertTo-Json); Assert $login.accessToken 'Login failed'
$ha=@{Authorization="Bearer $($a.accessToken)"};$hb=@{Authorization="Bearer $($b.accessToken)"}
$customer=Invoke-RestMethod "$BaseUrl/api/customers" -Method Post -Headers $ha -ContentType application/json -Body (@{name='Tenant A Customer'}|ConvertTo-Json); Assert $customer.id 'Customer creation failed'
$product=Invoke-RestMethod "$BaseUrl/api/products" -Method Post -Headers $ha -ContentType application/json -Body (@{name='Tea';sku="TEA-$stamp";sellingPrice=100;costPrice=80;stockQuantity=10;isActive=$true}|ConvertTo-Json); Assert $product.id 'Product creation failed'
$invoice=Invoke-RestMethod "$BaseUrl/api/invoices" -Method Post -Headers $ha -ContentType application/json -Body (@{customerId=$customer.id;issueDate=(Get-Date -Format yyyy-MM-dd);dueDate=(Get-Date -Format yyyy-MM-dd);items=@(@{productId=$product.id;description='Tea';quantity=2;unitPrice=100;discount=10;taxRate=10})}|ConvertTo-Json -Depth 5); Assert ($invoice.total -eq 209) 'Invoice total calculation failed'
try { Invoke-RestMethod "$BaseUrl/api/invoices/$($invoice.id)" -Headers $hb | Out-Null; throw 'Cross-tenant invoice was exposed' } catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 404) 'Expected tenant-safe invoice 404' }
try { Invoke-RestMethod "$BaseUrl/api/customers/$($customer.id)" -Headers $hb | Out-Null; throw 'Cross-tenant customer was exposed' } catch { Assert ($_.Exception.Response.StatusCode.value__ -eq 404) 'Expected tenant-safe 404' }
$seen=Invoke-RestMethod "$BaseUrl/api/customers" -Headers $hb; Assert ($seen.Count -eq 0) 'Tenant B can see tenant A data'; Write-Host 'All API smoke tests passed.' -ForegroundColor Green
