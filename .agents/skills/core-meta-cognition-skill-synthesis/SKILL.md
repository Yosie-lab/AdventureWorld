---
name: core-meta-cognition-skill-synthesis
description: >-
  Extracts reusable patterns from completed work and drafts new SKILL.md definitions.
  Use after successful bugfixes, workflows, or when the user asks to スキル化, 手順書,
  再利用, 次回から, meta, or 成功パターン. Works with create-skill and skill-creator eval loops.
---

# メタ認知・スキル自己生成 (Meta-Cognition & Skill Synthesis)

## なぜ必要か

毎回プロンプトを微調整する運用は頭打ち。**成功パターンをスキルに昇華** してトリガー可能にする。

## いつ発動するか

- タスク完了でユーザーが満足 / チェックリストが通った
- ユーザーが「スキルにして」「次回も」
- 同型タスクが2回目以降

## 昇華ワークフロー

### 1. 振り返り（5分相当）

- **トリガー**: どんなユーザー言い回しで発動すべきか
- **入力**: 必要ファイル・MCP・権限
- **手順**: 3〜8ステップ（DAG があればそれを圧縮）
- **アンチパターン**: 今回ハマった穴
- **検証**: 完了の定義（1行）

### 2. スキル草案

`create-skill`（`~/.cursor/skills-cursor/create-skill/SKILL.md`）に従い:

```markdown
---
name: ...
description: ...（WHAT + WHEN、やや pushy）
---

# タイトル
## なぜ
## 手順
## 例
```

- 500行未満。深い詳細は `references/` へ

### 3. 配置

| 環境 | パス |
|------|------|
| Cursor 個人 | ユーザー Agent Store `skills/<name>/SKILL.md` |
| Antigravity /  repo | `RustAndFloat/.agents/skills/<name>/` |
| プロジェクト限定 | `.cursor/skills/<name>/`（チーム共有時） |

**両方** に置く場合は内容同一。index `core-skills-index.md` を更新。

### 4. 評価（skill-creator）

- `evals/*.json` に prompt 1〜3件追加
- 可能なら with-skill vs baseline（skill-creator 手順）

### 5. ユーザー確認1問

「このスキル名とトリガーで保存してよいか」— 明示 commit 指示があるまで git commit しない。

## ハーネスとの役割分担

- コア（本スキル）: **新しい推進ルール** を増やす
- ハーネス: リトライ上限・禁止事項・品質ゲートは **スキル化しない**（既存 harness-* を改変）

## メタ認知チェック

昇華前に自問:

- これは1回限りのハックか、一般化できるか？
- 一般化すると **誤発火** しない description か？

不要なら「スキル化見送り」と結論。
