---
name: harness-context-efficiency
description: コンテキストウィンドウとトークン効率の最適化スキル。JIT（Just-In-Time）読み込みと局所差分編集を徹底する。
---

# Harness Context Efficiency

エージェントの思考速度と精度を最大化するためのコンテキスト管理原則。

## 原則
1. **Progressive Disclosure**: 全ファイルを一度に読み込まず、必要な関数・行範囲のみをピンポイントで閲覧。
2. **Local Diff Only**: ファイル全体の上書きを避け、`replace_file_content` で差分ブロックのみを編集。
3. **Log Sanitization**: 巨大なログは要約またはエラー行のみを抽出し、コンテキストの圧迫を防ぐ。
