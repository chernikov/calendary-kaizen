# Setup /domodel command for Telegram bot
# This script registers the bot command with Telegram

param(
    [Parameter(Mandatory=$false)]
    [string]$BotToken = "6553832531:AAE_5rBTJPLeLox7hR2nqUhZjKW3mRwSk_Y",
    
    [Parameter(Mandatory=$false)]
    [string]$KeyVaultName = "calendary-kaizen-kv-prod"
)

# Get bot token from Azure Key Vault if not provided via environment variable
if ([string]::IsNullOrEmpty($BotToken)) {
    Write-Host "Retrieving Telegram bot token from Azure Key Vault..." -ForegroundColor Cyan
    try {
        $BotToken = az keyvault secret show --vault-name $KeyVaultName --name "TelegramBotToken" --query "value" -o tsv
        if ([string]::IsNullOrEmpty($BotToken)) {
            Write-Error "Failed to retrieve TelegramBotToken from Key Vault"
            exit 1
        }
        Write-Host "✅ Token retrieved successfully" -ForegroundColor Green
    } catch {
        Write-Error "Failed to retrieve token from Key Vault: $_"
        Write-Host "Make sure you are logged in to Azure CLI and have access to the Key Vault" -ForegroundColor Yellow
        exit 1
    }
}

# Define bot commands
$commands = @(
    @{
        command = "domodel"
        description = "Створити та натренувати FLUX модель з завантажених зображень (тригер слово: TOK). Використання: /domodel"
    },
     @{
        command = "status"
        description = "Перевірити статус тренування вашої FLUX моделі. Використання: /status"
    }
)

Write-Host "Setting up Telegram bot commands..." -ForegroundColor Cyan
Write-Host ""

try {
    # Prepare request body
    $requestBody = @{
        commands = $commands
    } | ConvertTo-Json -Depth 3

    # Call Telegram API to set commands
    $response = Invoke-RestMethod -Uri "https://api.telegram.org/bot$BotToken/setMyCommands" `
        -Method Post `
        -ContentType "application/json" `
        -Body $requestBody

    if ($response.ok) {
        Write-Host "✅ Bot commands registered successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Available commands:" -ForegroundColor White
        foreach ($cmd in $commands) {
            Write-Host "  /$($cmd.command) - $($cmd.description)" -ForegroundColor Gray
        }
    } else {
        Write-Error "❌ Error: $($response.description)"
        exit 1
    }
} catch {
    Write-Error "Failed to register bot commands: $_"
    exit 1
}
