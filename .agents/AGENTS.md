# Antigravity

Read `/AGENTS.md` and `Assets/RustAndFloat/HANDOFF.md` first.

Work only on RustAndFloat (`Assets/RustAndFloat/`). Never edit `Assets/Scenes/AdventureWorld.unity` or `Land_Terrain.asset`.

## コアスキル（推進力・知能）

複雑タスクでは `.agents/skills/core-*` を適用する。索引: `.agents/skills/core-skills-index.md`

| 優先 | スキル |
|------|--------|
| 1 | `core-task-decomposition-replanning` |
| 2 | `core-adversarial-critique`（株・単一銘柄は必須） |
| 3 | `core-mcp-tool-orchestration` |
| 4 | `core-multimodal-gui-perception` |
| 5 | `core-agentic-rag-knowledge-graph` |
| 6 | `core-meta-cognition-skill-synthesis` |

## ドメインスキル

- `unity-app-architect` — Obsidian アイデアから Unity/iOS MVP（仕様・C#構成・Inspector手順・SE3実機チェック）

## ハーネススキル（安全境界）

`.agents/skills/harness-*` — 品質ゲート、ガードレール、ループ上限、メモリ。コアだけでは不十分。
