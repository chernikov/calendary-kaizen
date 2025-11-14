#!/bin/bash

# Azure Infrastructure Teardown Script
# Видаляє всі створені ресурси

set -e

# ======================
# CONFIGURATION
# ======================

# Завантажити конфігурацію
if [ -f "azure-config.env" ]; then
    source azure-config.env
    echo "Configuration loaded from azure-config.env"
else
    echo "Error: azure-config.env not found. Please provide configuration."
    echo "Usage: RESOURCE_GROUP=your-rg-name ./azure-teardown.sh"
    exit 1
fi

echo "=========================================="
echo "Calendary Kaizen - Azure Infrastructure Teardown"
echo "=========================================="
echo "⚠️  WARNING: This will DELETE all resources!"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "=========================================="
echo ""

read -p "Are you sure you want to delete ALL resources? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "Teardown cancelled."
    exit 0
fi

echo ""
echo "Starting teardown..."

# ======================
# DELETE RESOURCE GROUP
# ======================

echo ""
echo "Deleting Resource Group: ${RESOURCE_GROUP}"
echo "This will delete all contained resources..."

az group delete \
  --name "${RESOURCE_GROUP}" \
  --yes \
  --no-wait

echo ""
echo "✓ Deletion initiated. This may take several minutes."
echo ""
echo "To check deletion status:"
echo "  az group show --name ${RESOURCE_GROUP}"
echo ""
