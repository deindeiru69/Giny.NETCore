# TODO — Config hardening

Tracks hardcoded paths / credentials that still need to be migrated to a runtime
configuration mechanism before they can be safely deleted from source.

---

## 2026-05-20 — `ClientConstants.ClientPath` (the "olivi" path)

Defined in `Sources/Giny.IO/ClientConstants.cs:35` as a `const string`:

```csharp
public const string ClientPath = "C:\\Users\\olivi\\Desktop\\Giny .NET Core\\Dofus";
```

`WorldEditor` no longer uses it (fixed in commit `c93288e7` —
`ExternalResources.Initialize()` now reads
`ConfigManager<WorldViewConfig>.Instance.ClientPath`).

**Remaining 10 references — 7 files across 3 sub-projects** :

| Project | File | Lines |
|---|---|---|
| `Sync/Giny.DatabaseSynchronizer` | `Program.cs` | 52 |
| `Sync/Giny.DatabaseSynchronizer` | `MapSynchronizer.cs` | 156, 159 |
| `Sync/Giny.DatabaseSynchronizer` | `D2OSynchronizer.cs` | 40 |
| `Sync/Giny.CustomEnumsBuilder` | `Program.cs` | 27 |
| `Tools/Giny.MapEditor` | `Textures/TextureManager.cs` | 46 |
| `Tools/Giny.MapEditor` | `Textures/TextureMapper.cs` | 59, 62 |
| `Tools/Giny.MapEditor` | `Maps/MapsManager.cs` | 31, 32 |

### Suggested approach per tool

- **DatabaseSynchronizer** / **CustomEnumsBuilder** : Console codegen, run rarely
  by a dev. Easiest is a CLI flag (`--client-path`) with optional fallback to an
  env var (e.g. `GINY_CLIENT_PATH`). No persistent config file needed.
- **MapEditor** : WPF desktop, no Configuration UI today. Two options:
  - Quick win — read same `WorldViewConfig` (`config.json`) as WorldEditor so a
    user who configured one tool is set for both. Requires
    `Giny.WorldEditor.Config` reference (or move `WorldViewConfig` to a shared
    project like `Giny.Core.IO.Configuration`).
  - Proper — its own `MapEditorConfig : IConfigFile` mirroring the WorldEditor
    pattern, with a Configuration dialog at startup.

Once all 10 usages are migrated, `ClientConstants.ClientPath` can be deleted
from `Sources/Giny.IO/ClientConstants.cs` (and the line marked `/// Debug only`
removed). Validate with a final `grep -r 'ClientConstants.ClientPath'`.
