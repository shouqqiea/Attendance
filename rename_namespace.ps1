$files = Get-ChildItem -Path "LetsCheckIn" -Recurse -Include *.cs,*.cshtml
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $newContent = $content -replace "namespace AttendEase", "namespace LetsCheckIn" `
                          -replace "using AttendEase", "using LetsCheckIn" `
                          -replace "AttendEase.Models", "LetsCheckIn.Models" `
                          -replace "AttendEase.Controllers", "LetsCheckIn.Controllers" `
                          -replace "AttendEase.Migrations", "LetsCheckIn.Migrations"
    if ($content -ne $newContent) {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "Updated $($file.Name)"
    }
} 