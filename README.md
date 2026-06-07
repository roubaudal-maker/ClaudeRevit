# Claude Revit

Claude AI in Autodesk Revit 2027 — a dockable chat pane with **80 tools** that let Claude inspect and modify your model directly. Ask it to create walls, generate schedules, place families, dimension grids, draft sketches, and more.

---

## Features

- **Dockable chat pane** in Revit, with streaming responses
- **80 tools** spanning modeling, views, sheets, annotation, schedules, filters, families
- **Single-undo per prompt** — Ctrl+Z reverts everything Claude did in one turn
- **Selection awareness** — green pill shows what's selected; Claude knows what "this" means
- **Markdown rendering** + **selectable text** in messages
- **Clickable element IDs** — click any id in a tool result, Revit selects and zooms to that element
- **Cost telemetry** with prompt caching (1h TTL on system prompt + tools = ~5-7× cheaper for long sessions)
- **History persistence** — your conversation survives Revit restarts
- **Model picker** — Sonnet 4.6 / Opus 4.7 / Haiku 4.5
- **In-pane API key entry** — gear icon, no env-var dance

---

## Install (for users)

You need:

- **Autodesk Revit 2027**
- **Windows** (Revit is Windows-only)
- **Anthropic API key** — get one at [console.anthropic.com](https://console.anthropic.com/settings/keys) and add credits in **Billing**

Pick whichever install path you prefer:

### Option A — Installer .exe (easiest)

Download **`ClaudeRevit-Setup-vX.Y.exe`** from the [latest release](https://github.com/roubaudal-maker/ClaudeRevit/releases/latest), double-click, click Next → Install. Done.

> Windows SmartScreen may say "Windows protected your PC" the first time (the installer isn't code-signed yet). Click **More info → Run anyway**.

### Option B — PowerShell one-liner

Open PowerShell and run:

```powershell
iwr https://raw.githubusercontent.com/roubaudal-maker/ClaudeRevit/main/install.ps1 | iex
```

Either way: launch Revit, open any project, look for the **Claude** tab in the ribbon. Click **Chat** → the pane opens on the right. First time? Click the **⚙** icon in the pane and paste your API key.

To update later, re-run the installer or the one-liner — both pick up the latest release.

---

## Build from source (for developers)

You need:

- **Visual Studio 2026 Community** (or Rider, or VS Code with C# Dev Kit)
- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Autodesk Revit 2027** installed locally (only required for F5 debugging — compile works without it)

Steps:

1. Clone:
   ```powershell
   git clone https://github.com/roubaudal-maker/ClaudeRevit.git
   cd ClaudeRevit
   ```
2. Open `ClaudeRevit.sln` in Visual Studio.
3. Right-click the project → **Restore NuGet Packages**.
4. Set `ClaudeRevit` as the startup project.
5. Press **F5**.

The post-build target copies the DLL + addin manifest to `%AppData%\Autodesk\Revit\Addins\2027\` automatically. F5 launches Revit and attaches the debugger.

### How the build works

- Revit API references come from **Nice3point.Revit.Api.RevitAPI** and **Nice3point.Revit.Api.RevitAPIUI** NuGet packages — no local Revit install required to compile.
- The post-build `DeployToRevit` target copies to `%AppData%\Autodesk\Revit\Addins\2027\`. To skip the local deploy (e.g. on CI), pass `-p:SkipDeploy=true`.
- A separate `PackageRelease` target stages all release artifacts under `bin\Release\release\` — used by the GitHub Actions workflow.

---

## Releasing a new version

The `.github/workflows/release.yml` workflow builds and publishes a release whenever a `v*` tag is pushed:

```powershell
git tag v1.0
git push origin v1.0
```

GitHub Actions then:
1. Restores + builds in Release mode (with `SkipDeploy=true`)
2. Zips the release artifacts (`ClaudeRevit.dll`, `ClaudeRevit.addin`, `Anthropic.dll`, dependencies)
3. Creates a GitHub Release with the zip attached and auto-generated release notes

Users get the new version with the same `install.ps1` one-liner.

---

## Targeting a different Revit version

Change `<RevitVersion>2027</RevitVersion>` in `ClaudeRevit/ClaudeRevit.csproj` to your version (e.g. `2028`). The Nice3point packages use a wildcard (`$(RevitVersion).0.*`) so it'll pick up the matching API package automatically — as long as one exists for that Revit version.

The deploy target also follows the version, copying to `%AppData%\Autodesk\Revit\Addins\<version>\`.

---

## Tools

The plugin exposes **80 tools** to Claude across these categories:

- **Inspection** — get/list elements, parameters, levels, materials, phases, families, project info, warnings
- **Geometry creation** — walls, floors, roofs, rooms, levels, grids, doors, windows, columns, beams
- **Element ops** — move, rotate, copy, mirror, array, delete, set_parameter, pin/unpin
- **Views** — 3D, floor plan, ceiling plan, section, elevation, callouts, duplicate, set scale, apply template
- **Sheets** — create sheets, place views/schedules on sheets
- **Schedules** — real Revit ViewSchedules with field selection
- **Annotation** — dimensions, tags, text notes, detail lines, filled regions, reference planes
- **Visibility** — hide/isolate in view, color overrides, filters
- **Interactive** — `pick_point_in_view` for click-to-place workflows
- **Families** — load .rfa files, list loaded families, generic instance placement
- **Groups** — create/place model groups
- **Export & IO** — export view image, save document
- **Selection** — `select_similar` for "select all instances of this type"

See the [`ClaudeRevit/Tools/`](ClaudeRevit/Tools) folder for the full list — every `.cs` file there is one tool.

---

## Architecture

- **`App.cs`** — `IExternalApplication` entry point; registers tools, dockable pane, ribbon button
- **`Tools/`** — every `IRevitTool` class. Add a new file + register in `App.cs` → it's available to Claude.
- **`Tools/ToolDispatcher.cs`** — `IExternalEventHandler` that runs tool calls on Revit's API thread. Wraps each tool in a `Transaction`, wraps each turn in a `TransactionGroup` for one-undo-per-prompt.
- **`Services/AnthropicChatService.cs`** — the agentic loop. Uses `BetaMessageContentAggregator` for streaming text + tool dispatch. Sets cache control on the system prompt and tools list so they're cached for 1 hour (prompt caching).
- **`UI/ChatPaneView.xaml`** — WPF chat pane (RichTextBox bubbles, markdown rendering, clickable element-id links).
- **`Services/SelectionService.cs`** — subscribes to Revit's `Idling` event to track selection changes.

---

## License

MIT — do whatever you want with it.
