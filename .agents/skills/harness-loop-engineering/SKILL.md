---
name: harness-loop-engineering
description: 自律的閉ループ（Closed-Loop Engineering）実行スキル。タスク分解、実装、品質検証、自己修正、次ステップへの自動遷移を一気通貫で完遂する。
---

# Harness Loop Engineering

AIエージェントが人間からの指示待ちを減らし、目標達成まで自律的なサイクル（Loop）を回し続けるためのエンジニアリング手法。

## サイクル構成
1. **Plan & Decompose**: 要件を明確な検証可能ステップに分解。
2. **Execute (JIT)**: 必要最小限のコード編集・ファイル操作。
3. **Quality Gate Verification**: コンパイルエラー・ランタイム例外の機械的チェック。
4. **Self-Correction**: 失敗時はロールバックまたは差分修正。
5. **Memory Commit**: `HANDOFF.md` と Git への状態永続化。
6. **Next Iteration / Conclusion**: 全ステップ完了まで自律継続。
