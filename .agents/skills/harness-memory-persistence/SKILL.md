---
name: harness-memory-persistence
description: セッション跨ぎの完全な状態永続化スキル。HANDOFF.md、Gitコミット履歴、コンパクション耐性を維持する。
---

# Harness Memory Persistence

会話のコンパクションやセッション中断が発生しても、作業進捗と決定事項を100%保持する永続化プロトコル。

## プロトコル
1. **Live Handoff Sync**: 各マイルストーン完了ごとに `Assets/RustAndFloat/HANDOFF.md` を最新状態に更新。
2. **Atomic Commits**: 各機能・案ごとに単一目的の明確なコミットメッセージを作成して即時プッシュ。
3. **Decisions Logging**: アーキテクチャ判断やパフォーマンス最適化の根拠をドキュメント化。
