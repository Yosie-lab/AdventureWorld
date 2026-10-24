# Agent instructions

This repo is a Unity 6 (URP) adventure. The live original game is `Assets/Scenes/AdventureWorld.unity` — do not modify its terrain or scene.

Active work is **RustAndFloat** (folder `Assets/RustAndFloat/`). Read [[Assets/RustAndFloat/HANDOFF.md]] before changing anything.

- Language with the user: Japanese, conclusion first, one step at a time, one verification question.
- Stop Play Mode before saving scenes or prefabs.
- Do not commit secrets, the huge untracked `Assets/Art` dump, or Build Settings that replace AdventureWorld with Demo/Loader.

## Agent skills (Cursor & Antigravity)

- **Core (reasoning/tools)**: `.agents/skills/core-*` — see `core-skills-index.md`
- **Harness (safety)**: `.agents/skills/harness-*`
- **Unity/iOS MVP**: `.agents/skills/unity-app-architect/` — Obsidian idea → SE3 checklist
- Cursor personal mirror: user Agent Store `skills/core-*`
