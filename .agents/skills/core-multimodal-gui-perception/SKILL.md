---
name: core-multimodal-gui-perception
description: >-
  Reads charts, PDF figures, Unity Game view, DTM/plugin UIs, and web screens from images
  or browser snapshots; extracts structured data. Use when the user attaches screenshots,
  chart images, IR slides, 決算資料, チャート, Unity画面, GUI, PDF図, or errors visible only on screen.
---

# マルチモーダル知覚・GUI操作 (Multimodal Perception & Computer Use)

## なぜ必要か

テキスト化されていない図表・画面上のエラーは **見ないと判断を誤る**。

## 入力ソースと手段

| ソース | 手段 |
|--------|------|
| ユーザー添付画像 | Read ツール（png/jpg/webp） |
| Unity Game 画面 | `browser_take_screenshot` / Unity MCP / ユーザーキャプチャ |
| Web IR・株価 | `cursor-ide-browser` snapshot + screenshot |
| PDF | 可能ならテキスト抽出ツール; 不可ならページ画像化をユーザーに1依頼 |

## ワークフロー

1. **何を読み取るか** を先に宣言（例: 支持線、出来高スパイク、Console 赤文字）
2. 画像/snapshot を取得
3. **観察のみ** を箇条書き（推測と分離）
4. 構造化出力（JSON / 表）— Canvas  skill が使える場合は可視化優先
5. 読めない部分は `"unclear"` と明記。 hallucinate 禁止

## チャート・IR 向け抽出フィールド（例）

```json
{
  "time_range": "",
  "trend": "up|down|range",
  "support_levels": [],
  "resistance_levels": [],
  "volume_anomaly": "",
  "caption_or_title": "",
  "confidence": "high|medium|low"
}
```

## GUI 操作

- Browser: lock → navigate → snapshot → interact → unlock（MCP 手順遵守）
- Unity: 直接クリックより **再現手順**（メニュー名・シーン名）をログに残す
- Logic Pro / DTM: プラグイン名とパラメータ **目視値** のみ記録。自動操作はユーザー許可時のみ

## ハーネス連携

- スクショに秘密情報（口座番号等）が写る場合はマスクまたは保存しない（`harness-security-guardrails`）

## 限界

解像度不足・日本語フォント潰れ・複数画面は **確認1問** で補う。
