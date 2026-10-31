---
name: harness-security-guardrails
description: プロジェクト不可侵領域の保護および安全ガードレールスキル。AdventureWorldやLand_Terrainの完全保護を遵守する。
---

# Harness Security Guardrails

プロジェクトの破壊や不正な変更を物理的・論理的に防止する防護壁。

## 不可侵ルール
1. **Protected Assets**:
   - `Assets/Scenes/AdventureWorld.unity`（元ゲーム本編・絶対不変）
   - `Land_Terrain.asset`（元地形データ・絶対不変）
2. **Commit Filtering**:
   - `Assets/Art` の巨大ダンプ、認証情報、Build Settingsの差し替えはコミット禁止。
3. **Execution Safety**:
   - シーン保存やPrefab保存前には必ず Play Mode を停止（`manage_editor: stop`）。
