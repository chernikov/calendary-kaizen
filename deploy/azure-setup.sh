#!/bin/bash

# Azure Infrastructure Setup Script for Calendary Kaizen
# Цей скрипт створює всі необхідні ресурси в Azure

set -e  # Зупинити виконання при помилці

# ======================
# CONFIGURATION
# ======================

# Основні параметри (змініть на свої)
SUBSCRIPTION_ID="1741b591-3afd-461f-bca2-75c63bb75ddd"
RESOURCE_GROUP="calendary-kaizen-rg"
LOCATION="westeurope"
ENVIRONMENT="prod"

# Назви ресурсів
STORAGE_ACCOUNT_NAME="calendarykaizen${ENVIRONMENT}"
FUNCTION_APP_NAME="calendary-kaizen-${ENVIRONMENT}"
APP_SERVICE_PLAN="calendary-kaizen-plan-${ENVIRONMENT}"
KEY_VAULT_NAME="calendary-kaizen-kv-${ENVIRONMENT}"
APP_INSIGHTS_NAME="calendary-kaizen-insights-${ENVIRONMENT}"

# Tags для ресурсів
TAGS="Project=CalendaryKaizen Environment=${ENVIRONMENT}"

echo "=========================================="
echo "Calendary Kaizen - Azure Infrastructure Setup"
echo "=========================================="
echo "Subscription: ${SUBSCRIPTION_ID}"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "Location: ${LOCATION}"
echo "Environment: ${ENVIRONMENT}"
echo "=========================================="

# ======================
# LOGIN & SUBSCRIPTION
# ======================

echo ""
echo "Step 1: Login to Azure..."
az login

echo ""
echo "Step 2: Set subscription..."
az account set --subscription "${SUBSCRIPTION_ID}"

# Перевірка поточної підписки
CURRENT_SUB=$(az account show --query name -o tsv)
echo "Current subscription: ${CURRENT_SUB}"

# ======================
# RESOURCE GROUP
# ======================

echo ""
echo "Step 3: Creating Resource Group..."
az group create \
  --name "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --tags ${TAGS}

echo "✓ Resource Group created: ${RESOURCE_GROUP}"

# ======================
# STORAGE ACCOUNT
# ======================

echo ""
echo "Step 4: Creating Storage Account..."
az storage account create \
  --name "${STORAGE_ACCOUNT_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --location "${LOCATION}" \
  --sku Standard_LRS \
  --kind StorageV2 \
  --tags ${TAGS}

echo "✓ Storage Account created: ${STORAGE_ACCOUNT_NAME}"

# Отримати connection string
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
  --name "${STORAGE_ACCOUNT_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query connectionString -o tsv)

echo "✓ Storage Connection String retrieved"

# ======================
# STORAGE CONTAINERS & TABLES
# ======================

echo ""
echo "Step 5: Creating Storage Containers and Tables..."

# Створити Blob Container - єдиний контейнер з структурою chatId/upload, chatId/generated
az storage container create \
  --name "data" \
  --connection-string "${STORAGE_CONNECTION_STRING}" \
  --public-access off

echo "✓ Blob Container created: data"
echo "  Structure: {chatId}/upload/, {chatId}/generated/, {chatId}/index.md"

# Створити Tables
az storage table create \
  --name "Trainings" \
  --connection-string "${STORAGE_CONNECTION_STRING}"

az storage table create \
  --name "Generations" \
  --connection-string "${STORAGE_CONNECTION_STRING}"

echo "✓ Tables created: Trainings, Generations"

# Створити Queue
az storage queue create \
  --name "telegram-notifications" \
  --connection-string "${STORAGE_CONNECTION_STRING}"

echo "✓ Queue created: telegram-notifications"

# ======================
# KEY VAULT
# ======================

echo ""
echo "Step 6: Creating Key Vault..."

# Create Key Vault only if it doesn't already exist
if az keyvault show --name "${KEY_VAULT_NAME}" --resource-group "${RESOURCE_GROUP}" >/dev/null 2>&1; then
  echo "Key Vault already exists: ${KEY_VAULT_NAME}"
else
  az keyvault create \
    --name "${KEY_VAULT_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --enable-rbac-authorization false \
    --tags ${TAGS}

  echo "✓ Key Vault created: ${KEY_VAULT_NAME}"
fi

# ======================
# APPLICATION INSIGHTS
# ======================

echo ""
echo "Step 7: Creating Application Insights..."

az monitor app-insights component create \
  --app "${APP_INSIGHTS_NAME}" \
  --location "${LOCATION}" \
  --resource-group "${RESOURCE_GROUP}" \
  --tags ${TAGS}

# Отримати Instrumentation Key
APPINSIGHTS_INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app "${APP_INSIGHTS_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query instrumentationKey -o tsv)

echo "✓ Application Insights created: ${APP_INSIGHTS_NAME}"

# ======================
# FUNCTION APP (CONSUMPTION PLAN)
# ======================

echo ""
echo "Step 8: Creating Function App (Consumption)..."

az functionapp create \
  --name "${FUNCTION_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --consumption-plan-location "${LOCATION}" \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --storage-account "${STORAGE_ACCOUNT_NAME}" \
  --os-type Linux \
  --functions-version 4 \
  --tags ${TAGS}

echo "✓ Function App created: ${FUNCTION_APP_NAME}"

# ======================
# CONFIGURE FUNCTION APP
# ======================

echo ""
echo "Step 10: Configuring Function App Settings..."

# Отримати URL Function App для webhook
FUNCTION_APP_URL="https://${FUNCTION_APP_NAME}.azurewebsites.net"

az functionapp config appsettings set \
  --name "${FUNCTION_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --settings \
    "AzureWebJobsStorage=${STORAGE_CONNECTION_STRING}" \
    "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
    "APPINSIGHTS_INSTRUMENTATIONKEY=${APPINSIGHTS_INSTRUMENTATION_KEY}" \
    "ReplicateApiKey=@Microsoft.KeyVault(SecretUri=https://${KEY_VAULT_NAME}.vault.azure.net/secrets/ReplicateApiKey)" \
    "ReplicateOwner=chernikov" \
    "ReplicateTrainerModel=replicate/fast-flux-trainer" \
    "ReplicateTrainerVersion=f463fbfc97389e10a2f443a8a84b6953b1058eafbf0c9af4d84457ff07cb04db" \
    "WebhookUrl=${FUNCTION_APP_URL}/api/ReplicateWebHook" \
    "TelegramBotToken=@Microsoft.KeyVault(SecretUri=https://${KEY_VAULT_NAME}.vault.azure.net/secrets/TelegramBotToken)" \
    "OpenAIApiKey=@Microsoft.KeyVault(SecretUri=https://${KEY_VAULT_NAME}.vault.azure.net/secrets/OpenAIApiKey)" \
    "OpenAIModel=gpt-5-mini"

echo "✓ Function App settings configured"

# ======================
# KEY VAULT - Enable Managed Identity
# ======================

echo ""
echo "Step 11: Enabling Managed Identity for Function App..."

az functionapp identity assign \
  --name "${FUNCTION_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}"

# Отримати Principal ID (для інформації)
FUNCTION_APP_PRINCIPAL_ID=$(az functionapp identity show \
  --name "${FUNCTION_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query principalId -o tsv)

echo "✓ Managed Identity enabled. Principal ID: ${FUNCTION_APP_PRINCIPAL_ID}"

echo "⚠️  NOTE: Grant this identity access to Key Vault manually if needed."

# ======================
# STORE SECRETS IN KEY VAULT
# ======================

echo ""
echo "Step 12: Storing secrets in Key Vault..."
echo ""
echo "⚠️  IMPORTANT: You need to manually set these secrets:"
echo ""
echo "1. Replicate API Key:"
echo "   az keyvault secret set --vault-name \"${KEY_VAULT_NAME}\" --name \"ReplicateApiKey\" --value \"your-replicate-api-key\""
echo ""
echo "2. Telegram Bot Token:"
echo "   az keyvault secret set --vault-name \"${KEY_VAULT_NAME}\" --name \"TelegramBotToken\" --value \"your-telegram-bot-token\""
echo ""
echo "3. OpenAI API Key:"
echo "   az keyvault secret set --vault-name \"${KEY_VAULT_NAME}\" --name \"OpenAIApiKey\" --value \"your-openai-api-key\""
echo ""

# ======================
# OUTPUT INFORMATION
# ======================

echo ""
echo "=========================================="
echo "✓ DEPLOYMENT COMPLETED SUCCESSFULLY!"
echo "=========================================="
echo ""
echo "Resource Group: ${RESOURCE_GROUP}"
echo "Location: ${LOCATION}"
echo ""
echo "Created Resources:"
echo "  - Storage Account: ${STORAGE_ACCOUNT_NAME}"
echo "  - Function App: ${FUNCTION_APP_NAME}"
echo "  - Key Vault: ${KEY_VAULT_NAME}"
echo "  - App Insights: ${APP_INSIGHTS_NAME}"
echo ""
echo "Function App URL: ${FUNCTION_APP_URL}"
echo "WebHook URL: ${FUNCTION_APP_URL}/api/ReplicateWebHook"
echo ""
echo "Key Vault URL: https://${KEY_VAULT_NAME}.vault.azure.net/"
echo ""
echo "=========================================="
echo "NEXT STEPS:"
echo "=========================================="
echo ""
echo "1. Set secrets in Key Vault (see commands above)"
echo ""
echo "2. Deploy your Function App code:"
echo "   cd /home/user/calendary-kaizen"
echo "   func azure functionapp publish ${FUNCTION_APP_NAME}"
echo ""
echo "3. Configure N8N workflows (see docs/n8n-setup.md)"
echo ""
echo "4. Test your deployment:"
echo "   curl -X POST ${FUNCTION_APP_URL}/api/UploadImages -H \"Content-Type: application/json\" -d '{...}'"
echo ""
echo "=========================================="

# Зберегти конфігурацію у файл
cat > azure-config.env << EOF
SUBSCRIPTION_ID=${SUBSCRIPTION_ID}
RESOURCE_GROUP=${RESOURCE_GROUP}
LOCATION=${LOCATION}
STORAGE_ACCOUNT_NAME=${STORAGE_ACCOUNT_NAME}
FUNCTION_APP_NAME=${FUNCTION_APP_NAME}
KEY_VAULT_NAME=${KEY_VAULT_NAME}
APP_INSIGHTS_NAME=${APP_INSIGHTS_NAME}
FUNCTION_APP_URL=${FUNCTION_APP_URL}
WEBHOOK_URL=${FUNCTION_APP_URL}/api/ReplicateWebHook
STORAGE_CONNECTION_STRING=${STORAGE_CONNECTION_STRING}
EOF

echo "✓ Configuration saved to: azure-config.env"
echo ""
