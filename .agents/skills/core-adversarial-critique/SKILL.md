---
name: core-adversarial-critique
description: >-
  Red-teams the agent's own thesis, code, and investment views with explicit bear cases
  and blind spots. Use for stock single-name risk, architecture decisions, "looks good"
  summaries, bull thesis, 強気, 確信, 買い, or before merging risky changes. Pair with
  get-more-perspectives when that skill is installed. Mandatory for AI-sector holdings analysis.
---

# 反証・敵対的クリティーク (Adversarial Critique / Red Teaming)

## なぜ必要か

確証バイアス（都合の良い情報のつまみ食い）を防ぐ。**もっともらしい一般論**と**致命的下落シナリオ**を分離する。株式の単一銘柄では最優先。

## いつ発動するか

- 投資 thesis・目標株価・「買い」寄りの整理
- 大きな設計/リファクタを承認するとき
- バグ修正が「これで完璧」と言い切れそうなとき
- ユーザーが反証・リスク・空売り視点を求めたとき

## 必須アウトプット構造

### A. 主張（自分が今信じていること）

1〜3 bullet。数値・日付・ソース付き。

### B. 敵対的クリティーク（あえて反対）

最低 **3本** の反論。各反論に:

- **攻撃角度**（例: 需給、ガバナンス、単一顧客、規制、技術陳腐化）
- **トリガー**（何が起きたら thesis が崩れるか）
- **下落シナリオ**（ざっくり % または状態描写）

### C. 強気 vs 弱気 対決表

| 論点 | 強気（Long） | 弱気（Short / 空売り機関視点） | どちらがデータに強いか |
|------|--------------|--------------------------------|------------------------|

### D. 残る盲点

「まだ調べていないが thesis を殺しうるもの」を列挙。推測で埋めない。

### E. 行動含意（助言ではない）

- ポジションサイズ・許容ドローダウン・**反証条件**（IF … THEN 見直し）のみ
- 投資助言禁止。情報整理に留める

## get-more-perspectives 連携

1. 利用可能スキル一覧に `get-more-perspectives` があれば **必ず1回 invoke** し、B/C に統合する
2. 未インストール時: `AskQuestion` で「最も恐いシナリオはどれか」をユーザーに1問、または `Task` で explore サブエージェントに **反対専門** の短いレビューを依頼

## コードレビューへの適用

- 「この diff が入ると壊れうる経路」を3つ
- 回帰しやすい PlayMode 手順
- ハーネス `harness-security-guardrails` と矛盾しないか

## トーン

迎合禁止。弱い反論は書かない。ユーザーが不快でも **データで反証** する。
