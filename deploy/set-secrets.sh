#!/bin/bash

# Set Secrets in Azure Key Vault
# Інтерактивний скрипт для налаштування секретів

set -e

# ======================
# CONFIGURATION
# ======================

# Завантажити конфігурацію
if [ -f "azure-config.env" ]; then
    source azure-config.env
    echo "Configuration loaded from azure-config.env"
else
    echo "Error: azure-config.env not found. Run azure-setup.sh first."
    exit 1
fi

echo "=========================================="
echo "Calendary Kaizen - Set Secrets"
echo "=========================================="
echo "Key Vault: ${KEY_VAULT_NAME}"
echo "=========================================="
echo ""

# ======================
# REPLICATE API KEY
# ======================

echo "1. Replicate API Key"
echo "   Get your API key from: https://replicate.com/account/api-tokens"
echo ""
read -sp "Enter Replicate API Key (r8_...): " REPLICATE_API_KEY
echo ""

if [ -n "$REPLICATE_API_KEY" ]; then
    az keyvault secret set \
      --vault-name "${KEY_VAULT_NAME}" \
      --name "ReplicateApiKey" \
      --value "${REPLICATE_API_KEY}"
    echo "✓ Replicate API Key saved"
else
    echo "⚠️  Skipped Replicate API Key"
fi

echo ""

# ======================
# TELEGRAM BOT TOKEN
# ======================

echo "2. Telegram Bot Token"
echo "   Create a bot via @BotFather on Telegram"
echo ""
read -sp "Enter Telegram Bot Token: " TELEGRAM_BOT_TOKEN
echo ""

if [ -n "$TELEGRAM_BOT_TOKEN" ]; then
    az keyvault secret set \
      --vault-name "${KEY_VAULT_NAME}" \
      --name "TelegramBotToken" \
      --value "${TELEGRAM_BOT_TOKEN}"
    echo "✓ Telegram Bot Token saved"
else
    echo "⚠️  Skipped Telegram Bot Token"
fi

echo ""

# ======================
# OPENAI API KEY
# ======================

echo "3. OpenAI API Key"
echo "   Get your API key from: https://platform.openai.com/api-keys"
echo ""
read -sp "Enter OpenAI API Key (sk-...): " OPENAI_API_KEY
echo ""

if [ -n "$OPENAI_API_KEY" ]; then
    az keyvault secret set \
      --vault-name "${KEY_VAULT_NAME}" \
      --name "OpenAIApiKey" \
      --value "${OPENAI_API_KEY}"
    echo "✓ OpenAI API Key saved"
else
    echo "⚠️  Skipped OpenAI API Key"
fi

echo ""
echo "=========================================="
echo "✓ SECRETS CONFIGURED!"
echo "=========================================="
echo ""
echo "Restart your Function App to apply changes:"
echo "  az functionapp restart --name ${FUNCTION_APP_NAME} --resource-group ${RESOURCE_GROUP}"
echo ""
