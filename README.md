# Calendary Kaizen - FLUX Model Training & Generation Service

Azure Functions сервіс для тренування FLUX моделей через Replicate API та генерації зображень з інтеграцією з Telegram через N8N.

## Архітектура

```
Telegram (User)
    ↓
N8N Workflows
    ↓
Azure Functions (HTTP Triggers)
    ↓
Replicate API (FLUX Training/Generation)
    ↓
Azure Storage (Blob, Table, Queue)
    ↓
N8N → Telegram (Notifications)
```

## Структура даних в Azure Blob Storage

Всі дані зберігаються в контейнері `data` з такою структурою:

```
data/
├── {chatId}/
│   ├── upload/
│   │   ├── image_001.jpg
│   │   ├── image_002.jpg
│   │   └── ...
│   ├── generated/
│   │   ├── {generationId}.jpg
│   │   ├── {generationId}_prompt.txt
│   │   └── ...
│   ├── archive_{timestamp}.zip  # Архів для тренування
│   └── index.md                 # Метадані чату
```

### Приклад index.md

```markdown
# Chat 123456789

## Uploaded Images

- Date: 2025-01-14 10:30:00 UTC
- Count: 15

## Training

- Training ID: abc123xyz
- Model ID: chernikov/telegram_flux_123456789_456
- Status: succeeded
- Archive: https://storage.blob.core.windows.net/data/123456789/archive.zip
- Trigger Word: TOK
- Steps: 1000
- Started: 2025-01-14 10:35:00 UTC

### Generation abc-123

- Date: 2025-01-14 11:00:00 UTC
- Prompt: a photo of TOK person at the beach
- Seed: 12345
- Image: https://storage.blob.core.windows.net/data/123456789/generated/abc-123.jpg

### Generation def-456

- Date: 2025-01-14 11:05:00 UTC
- Prompt: TOK person wearing a suit
- Seed: 67890
- Image: https://storage.blob.core.windows.net/data/123456789/generated/def-456.jpg
```

## Azure Functions

### 1. UploadImages

**Endpoint:** `POST /api/UploadImages`

**Призначення:** Завантажує зображення з Telegram і зберігає в `{chatId}/upload/`

**Запит:**
```json
{
  "userId": "123456789",
  "imageUrls": [
    "https://telegram.org/file/image1.jpg",
    "https://telegram.org/file/image2.jpg"
  ]
}
```

**Відповідь:**
```json
{
  "success": true,
  "data": {
    "archiveUrl": "",
    "imageCount": 2
  }
}
```

### 2. CreateAndTrainModel

**Endpoint:** `POST /api/CreateAndTrainModel`

**Призначення:** Створює архів з `{chatId}/upload/`, створює модель в Replicate і запускає тренування

**Запит:**
```json
{
  "userId": "123456789",
  "modelDescription": "User model",
  "triggerWord": "TOK",
  "steps": 1000
}
```

**Відповідь:**
```json
{
  "success": true,
  "data": {
    "trainingId": "abc123xyz",
    "modelId": "chernikov/telegram_flux_123456789_456",
    "status": "starting"
  }
}
```

### 3. GenerateImage

**Endpoint:** `POST /api/GenerateImage`

**Призначення:** Генерує зображення і зберігає в `{chatId}/generated/`

**Запит:**
```json
{
  "userId": "123456789",
  "trainingId": "abc123xyz",
  "prompt": "a photo of TOK person at the beach",
  "seed": 12345,
  "aspectRatio": "1:1",
  "numInferenceSteps": 28
}
```

**Відповідь:**
```json
{
  "success": true,
  "data": {
    "generationId": "def-456",
    "status": "succeeded"
  }
}
```

### 4. ReplicateWebHook

**Endpoint:** `POST /api/ReplicateWebHook`

**Призначення:** Обробляє WebHook від Replicate після завершення тренування

## Azure Tables

### Trainings

| PartitionKey | RowKey      | Поля                                                      |
|--------------|-------------|-----------------------------------------------------------|
| chatId       | trainingId  | ReplicateId, ModelId, Version, Status, ArchiveUrl, ...   |

### Generations

| PartitionKey | RowKey       | Поля                                                     |
|--------------|--------------|----------------------------------------------------------|
| chatId       | generationId | TrainingId, Prompt, Seed, ImageUrl, Status, ...          |

## Azure Queue

### telegram-notifications

Повідомлення для відправки в Telegram через N8N:

```json
{
  "userId": "123456789",
  "text": "Тренування завершено!",
  "imageUrl": "https://...",
  "messageType": "training_complete",
  "metadata": {}
}
```

## Розгортання

### Швидкий старт

```bash
# 1. Клонувати репозиторій
git clone https://github.com/chernikov/calendary-kaizen.git
cd calendary-kaizen

# 2. Налаштувати параметри в deploy/azure-setup.sh
nano deploy/azure-setup.sh

# 3. Запустити розгортання інфраструктури
cd deploy
./azure-setup.sh

# 4. Налаштувати секрети
./set-secrets.sh

# 5. Розгорнути код
./azure-deploy-code.sh
```

Детальніше: [docs/deployment-guide.md](docs/deployment-guide.md)

## Налаштування N8N

Див. [docs/n8n-setup.md](docs/n8n-setup.md) для налаштування workflows:

1. Завантаження зображень через Telegram
2. Запуск тренування
3. Генерація зображень
4. Обробка повідомлень з Azure Queue

## Технології

- **.NET 8** - Azure Functions Worker (isolated)
- **Azure Functions** - Serverless HTTP triggers
- **Azure Storage** - Blob, Table, Queue
- **Azure Key Vault** - Управління секретами
- **Replicate API** - FLUX model training & generation
- **Telegram Bot API** - Користувацький інтерфейс
- **N8N** - Workflow automation

## Структура проєкту

```
calendary-kaizen/
├── Functions/
│   ├── UploadImagesFunction.cs
│   ├── CreateAndTrainModelFunction.cs
│   ├── GenerateImageFunction.cs
│   └── ReplicateWebHookFunction.cs
├── Services/
│   ├── IReplicateService.cs
│   ├── ReplicateService.cs
│   ├── IStorageService.cs
│   └── StorageService.cs
├── Models/
│   ├── ReplicateModels.cs
│   ├── StorageEntities.cs
│   └── FunctionRequests.cs
├── deploy/
│   ├── azure-setup.sh
│   ├── azure-teardown.sh
│   ├── azure-deploy-code.sh
│   └── set-secrets.sh
├── docs/
│   ├── process.md
│   ├── n8n-setup.md
│   ├── deployment-guide.md
│   └── todo.md
├── Program.cs
├── host.json
├── local.settings.json
└── CalendaryKaizen.csproj
```

## Розробка локально

### Передумови

- .NET 8 SDK
- Azure Functions Core Tools v4
- Azure Storage Emulator або Azurite

### Запуск

```bash
# Встановити залежності
dotnet restore

# Запустити локально
func start
```

### Налаштування local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ReplicateApiKey": "r8_xxx...",
    "ReplicateOwner": "chernikov",
    "ReplicateTrainerModel": "replicate/fast-flux-trainer",
    "ReplicateTrainerVersion": "f463fbfc97389e10a2f443a8a84b6953b1058eafbf0c9af4d84457ff07cb04db",
    "WebhookUrl": "https://yourapp.azurewebsites.net/api/ReplicateWebHook",
    "TelegramBotToken": ""
  }
}
```

## Приклади використання

### 1. Завантаження зображень

```bash
curl -X POST https://calendary-kaizen-prod.azurewebsites.net/api/UploadImages \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_KEY" \
  -d '{
    "userId": "123456789",
    "imageUrls": [
      "https://example.com/image1.jpg",
      "https://example.com/image2.jpg"
    ]
  }'
```

### 2. Тренування моделі

```bash
curl -X POST https://calendary-kaizen-prod.azurewebsites.net/api/CreateAndTrainModel \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_KEY" \
  -d '{
    "userId": "123456789",
    "modelDescription": "My model",
    "triggerWord": "TOK",
    "steps": 1000
  }'
```

### 3. Генерація зображення

```bash
curl -X POST https://calendary-kaizen-prod.azurewebsites.net/api/GenerateImage \
  -H "Content-Type: application/json" \
  -H "x-functions-key: YOUR_KEY" \
  -d '{
    "userId": "123456789",
    "trainingId": "abc123xyz",
    "prompt": "a photo of TOK person at the beach",
    "aspectRatio": "1:1"
  }'
```

## Моніторинг

### Application Insights

Всі логи доступні в Application Insights:

```bash
az monitor app-insights query \
  --app calendary-kaizen-insights-prod \
  --analytics-query "traces | where timestamp > ago(1h)"
```

### Перегляд Blob Storage

```bash
az storage blob list \
  --container-name data \
  --account-name calendarykaizenprod \
  --prefix "123456789/"
```

## Troubleshooting

### Проблема: Зображення не завантажуються

Перевірте:
- URLs доступні і повертають images
- Azure Storage connection string правильний
- Blob container `data` існує

### Проблема: Тренування не запускається

Перевірте:
- Replicate API Key в Key Vault
- Папка `{chatId}/upload/` містить мінімум 5 зображень
- WebHook URL доступний

### Проблема: Генерація fail

Перевірте:
- Training успішно завершено (status = "succeeded")
- Model version існує
- Prompt містить trigger word "TOK"

## Ліцензія

MIT

## Автор

Chernikov (chernikov@calendary.com.ua)

## Посилання

- [Replicate API Docs](https://replicate.com/docs)
- [Azure Functions Docs](https://docs.microsoft.com/azure/azure-functions/)
- [FLUX Model](https://replicate.com/black-forest-labs/flux-dev)
- [N8N Docs](https://docs.n8n.io/)
