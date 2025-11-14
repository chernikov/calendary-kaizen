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
# Determine repo root (one level above deploy/) and cd there
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
echo "Repo root: ${REPO_ROOT}"
cd "${REPO_ROOT}"
# ensure a local NuGet package folder so dotnet doesn't look for Visual Studio fallback paths
export NUGET_PACKAGES="${REPO_ROOT}/.nuget/packages"
mkdir -p "${NUGET_PACKAGES}"
# Also set a local fallback packages folder to avoid dotnet looking for Visual Studio global folders on Windows
export NUGET_FALLBACK_PACKAGES="${REPO_ROOT}/.nuget/fallback"
mkdir -p "${NUGET_FALLBACK_PACKAGES}"

DOTNET_PROJECT="${REPO_ROOT}/CalendaryKaizen.csproj"
echo "Using project: ${DOTNET_PROJECT}"
dotnet clean "${DOTNET_PROJECT}"

echo ""
echo "Step 2: Restoring packages..."
dotnet restore "${DOTNET_PROJECT}"

echo ""
echo "Step 3: Building project..."
dotnet build "${DOTNET_PROJECT}" --configuration Release

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
