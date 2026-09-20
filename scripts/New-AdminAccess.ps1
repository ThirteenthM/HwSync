<#
.SYNOPSIS
Создаёт персональный ключ и запись пользователя управления без изменения конфигурации.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Name
)

$random = [Security.Cryptography.RandomNumberGenerator]::Create()
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $bytes = New-Object byte[] 32
    $random.GetBytes($bytes)
    $accessToken = [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    $hash = [BitConverter]::ToString(
        $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($accessToken))
    ).Replace('-', '').ToLowerInvariant()
    [pscustomobject]@{
        AccessToken = $accessToken
        ServerUser = [ordered]@{
            Name = $Name
            TokenSha256 = $hash
            CanRead = $true
        }
    } | ConvertTo-Json -Depth 3
}
finally {
    $random.Dispose()
    $sha256.Dispose()
}
