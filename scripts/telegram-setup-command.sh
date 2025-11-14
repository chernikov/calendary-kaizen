#!/bin/bash
# Setup /domodel command for Telegram bot
# This script registers the bot command with Telegram

set -e

# Configuration
BOT_TOKEN="${TELEGRAM_BOT_TOKEN}"
KEY_VAULT_NAME="${KEY_VAULT_NAME:-calendary-kaizen-kv-prod}"

# Get bot token from Azure Key Vault if not provided via environment variable
if [ -z "$BOT_TOKEN" ]; then
    echo "Retrieving Telegram bot token from Azure Key Vault..."
    BOT_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "TelegramBotToken" --query "value" -o tsv)
    
    if [ -z "$BOT_TOKEN" ]; then
        echo "Error: Failed to retrieve TelegramBotToken from Key Vault"
        exit 1
    fi
    echo "✅ Token retrieved successfully"
fi

# Define bot commands
COMMANDS='[
  {
    "command": "domodel",
    "description": "Створити та натренувати FLUX модель з завантажених зображень (тригер слово: TOK). Використання: /domodel"
  },
  {
    "command": "status",
    "description": "Перевірити статус тренування вашої FLUX моделі. Використання: /status"
  }
]'

echo "Setting up Telegram bot commands..."
echo ""

# Call Telegram API to set commands
RESPONSE=$(curl -s -X POST "https://api.telegram.org/bot${BOT_TOKEN}/setMyCommands" \
  -H "Content-Type: application/json" \
  -d "{\"commands\": $COMMANDS}")

# Check if successful
SUCCESS=$(echo "$RESPONSE" | jq -r '.ok')

if [ "$SUCCESS" = "true" ]; then
    echo "✅ Bot commands registered successfully!"
    echo ""
    echo "Available commands:"
    echo "$COMMANDS" | jq -r '.[] | "  /\(.command) - \(.description)"'
else
    ERROR=$(echo "$RESPONSE" | jq -r '.description')
    echo "❌ Error: $ERROR"
    exit 1
fi
