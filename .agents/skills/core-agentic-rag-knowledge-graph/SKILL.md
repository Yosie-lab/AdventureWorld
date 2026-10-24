---
name: core-agentic-rag-knowledge-graph
description: >-
  Retrieves context from Obsidian notes, past trials, HANDOFF, agent transcripts, and
  the codebase before answering. Use when the user mentions Obsidian, 過去メモ, ノート,
  治験, 前回, 同じ失敗, RAG, ナレッジ, or tasks that may repeat prior mistakes.
---

# セマンティック検索・知識グラフ (Agentic RAG & Knowledge Graph)

## なぜ必要か

Obsidian や HANDOFF、過去チャットに既にある知見を使わないと **同じ失敗を繰り返す**。

## 検索対象（優先順）

1. **プロジェクト**: `RustAndFloat/Assets/RustAndFloat/HANDOFF.md`, `AGENTS.md`, `.agents/skills/`
2. **コード**: Grep / explore サブエージェント（シンボル・過去 fix のコミットメッセージ）
3. **エージェント履歴**: `agent-transcripts/*.jsonl`（キーワード検索、全文読み込み禁止）
4. **Obsidian**（ユーザー vault）:
   - 明示パスがあればそこを Grep
   - なければユーザー personal store や `~/Documents` 配下を **ユーザー確認後** に限定検索
5. **Git**: `git log -S` / `git log --grep` で過去修正理由

## Agentic RAG 手順

1. **クエリを3語以上** に分解（例: 光の柱 / ダイブ / 墜落）
2. 各ソースで **1回以上** 検索してから回答開始
3. ヒットしたノート・ファイルを **リンクまたはパス** で引用（Obsidian は `[[ノート名]]` 可）
4. 矛盾する過去メモがあれば両方示し、新しい方針を1文で選ぶ理由を書く

## 軽量ナレッジグラフ（Markdown）

複雑な依存があるとき、回答末尾に:

```text
[概念A] --caused--> [過去インシデントB] --fixed_by--> [コミット/スキルC]
```

## Obsidian 執筆ルール（ユーザー設定）

- YAML frontmatter、内部リンク `[[...]]` を新規ノートに使う場合はユーザー依頼時のみ

## ハーネス連携

- 状態保存: `harness-memory-persistence`
- 秘密・API キーがノートにあれば **引用しない**（`harness-security-guardrails`）

## 出力

結論の前に **参照したソース一覧**（最大5件）。0件なら「未検索」と明言。
