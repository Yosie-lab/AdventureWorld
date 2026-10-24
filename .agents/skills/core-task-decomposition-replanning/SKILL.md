---
name: core-task-decomposition-replanning
description: >-
  Decomposes vague goals into dependency-ordered plans (DAG) and replans when blocked.
  Use for any multi-step task, Unity bugs, stock analysis pipelines, refactors, or when
  the user mentions タスク分解, 再計画, 手順, 依存関係, 行き詰まり, or ambiguous "やって" requests.
  Always apply before large implementation diffs.
---

# タスク分解・動的再計画 (Task Decomposition & Re-planning)

## なぜ必要か

1箇所のエラーで思考停止したり、手順が前後して手戻りが増えるのは、**全体像と依存関係が見えていない**ため。実装前に逆算し、途中でルートを切り替える条件を決めておく。

## いつ発動するか

- 3ステップ以上かかりそうな依頼
- 原因不明のバグ・「直して」だけの指示
- 仕様変更・新情報が途中で入ったとき
- 投資・調査・開発が混在する依頼

## ワークフロー

### 1. ゴールを1文で固定

- **完了状態**（ユーザーが何を見れば OK か）を書く
- **非ゴール**（今回やらないこと）を1〜3行で明示

### 2. 依存関係グラフ（DAG）を書く

Markdown で十分。例:

```text
[G0 再現確認] → [G1 原因特定] → [G2 最小修正] → [G3 PlayMode/CI検証] → [G4 報告]
         ↘ [G1b ログ収集] ↗
```

各ノードに **成果物**（ファイル名、ログ、スクショ）を1つずつ紐付ける。

### 3. 実行順

- 並列可能なノードは同時に調査（read/grep/shell）
- **ブロッカー**（権限、Play Mode、API鍵、ユーザー判断）を先に列挙

### 4. 動的再計画（Re-plan）トリガー

次のいずれかで **DAG を更新してから** 続行する。同じ失敗を3回超えない（ハーネスと連携）:

| トリガー | 行動 |
|----------|------|
| 仮説が1回で裏切られた | 別仮説ノードを追加、旧ルートを abandon とマーク |
| 触ってはいけないファイルに当たった | AGENTS/HANDOFF を再読み、迂回ルート |
| ツール/MCP が使えない | 代替ツール列を `core-mcp-tool-orchestration` に従い差し替え |
| スコープが膨らんだ | 非ゴールを再確認し、ユーザーに1問だけ確認 |

### 5. 報告形式（日本語・結論ファースト）

1. 結論（完了 / 部分完了 / ブロック）
2. 実行したノードと結果
3. 次の1ステップ（または再計画した DAG の差分）
4. 確認1問

## Unity / RustAndFloat 向け注意

- `HANDOFF.md` と `AGENTS.md` を DAG の **G0** に含める
- シーン保存・コミット前: Play Mode 停止（`harness-quality-gates`）

## 例

**依頼**: 「光の柱ダイブ前に浮く」

**DAG**: 再現手順 → 上昇トリガー grep → CC/BlocksSkybreakColumnLift 確認 → 最小 diff → レバー〜ダイブ手動チェックリスト
