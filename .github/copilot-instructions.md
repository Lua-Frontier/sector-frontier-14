# Copilot Instructions for Sector Frontier 14

## Project Overview
- **Sector Frontier 14** is a fork of Space Station 14/Frontier Station 14, running on the Robust Toolbox engine (C#).
- The codebase is modular: `Content.Server`, `Content.Client`, `Content.Shared`, and `RobustToolbox` (engine).
- Many subfolders (e.g., `_Lua`, `_NF`, `_Corvax`) represent code/assets from different forks. See `README.md` and the table there for origins and licenses.

## Build & Test Workflow
- **Initialize**: Run `RUN_THIS.py` (Python 3.5+) to set up submodules and dependencies.
- **Build**: Use `dotnet build` or the VS Code build task. Output goes to `bin/`.
- **Test**: Run `dotnet test Content.Tests/Content.Tests.csproj` for unit tests, or use the `test` task. Integration tests are in `Content.IntegrationTests`.
- **Map validation**: Use `.github/mapchecker/mapchecker.py` to check map changes against prototype rules.

## Code & Asset Marking Conventions
- **C# and YAML files** added after July 1, 2024 must include an AGPLv3 header (see `MARKERS.md`).
- **Modifications** to MIT-licensed files must be marked with a special comment block (see `MARKERS.md`).
- **Assets**: Most are CC-BY-SA 3.0; some are CC-BY-NC-SA 3.0 or custom. Check asset metadata for details.

## Key Patterns & Structure
- **Dependency Injection**: Systems use `[Dependency]` attributes for service injection.
- **Event-driven**: Game logic is often handled via event subscriptions (e.g., `SubscribeLocalEvent`).
- **Prototypes**: Game data is defined in YAML under `Resources/Prototypes`.
- **MapChecker**: Custom Python tool for validating map edits against forbidden prototypes.

## Integration & External Dependencies
- **RobustToolbox**: Engine code is in `RobustToolbox/` (do not modify unless necessary).
- **Discord.Net**: Used for Discord integration in `Content.Server`.
- **NUnit**: Used for tests in `Content.Tests` and `Content.IntegrationTests`.

## Special Notes
- **Forked Content**: When adding or modifying code/assets from other forks, follow the attribution and marking rules in `README.md` and `MARKERS.md`.
- **Licensing**: New content is AGPLv3 unless otherwise noted. See `LICENSE-AGPLv3.txt` and `LICENSE-MIT.txt`.
- **Documentation**: See the main `README.md` and linked wikis for more details.

---
For more, see: `README.md`, `MARKERS.md`, `.github/mapchecker/README.md`, and the project wiki links.
