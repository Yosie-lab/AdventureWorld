---
name: unity-app-architect
description: >-
  Turns an Obsidian idea memo into a Unity/iOS MVP: minimal spec, C# script map,
  Inspector setup with copy-paste code, and an iPhone SE3 device checklist.
  Use when the user mentions Unity, iOS, iPhone SE3, MVP, アイデアメモ, Obsidian,
  インスペクタ, スクリプト構成, 実機検証, or wants to design a small Unity app from notes.
---

# Unity / iOS 設計・実装 (unity-app-architect)

アイデアメモを、実装に落ちる4点セットへ変換する。推測でスコープを広げない。不足は「未確定」と書き、確認は最後に1問だけ。

## いつ読むか

- Obsidian ノート、箇条書きアイデア、「アプリにして」
- Unity シーン／Prefab／C# の最小構成を先に決めたいとき
- iPhone SE3 実機での検証項目が必要なとき

関連スキル: メモ探索は `core-agentic-rag-knowledge-graph`。実装に入る前の手順は `core-task-decomposition-replanning`。画面のスクショは `core-multimodal-gui-perception`。

## 入力の読み方

1. ユーザーがパスを渡したらその Markdown を読む。渡していなければ vault 内をキーワード検索し、ヒットを最大3件引用する。
2. このリポジトリが RustAndFloat のとき: `AGENTS.md` と `HANDOFF.md` を先に読む。`AdventureWorld.unity` と `Land_Terrain.asset` は変更対象にしない。
3. メモに無い機能は MVP に入れない。

## 制約（既定）

| 項目 | 既定 |
|------|------|
| 対象端末 | iPhone SE（第3世代）。論理 375×667 pt、物理 750×1334、ホームボタン、A15 |
| エンジン | 既存プロジェクトの Unity バージョンに合わせる。新規なら Unity 6 + URP |
| 入力 | タッチ＋画面内ボタン。SE3 は画面が狭いので片手親指で届く配置 |
| スコープ | 1画面〜2画面で「動く核」だけ。課金・アカウント・多言語はメモに無い限り後回し |
| 言語 | 成果物は日本語。コード識別子は英語 |

## 必ずこの順で出力する

見出し名は変えない。

### ① 最小構成のMVP仕様

```markdown
## ① 最小構成のMVP仕様
- 誰が / 何を / 成功条件（1文）
- 画面: （最大2）
- 操作: （タップ・スワイプ等、3つ以内）
- データ: （保存するなら PlayerPrefs か 1 ScriptableObject。サーバは MVP 外）
- 含まないもの: （メモにあっても後回し）
- 未確定: （推測していない穴）
```

### ② 必要なC#スクリプト構成図

MonoBehaviour は画面と入力に限る。ロジックは plain C# に分ける。図はテキストでよい。

```text
Assets/Scripts/
  AppBootstrap.cs          // 起動時に1回だけ参照を結ぶ
  GameLoop.cs              // Update の入口（薄く）
  Core/
    SessionState.cs        // 状態だけ。UnityEngine 非依存が理想
  UI/
    HudView.cs             // 表示の反映だけ
```

各ファイルに「責務1行」「依存（誰を参照するか）」を付ける。新規クラスは MVP に必要な本数だけ（目安 4〜8）。

### ③ Unityインスペクタの設定手順（コピペ用コード付き）

手順は番号付き。各ステップに **Hierarchy 上のオブジェクト名** と **Add Component する型** を書く。

コードは次を満たす:

- `[SerializeField]` で Inspector 配線。`Find` は起動時1回までに限る
- セーフエリアは `Screen.safeArea` を RectTransform に反映する短いメソッドを付ける
- SE3 向けにフォントサイズ・ボタン高さの初期値をコメントする（ボタン高さ ≥ 44pt 相当）

例（そのまま使える骨格。プロジェクト名に合わせて namespace だけ変える）:

```csharp
using UnityEngine;

public class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] RectTransform target;

    void Awake()
    {
        if (target == null) target = GetComponent<RectTransform>();
        Apply();
    }

    void Apply()
    {
        var sa = Screen.safeArea;
        var min = sa.position;
        var max = sa.position + sa.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;
        target.anchorMin = min;
        target.anchorMax = max;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}
```

シーン保存の前に Play Mode を止める。

### ④ iPhone SE3実機での検証チェックリスト

Development Build で実機確認する項目だけ。Editor Game ビュー（375×667）は事前確認であり、実機の代替にしない。

```markdown
## ④ iPhone SE3実機での検証チェックリスト
- [ ] 解像度 375×667 で主要UIが欠けない（ホームボタン側・ステータスバー）
- [ ] ボタンが親指で押せる（高さおおよそ44pt以上、端に寄りすぎない）
- [ ] 起動から目的操作まで30秒以内に到達する
- [ ] 画面回転はメモの指定どおり（未指定なら Portrait 固定）
- [ ] バックグラウンド→復帰で状態が壊れない
- [ ] 60fps付近を維持（プロファイラでスパイク箇所を1つメモ）
- [ ] 音が無音マナー／サイレントで期待どおり（鳴らすなら明示）
- [ ] クラッシュなしでコアループを3周
```

メモ固有の操作があれば、その手順をチェック項目として最大3つ追加する。

## やってはいけないこと

- MVP にサーバ、IAP、広告、アカウント連携を黙って足す
- インスペクタ手順なしで「後は Unity で設定」と終わる
- SE3 をノッチ付き大型 iPhone と同じセーフエリア前提で書く
- RustAndFloat 作業中に本編シーンの地形を仕様の実験台にする

## 確認は1問

出力の最後に、未確定が残っていれば **1つだけ** 聞く。無ければ「この MVP でスクリプトをリポジトリに置きますか？」と聞く。
