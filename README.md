# AlienCyborgModernTempleOS 🛸🤖
**An automated multi-agent orchestration system to look for alien signals in YouTube recommendation algorithms (pattern analysis).**

This is a local, offline-friendly ASP.NET Core Razor Pages app that:
1) Accepts a ZIP of saved YouTube Home HTML snapshots (ex: `SingleFile.html` saves).  
2) Extracts video “tiles” (title + thumbnail URL when available).  
3) Runs a **multi-agent LLM pipeline** (Signal → Interpreter → Skeptic → Synth).  
4) Writes outputs to disk and displays them in a clean **Activity** panel.

---

## Features
- ✅ Upload ZIP → safe extraction (blocks zip path traversal)
- ✅ Parse tiles from:
  - `ytInitialData` (when present), and/or
  - DOM parsing fallback (SingleFile-friendly)
- ✅ Multi-agent orchestration:
  - **Signal**: clusters + anomalies (JSON output)
  - **Interpreter**: roleplay hypothesis grounded in evidence
  - **Skeptic**: normal explanations + bias checks
  - **Synth**: final combined report with confidence + next experiments
- ✅ Activity endpoint that auto-refreshes and shows:
  - Final report
  - Signal JSON
  - Interpreter output
  - Skeptic output
- ✅ Results persisted per job under `App_Data/jobs/<jobId>/`

---

## Project layout (high-level)
- `Pages/Index.cshtml` – Upload UI + Activity panel (auto-refresh)
- `Pages/Index.cshtml.cs` – Upload handler, ZIP extraction, parsing, orchestration, file writes
- `AlienOrchestrator.cs` – Multi-agent pipeline + proof-text capping
- `LmStudioChatClient.cs` – OpenAI-compatible chat client for LM Studio
- `LlmAgent.cs`, `IAgent.cs` – Agent wrapper(s)
- `AgentPrompts.cs` – Prompt “system” roles for each agent
- `YouTubeSnapshotParser.cs` – HTML parsing → `VideoTile` extraction

---

## Requirements
### 1) .NET
- **.NET SDK 8 or newer** (recommended: latest LTS)

### 2) LM Studio (local LLM server)
You need LM Studio running a local OpenAI-compatible server:
- Install LM Studio
- Download/load a chat model
- Start the **Local Server** (OpenAI-compatible)

This project expects the server at:
- `http://localhost:1234/v1/chat/completions`

> If you change the port or base URL, update your app configuration accordingly.

### 3) (Optional) AngleSharp
If you are using DOM fallback parsing, install:
- `AngleSharp` NuGet package

---

## Configure the app
You can configure LM Studio settings using `appsettings.json` (recommended) or hard-code in `LmStudioChatClient`.

Example `appsettings.json` snippet:
```json
{
  "LmStudio": {
    "BaseUrl": "http://localhost:1234/v1/",
    "Model": "your-model-name-here"
  }
}
```

Then bind in `Program.cs` (or inject via options). If you already inject `LmStudioChatClient` and `AlienOrchestrator`, you’re good.

---

## Run it
### 1) Start LM Studio server
- Load your model
- Start the local server
- Confirm it answers at `http://localhost:1234/v1/chat/completions`

### 2) Run the web app
```bash
dotnet restore
dotnet run
```

Open the printed URL (usually `https://localhost:xxxx`).

---

## How to use
1) Create YouTube Home snapshots:
   - Open YouTube Home (signed in if you want personalization)
   - Save each page as a **SingleFile** HTML (or similar)
   - Do this multiple times (ex: `yt_home_01.html` … `yt_home_20.html`)

2) ZIP the HTML files together.

3) Upload the ZIP in the app.

4) Watch the **Activity** panel:
   - It will show state changes (extracted → running_agents → completed)
   - Expand accordions for Signal / Interpreter / Skeptic / Final Report

---

## Where results go
Each upload creates a **job folder**:
```
App_Data/jobs/<jobId>/
  snapshot.zip
  extracted/
    (your html files)
  status.json
  results.json
```

- `status.json` contains state + counts
- `results.json` contains the serialized `JobResult` (agent outputs)

---

## Troubleshooting
### “400 Bad Request” + context overflow
If your ZIP is large, your input can exceed the model’s context length.

Fix options:
- Reduce the number of tiles/pages you feed into the proof text
- Cap the proof text length (recommended)
- Use a model with a larger context window
- Increase context settings in LM Studio (if supported by your model/runtime)

### “found 0 tiles” / “Could not locate ytInitialData”
This can happen if:
- Your snapshot doesn’t include the `ytInitialData` payload
- The page is saved in a way that strips scripts/data

Fix options:
- Enable the DOM fallback parsing path (AngleSharp)
- Ensure you saved a fully-loaded YouTube Home page
- Try SingleFile with “include scripts” enabled (if available)

### Output is in Chinese / includes `<think>`
Some models ignore prompt instructions.
Fix options:
- Switch models
- Add stronger “English-only / no reasoning tags” constraints
- Add a post-filter step to strip `<think>…</think>` blocks

---

## Credits
- Built with ASP.NET Core Razor Pages
- Uses an OpenAI-compatible local server (LM Studio)
- Optional DOM parsing via AngleSharp
