#!/bin/bash

# Deploy Function App Code to Azure
# Цей скрипт компілює та публікує код до Azure Functions

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
echo "Calendary Kaizen - Deploy Function App Code"
echo "=========================================="
echo "Function App: ${FUNCTION_APP_NAME}"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "=========================================="

# ======================
# BUILD & PUBLISH
# ======================

echo ""
echo "Step 1: Cleaning previous builds..."
cd /home/user/calendary-kaizen
dotnet clean

echo ""
echo "Step 2: Restoring packages..."
dotnet restore

echo ""
echo "Step 3: Building project..."
dotnet build --configuration Release

echo ""
echo "Step 4: Publishing to Azure..."
func azure functionapp publish "${FUNCTION_APP_NAME}" --dotnet-isolated

echo ""
echo "=========================================="
echo "✓ DEPLOYMENT COMPLETED!"
echo "=========================================="
echo ""
echo "Function App URL: ${FUNCTION_APP_URL}"
echo ""
echo "Available endpoints:"
echo "  - POST ${FUNCTION_APP_URL}/api/UploadImages"
echo "  - POST ${FUNCTION_APP_URL}/api/CreateAndTrainModel"
echo "  - POST ${FUNCTION_APP_URL}/api/GenerateImage"
echo "  - POST ${FUNCTION_APP_URL}/api/ReplicateWebHook"
echo ""
echo "Test your deployment:"
echo "  curl ${FUNCTION_APP_URL}/api/UploadImages"
echo ""
