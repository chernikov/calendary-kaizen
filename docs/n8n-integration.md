# N8N Workflow Integration Guide

## Telegram Bot Switch Logic

N8N workflow для обробки різних типів повідомлень від Telegram бота.

### Workflow Structure

```
Telegram Trigger
    ↓
Switch Node (Message Type)
    ├─ Photo/File → Upload Images
    ├─ Command /domodel → Start Training
    ├─ Command /status → Check Status
    └─ Text Message → Generate Image
```

## 1. Photo/File Upload Handler

**Trigger:** User sends photo(s) to the bot

**Action:** Call `UploadImages` function

**Request:**
```json
{
  "userId": "{{ $json.message.chat.id }}",
  "imageUrls": [
    "{{ $json.message.photo[0].file_id }}"
  ]
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "imageCount": 5,
    "uploadedImages": [
      {
        "fileName": "image_20251114_120000_abc123_001.jpg",
        "sizeBytes": 1048576,
        "url": "https://storage.blob.core.windows.net/..."
      }
    ]
  }
}
```

**N8N Response to User:**
```
✅ Uploaded {{ $json.data.imageCount }} images!

Files:
{% for image in $json.data.uploadedImages %}
- {{ image.fileName }} ({{ (image.sizeBytes / 1024 / 1024).toFixed(2) }} MB)
{% endfor %}

Total images: {{ $json.data.imageCount }}

Ready to train? Use /domodel [trigger_word] [steps]
```

---

## 2. /domodel Command Handler

**Trigger:** User sends `/domodel [trigger_word] [steps]`

**Parse Command:**
```javascript
// In Function node
const text = $input.item.json.message.text;
const parts = text.split(' ');
const triggerWord = parts[1] || 'TOK';
const steps = parseInt(parts[2]) || 1000;

return {
  chatId: $input.item.json.message.chat.id,
  triggerWord: triggerWord,
  steps: steps
};
```

**Action:** Call `CreateAndTrainModel` function

**Request:**
```json
{
  "userId": "{{ $json.chatId }}",
  "modelDescription": "User model for {{ $json.chatId }}",
  "triggerWord": "{{ $json.triggerWord }}",
  "steps": {{ $json.steps }}
}
```

**Response:**
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

**N8N Response to User:**
```
🚀 Model training started!

Training ID: {{ $json.data.trainingId }}
Model: {{ $json.data.modelId }}
Status: {{ $json.data.status }}
Trigger word: {{ $json.triggerWord }}
Steps: {{ $json.steps }}

You'll receive a notification when training is complete.
Use /status {{ $json.data.trainingId }} to check progress.
```

---

## 3. /status Command Handler

**Trigger:** User sends `/status [training_id]`

**Parse Command:**
```javascript
// In Function node
const text = $input.item.json.message.text;
const parts = text.split(' ');
const trainingId = parts[1] || '';

return {
  chatId: $input.item.json.message.chat.id,
  trainingId: trainingId
};
```

**Action:** Call `GetTrainingStatus` function

**Request:**
```json
{
  "userId": "{{ $json.chatId }}",
  "trainingId": "{{ $json.trainingId }}"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "trainingId": "abc123xyz",
    "modelId": "chernikov/telegram_flux_123456789_456",
    "status": "succeeded",
    "createdAt": "2025-11-14T10:00:00Z",
    "completedAt": "2025-11-14T11:30:00Z",
    "version": "v1234567890"
  }
}
```

**N8N Response to User (In Progress):**
```
⏳ Training in progress...

Training ID: {{ $json.data.trainingId }}
Model: {{ $json.data.modelId }}
Status: {{ $json.data.status }}
Started: {{ new Date($json.data.createdAt).toLocaleString() }}

Please wait, training typically takes 20-40 minutes.
```

**N8N Response to User (Completed):**
```
✅ Training completed!

Training ID: {{ $json.data.trainingId }}
Model: {{ $json.data.modelId }}
Version: {{ $json.data.version }}
Completed: {{ new Date($json.data.completedAt).toLocaleString() }}

You can now generate images!
Send me a text prompt to create an image.
```

---

## 4. Text Message Handler (Generate Image)

**Trigger:** User sends text message (not a command)

**Action:** Call `GenerateImage` function

**First, get the latest training:**
```json
{
  "userId": "{{ $json.message.chat.id }}",
  "trainingId": ""
}
```

**Then generate image:**
```json
{
  "userId": "{{ $json.message.chat.id }}",
  "trainingId": "{{ $json.latestTrainingId }}",
  "prompt": "{{ $json.message.text }}",
  "aspectRatio": "1:1",
  "numInferenceSteps": 28
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "generationId": "gen-abc123",
    "status": "succeeded"
  }
}
```

**N8N Response to User:**
```
🎨 Image generated!

Prompt: {{ $json.message.text }}
Generation ID: {{ $json.data.generationId }}

[Send the generated image here]
```

---

## N8N Switch Node Configuration

```javascript
// Switch Expression
{{ $json.message.photo ? 'photo' : 
   $json.message.document ? 'file' :
   $json.message.text.startsWith('/domodel') ? 'domodel' :
   $json.message.text.startsWith('/status') ? 'status' :
   $json.message.text.startsWith('/') ? 'command' :
   'text' }}
```

### Routing Rules:
- `photo` → Upload Images Handler
- `file` → Upload Images Handler  
- `domodel` → Start Training Handler
- `status` → Check Status Handler
- `text` → Generate Image Handler
- `command` → Help/Unknown Command Handler

---

## Azure Function Endpoints

All endpoints require `x-functions-key` header.

Base URL: `https://calendary-kaizen-prod.azurewebsites.net/api`

1. **UploadImages**
   - `POST /UploadImages`
   - Returns: File list with sizes

2. **CreateAndTrainModel**  
   - `POST /CreateAndTrainModel`
   - Returns: Training ID and status

3. **GetTrainingStatus**
   - `POST /GetTrainingStatus`
   - Returns: Current training status

4. **GenerateImage**
   - `POST /GenerateImage`
   - Returns: Generation ID

5. **ReplicateWebHook**
   - `POST /ReplicateWebHook`
   - Handles Replicate callbacks

---

## Error Handling

All responses follow this format:

**Success:**
```json
{
  "success": true,
  "data": { ... }
}
```

**Error:**
```json
{
  "success": false,
  "error": "Error message"
}
```

**N8N Error Response:**
```
❌ Error: {{ $json.error }}

Please try again or contact support.
```

---

## Environment Variables in N8N

Set these in N8N credentials:

- `AZURE_FUNCTION_URL`: `https://calendary-kaizen-prod.azurewebsites.net`
- `AZURE_FUNCTION_KEY`: Your function key
- `TELEGRAM_BOT_TOKEN`: Your Telegram bot token

---

## Complete N8N Workflow JSON

See `n8n-workflow.json` for the complete workflow configuration.
