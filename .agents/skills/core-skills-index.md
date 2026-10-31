# コアスキル × ハーネススキル

| 層 | 役割 |
|----|------|
| **コアスキル（本ディレクトリ `core-*`）** | 推進力と知能 — 分解・反証・ツール・知覚・記憶・自己進化 |
| **ハーネススキル（`harness-*`）** | 安全境界 — 品質ゲート、リトライ上限、ガードレール、状態保存 |

どちらか一方だけでは不十分。コアが「どう解くか」を担い、ハーネスが暴走と手抜きを止める。

## コアスキル一覧（優先度順）

| # | スキル | ディレクトリ |
|---|--------|--------------|
| 1 | タスク分解・動的再計画 | `core-task-decomposition-replanning/` |
| 2 | 反証・敵対的クリティーク | `core-adversarial-critique/` |
| 3 | MCP・ツールオーケストレーション | `core-mcp-tool-orchestration/` |
| 4 | マルチモーダル知覚・GUI操作 | `core-multimodal-gui-perception/` |
| 5 | セマンティック検索・知識グラフ | `core-agentic-rag-knowledge-graph/` |
| 6 | メタ認知・スキル自己生成 | `core-meta-cognition-skill-synthesis/` |

## ドメインスキル

| スキル | ディレクトリ | いつ |
|--------|--------------|------|
| 100点満点マスター・ゲームフィール調律 | `core-gamefeel-perfection-100/` | 厳格な100点満点評価基準を完全突破し、極上の手触りと完成度を実装するとき |
| Unity / iOS MVP 設計 | `unity-app-architect/` | Obsidian のアイデアメモから SE3 向け Unity 構成を出すとき |
| 厳格100点判定とロードマップ | `rustfloat-strict-100-roadmap/` | ゲーム評価・100点・減点・出荷判断。採点表は harness-eval-coverage、厳しさはこちら |

## Cursor / Antigravity

- **Antigravity**: リポジトリ内 `.agents/skills/` を参照（本ファイルと同階層）。
- **Cursor**: ユーザー個人ストア `skills/core-*` に同一内容をミラー（全プロジェクト共通）。

評価用プロンプト: `evals/core-skills-evals.json`
