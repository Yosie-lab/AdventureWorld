---
tags: [rust-and-float, handoff]
created: 2026-09-11
---

# RustAndFloat 引き継ぎ

次のエージェント（Antigravity 含む）は、このファイルを最初に読む。作業対象は **RustAndFloat**。元ゲーム **AdventureWorld は壊さない**。

## 触ってよい / 触るな

| 触ってよい | 触るな |
|---|---|
| `Assets/RustAndFlat/` | `Assets/Scenes/AdventureWorld.unity` |
| `Assets/Game/Scripts/AdventureRust*.cs` | `Assets/Idyllic Fantasy Nature/Demo/Settings/Land_Terrain.asset` |
| `Assets/Game/Scripts/AdventurePalmFactory.cs` | `Assets/Game/Scripts/AdventurePlayerController.cs` の AdventureWorld 専用挙動を壊す変更 |
| `Assets/Game/Scripts/AdventureButterflyDrift.cs` | Build Settings を Demo/Loader に差し替えること |
| `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs` / Open / Build | 秘密情報・`.env` |

フォルダ名は `RustAndFlat`、製品名・メニュー名は **RustAndFloat**。ユーザーは RustAndFloat と呼ぶ。

## 開き方

Unity メニュー: **Adventure → Open RustAndFlat Scene (new island)**

シーン: `Assets/RustAndFlat/Scenes/RustAndFlat.unity`

再生中にシーン保存しない。■ で止めてから保存。

## いまの中身

- 丸い島（256×48×256）。海面 y=5.5。北に滑空用の崖。見晴らし台スポーン ≈ `(138, 33, 176)`、北向き。
- niko: `CharacterController` + `AdventurePlayerController`（`canGlide=true`）。空中 WASD 歩行なし。Space 長押しで滑空。R でリセット。
- 海に沈まない（`waterLevel` で浮く）。急斜面は歩けず落ちる。
- 相棒 **Rust**: 錆びた球ドローン。遅れ・ヒッチ・きしみ音・熱けむり・油垂れ。
- 楽園見た目: 花の木・緑の木・花畑・岸の岩・ヨシ・ヤシ（羽状の葉）・蝶。北の崖は空けてある。
- 浮いていた円柱 `CliffLookout` は削除済み。

## 主要ファイル

- シーン: `Assets/RustAndFlat/Scenes/RustAndFlat.unity`
- 地形: `Assets/RustAndFlat/Terrain/IslandTerrain.asset`
- Rust: `Assets/RustAndFlat/Prefabs/Rust.prefab` / `Assets/Game/Scripts/AdventureRustDrone.cs`
- 島バウンド: `Assets/Game/Scripts/AdventureRustFloatIsland.cs`（`DefaultExecutionOrder(-300)`）
- 滑空: `Assets/Game/Scripts/AdventurePlayerController.cs`
- Boot スキップ: `Assets/Game/Scripts/AdventureWorldBoot.cs` が `RustAndFlat` / `RustAndFloat` なら early-return（これを外すと海面 `OceanPlane` が消える）
- 楽園配置: `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs`（メニュー Adventure → Dress RustAndFloat Paradise）
- ヤシ生成: `Assets/Game/Scripts/AdventurePalmFactory.cs`
- 蝶: `Assets/Game/Scripts/AdventureButterflyDrift.cs` + Idyllic の Butterfly prefab

## 設定メモ

- 入力: Input System only。Editor 再生時は Game ビューにフォーカスしなくても WASD が来る想定。
- 植生の再配置は Play 停止後に Dress メニュー。北崖・スポーン周辺は空けている。
- ヤシはプロジェクトに FBX が無いので手続きメッシュ。もっとリアルにするなら専用アセットが必要。

## まだやっていない（次の候補）

ユーザーは一手ずつ、確認質問は1つ。日本語、結論ファースト。

1. 北の崖を滑空向きに整える
2. この島の冒頭セリフを2050にする（AdventureWorld の会話は触らない）
3. 砂浜に漂着ゴミ（2030〜2050の年代グラデーション）
4. Rust の対話・油をアイテム化する
5. フォルダ名 RustAndFlat → RustAndFloat のリネーム（参照切れに注意）

## ブランチ

`refactor/island-map-and-terrain-cleanup`  
リモート: `https://github.com/Yosie-lab/AdventureWorld.git`

コミット方針: ユーザーが commit / push を明示したときだけ。AdventureWorld の地形と Demo/Loader へのビルド差し替えは入れない。
