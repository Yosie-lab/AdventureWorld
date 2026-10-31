---
name: harness-quality-gates
description: 厳格な品質ゲート検証スキル。Unityコンパイルエラー0件、コンソール例外0件、Play Mode状態管理を機械的に検証する。
---

# Harness Quality Gates

エージェントが生成した成果物の健全性を決定論的に保証する品質ゲート。

## 必須チェック項目
1. **Compilation Check**: `unityMCP:read_console` (types: error) でコンパイルエラー0件を確認。
2. **Runtime Exception Check**: ParticleSystemやNullReferenceなどの毎フレーム例外を未然に防止。
3. **Editor State Gate**: シーン保存・コミット前に `unityMCP:manage_editor` (action: stop) を実行。
4. **Git Cleanliness**: 想定外の巨大ファイルやメタデータの混入がないことを `git status` で確認。
