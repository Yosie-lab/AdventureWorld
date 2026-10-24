---
name: core-mcp-tool-orchestration
description: >-
  Selects and orchestrates MCP, Shell, Git, browser, and Unity tools with correct order
  and parallelization. Use when fetching data, editing files, running Unity, GitHub,
  J-Quants, or any task that must not end as text-only advice. Triggers on 実行して,
  調べて, MCP, API, git, Unity, ブラウザ, 実際に.
---

# MCP・ツールオーケストレーション (Tool Use & MCP Integration)

## なぜ必要か

テキストだけ出して人間に手作業させない。**環境でできることは環境でやる**。

## 基本原則

1. **Discover → Invoke**: MCP は `GetDynamicTools` でスキーマを読んでから `CallDynamicTool`
2. **Read before write**: 状態変更前に read/status/diff
3. **Parallel 独立調査**: git status + diff + log、複数 grep は同時
4. **Sequential 依存操作**: コンパイル確認 → 編集 → 再コンパイル
5. **失敗時**: 別ツール経路へ（skill `core-task-decomposition-replanning` で再計画）

## ツール選択マトリクス（代表）

| 目的 | 第一選択 | 代替 |
|------|----------|------|
| Unity Play/コンパイル | `user-unityMCP` read_console, manage_editor | ユーザーに Play 結果を1問 |
| ファイル編集 | Read / StrReplace / Write | なし（推測でコードを提示だけしない） |
| GitHub PR/CI | `gh`, `cursor-origin` | WebFetch |
| ブラウザ確認 | `cursor-ide-browser` snapshot → click | スクショ + multimodal skill |
| 株・IR API | ユーザー設定 MCP（J-Quants 等） | 公式 URL + WebFetch（robots 遵守） |
| サブエージェント調査 | `Task` explore / generalPurpose | 自分で grep |

## Unity MCP 典型パイプライン

```text
editor/state (ready?) → read_console (errors) → 修正 → refresh/read_console → Play 指示
```

`harness-quality-gates` と併用: 保存前 Play 停止。

## 並列 vs 直列

**並列 OK**: 独立ファイルの grep、git status/diff/log、複数 MCP read-only

**直列必須**: commit（status→add→commit→status）、Unity コンパイル後のコンポーネント追加、PR 作成（push 後 create）

## 権限

- sandbox で hash 失敗 → `required_permissions: ["all"]` を理由付きで再試行
- ネットワーク API → `full_network` when needed

## 報告

実行したコマンド/ツール名と **観測結果** を短く。推測と観測をラベル分け。

## Antigravity / Cursor 共通

- Cursor: Dynamic Tools + Shell + スキル `harness-tool-coverage`
- Antigravity: 同一 MCP 設定（`RustAndFloatMcpBootstrap` 等）を前提。未接続なら接続手順を1ステップで提示
