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
| `Assets/Game/Scripts/AdventureRust*.cs`（Opening / Island / Drone） | `Assets/Idyllic Fantasy Nature/Demo/Settings/Land_Terrain.asset` |
| `Assets/Game/Scripts/AdventurePalmFactory.cs` | `Assets/Game/Scripts/AdventurePlayerController.cs` の AdventureWorld 専用挙動を壊す変更 |
| `Assets/Game/Scripts/AdventureButterflyDrift.cs` | Build Settings を Demo/Loader に差し替えること |
| `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs` / Open / Build / Shape North Cliff | 秘密情報・`.env`・巨大な未追跡 `Assets/Art` |

フォルダ名は `RustAndFlat`、製品名・メニュー名は **RustAndFloat**。ユーザーは RustAndFloat と呼ぶ。

## 開き方

Unity メニュー: **Adventure → Open RustAndFlat Scene (new island)**

シーン: `Assets/RustAndFlat/Scenes/RustAndFlat.unity`

再生中にシーン保存しない。■ で止めてから保存。

## いまの中身

- **Grand Island（1024×120×1024）**。海面 y=5.5。メニュー **Adventure → 🏝️ Build 1000m Grand Sanctuary Island** で生成。
- **広大な白砂ビーチ**: 標高6.0m〜8.5m、島を一周する幅約50mの鮮やかな白砂ビーチテラス。
- **西側の大草原（Grand Meadows）と東部〜北部の大樹海（Deep Ancient Forest）の壮大な共存**:
  - **東部〜北部の広大無辺な大樹海（Deep Forest）**: 島の東側から北部全域（x: 460〜890, z: 180〜750）を占める深緑の原生林。モミの木（Fir 01〜05）や深緑の巨木（スケール1.3〜2.5倍）を中心に **2,200本** の木々が高密度に林立し、林冠が空を覆う深い森の天蓋（密林クラスター）を形成。林床には **3,200本** の灌木やシダが生い茂り、木漏れ日と蝉時雨が響く本格的な深い森を再現。
  - **西側の広大な大草原（Grand Meadows）**: スタート地点（せせらぎ池平原 x=265, z=330）を中心とする西側全域は、木を爽やかな一本杉や木立（4%）に留め、地平線まで突き抜ける大草原の開放感を100%維持。Terrain Detail草（描画距離500m）による波打つ緑の絨毯＋要所のアクセント花畑。シーンサイズを27MBに最適化しGit/GitHubへ完全対応。
  - **二面性の絶景**: スタート地点からは眼前一面に大草原と青空が広がり、東の丘陵に目を向けると雄大な大樹海が広がる、絵画のようなコントラストを実現。
- **清流・大渓流・3つの池（Water & Ravine System）**:
  - **東の深林大渓流（Eastern Mountain Torrent）**: 標高40mの東部高地原生林からカルデラ湖（25.5m）へ急流で下る全長300m・落差約15mの本格的な山岳渓谷（V字谷掘り込み、24セグメント水面）。
    - 渓流の川底・川岸に **450個以上の巨石・苔岩・飛び石** を敷き詰め、白波を立てて縫うように激しく下るダイナミックな渓流美を再現。
    - 岸辺には葦・シダ・ガマなどの水辺植物と、岩陰から響くカジカガエルの美声（4箇所）。
  - **オアシス湧水池（SanctuarySpringPond）**: 中央台地足元（x=480, z=455, 標高48.2m, 半径16m）。
  - **上流急流（UpperParadiseStream）**: オアシス湧水池からカルデラ湖へ下るせせらぎ。
  - **大カルデラ湖（CalderaLake）**: x=420, z=440, 標高25.5m, 半径38mの広大な湖。水面一面の睡蓮・蓮の葉、湖畔のカエルの大合唱。
  - **大草原の憩いのせせらぎ池（MeadowLowlandPond）**: 西側の大草原の真ん中（x=290, z=320, 標高14.5m, 半径16m）。
  - **本流大河（ParadiseRiver）**: カルデラ湖から草原池の脇を蛇行し南西の海（5.5m）へ注ぐ大河・小川（飛び石2箇所付き）。
  - **水辺の生き物**: カエル36匹（跳躍アニメーション）、トンボ28匹（ホバリング飛行）。
- **抜けるように澄んだ青空と気持ちの良い雲群（Sky & Clouds）**:
  - **深く澄み切った青空スカイボックス（`RustAndFloat/ClearBlueSky`）**: 天頂の吸い込まれるような深い群青から眩しいアズールブルー、地平線の澄んだシアンブルーへと抜ける高彩度・低白ボケの快晴グラデーション。
  - **快晴の夏の日差し**: Directional Light（仰角50度、方位140度、強度1.8、Soft Shadows）＋草原と青空に調和したTrilight環境光。
  - **気持ちの良い白い雲（`ParadiseClouds`）**: 高度115m〜225mの空に18個のふんわりとした立体雲クラスター（シェーダー `RustAndFloat/FluffyCloud`）が風に乗ってゆったりと漂う（`AdventureCloudDrift`）。
  - **上空の風音**: `skywind_1.wav`（ボリューム0.03fのごく微かな気配に抑制し、耳障りな風切り音を解消）。
- **北の大滑空崖**（x=512, z=720, 標高≈92m）: z>750で海面下へ急落。
- 旧256m島は **Adventure → Build RustAndFloat Island (256m)** で再生成可能。
- niko: `CharacterController` + `AdventurePlayerController`（`canGlide=true`）。空中 WASD 歩行なし。Space 長押しで滑空。R でリセット。
  - **かわいい足音（`AdventureNikoFootsteps.cs`）**: `AdventurePlayerController` の起動時に自動アタッチ。2Dダイレクト音響（距離減衰ゼロ）。
    - **控えめで愛らしい音量**: 環境音や蝉の邪魔にならない優しい音量（`volume = 0.22f`、歩行時約0.16、ダッシュ時約0.22）。
    - **愛らしいトコトコ音**: 移動速度に合わせてリズム良く鳴る（左足・右足で交互にピッチが異なる愛らしい打楽器・ポップ音 `niko_step_L/R.wav`）。
    - **全地形共通の心地よさ**: 草原、砂浜、水辺・海の中もすべて共通の可愛いトコトコ音で統一。
    - **空中・滑空時の消音**: ジャンプ中や滑空中は自然に消音され、着地した瞬間にトコトコ再開。
- **寄せては返す波の音（`AdventureBeachWavesManager.cs`）**:
  - `AdventurePlayerController` の起動時に自動生成。
  - リマスター波音源 `ocean_waves_grand.wav`（+33.4dBノーマライズ、時間差2重クロスブレンド）を使用。
  - 外周ビーチや海岸線（海抜5.5m〜12m、半径345m以上）に近づくと自然にフェードインし、プレイヤーを包み込むリアルな潮騒の波音が響く（最大音量 `masterVolume = 0.55f` にほんの少し引き上げ）。内陸に入ると自然に静まり、蝉や虫の声が引き立つ。
- 海に沈まない（`waterLevel` で浮く）。急斜面は歩けず落ちる。
- 相棒 **Rust**: 錆びた球ドローン。遅れ・ヒッチ・きしみ音・熱けむり・油垂れ。
  - きしみ音の音量をほんの少し控えめ（`soundVolume = 0.28f`、従来の約65%）に調整。再生間隔も少し落ち着かせ（1.2〜2.2秒）、nikoの足音や波音・蝉の声に優しく寄り添う音響に設定。
- **「Assets/虫の声」による状況別の超リアルな自然音響システム**:
  - **ランタイム常駐蝉・虫音響（`AdventureCicadaAmbienceManager.cs`）**:
    - `AdventurePlayerController` の起動時に自動生成。
    - プレイヤーがどこにいても（スタート地点のせせらぎ池、西側大草原、砂浜・海岸線、東部樹海）、夏の日差しに合わせた『ミンミンゼミが鳴く雑木林』や『夏の田舎道』の広域アンビエンスが優しく響く。
    - プレイヤーの周囲（14m〜32m）の木立・草むらから、ランダムにヒグラシ、ミンミンゼミ、ツクツクボウシ、アブラゼミ、ニイニイゼミ、エンマコオロギが3D立体音響で時折鳴り響く。
  - **11大森林・木立ゾーンと蝉時雨（西側大草原・スタート地点・海辺にも増設！）**:
    - 東丘陵深緑原生林、北崖奥古樹林、カルデラ湖東岸木立、東部樹海南スロープ、北東樹海高地、東の深林大渓流沿い、オアシス湧水池木立、草原〜湖畔アプローチ木立に加えて、**スタート地点・せせらぎ池西岸木立**、**西側ビーチテラス海辺木立**、**南西草原・小川沿い木立**の11箇所に拡大。
    - 『ミンミンゼミが鳴く雑木林』『夏の山1・2』『ヒグラシ』の広域アンビエンス＋『ミンミンゼミ』『アブラゼミ1・2』『ツクツクボウシ1・2』『ニイニイゼミ』『ヒグラシ』『エンマコオロギ』の梢3D点音源を大量配置。
  - **水辺のカエル・渓流・砂浜の波音**:
    - **砂浜の波音（BeachWaves）**: 外周白砂ビーチ沿い（半径440m〜455m）に **24箇所の3D波音源** を円周配置。海辺に近づくとザザーッと寄せては引くリアルな波打ち際の潮騒がサラウンドで包み込む。
    - 大カルデラ湖（睡蓮・葦の群生）: 『カエルの大合唱』が湖畔一面に大音量で響く（4箇所）。
    - オアシス湧水池＆飛び石: 『01カジカガエル』の澄んだ「フィー、コロコロ」という美しい鳴き声（4箇所）。
    - 小川・急流: 『渓流』の清涼なせせらぎ音（ボリューム0.22〜0.25fに落ち着かせ、風音との混同を解消）。
  - **大草原の虫の声**:
    - 草原の草むら・花畑: 『エンマコオロギの鳴き声』が足元からコロコロと響く（18箇所）。
    - 草原の小道沿い: 『夏の田舎道』の風擦過音をボリューム0.10fに抑えて静けさを確保。
- **楽園の生き物**: カエル（36匹、池・小川）、カニ（35匹、砂浜横歩き）、トンボ（28匹、水辺・草原ホバリング）、蝶（36箇所）、カピバラ（18頭）、犬・猫（各10匹）。
  - ※空中を白い四角いボックスとして飛んでいた表示崩れVFX（`VFX_RoundingBirds`, `VFX_Insect`）は完全に排除・クリーンアップ済み。鳥の表現はリアルな3D環境音（森の小鳥、海岸のカモメ）で快適に演出。
- **2050冒頭セリフ**: `AdventureRustFloatOpening`。`AdventureRustFloatIsland.Start` がコンポーネントを付ける。
- **操作ガイド＆コンパスHUD**:
  - 画面最上部中央にミニマルで上品な水平リボンコンパスHUD（`AdventureCompassHUD.cs`）を常時表示。カメラの旋回に合わせて方角（N, NE, E, SE, S, SW, W, NW）と度数が滑らかに追従し、北（N）はシアン色で視認性抜群。邪魔にならない半透明デザイン。
  - 操作ガイドはコンパスの下（y = -44f）に `【WASD】移動　【Space長押し】崖から滑空　【R】リセット` と表示。

## 主要ファイル

- シーン: `Assets/RustAndFlat/Scenes/RustAndFlat.unity`
- 地形: `Assets/RustAndFlat/Terrain/IslandTerrain.asset`
- Rust: `Assets/RustAndFlat/Prefabs/Rust.prefab` / `Assets/Game/Scripts/AdventureRustDrone.cs`
- 島バウンド: `Assets/Game/Scripts/AdventureRustFloatIsland.cs`（`DefaultExecutionOrder(-300)`）
- 冒頭HUD: `Assets/Game/Scripts/AdventureRustFloatOpening.cs`（ランタイム生成、シーンには無い）
- 滑空: `Assets/Game/Scripts/AdventurePlayerController.cs`
- Boot スキップ: `Assets/Game/Scripts/AdventureWorldBoot.cs` が `RustAndFlat` / `RustAndFloat` なら early-return（これを外すと海面 `OceanPlane` が消える）
- 楽園配置: `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs`（メニュー Adventure → Dress RustAndFloat Paradise）
- 島生成: `Assets/Game/Scripts/Editor/AdventureBuildRustFloatIsland.cs`（メニュー Adventure → 🏝️ Build 1000m Grand Sanctuary Island / Build RustAndFloat Island (256m) / Shape RustAndFloat North Cliff）
- グリッド床シェーダー: `Assets/RustAndFlat/Shaders/SanctuaryGridGlow.shader`
- グリッド床マテリアル: `Assets/RustAndFlat/Materials/SanctuaryGridGlow.mat`
- グリッド床スクリプト: `Assets/Game/Scripts/AdventureSanctuaryGridGlow.cs`（Play中、プレイヤー接近で発光）
- ヤシ生成: `Assets/Game/Scripts/AdventurePalmFactory.cs`（Hawaii Beach House アセットの高精細ヤシの木モデル + URP両面マテリアル `Assets/RustAndFlat/Materials/HawaiiPalm_URP.mat` を使用、高さ約12〜18mのリアル大ヤシ）
- 蝶: `Assets/Game/Scripts/AdventureButterflyDrift.cs` + Idyllic の Butterfly prefab

## 設定メモ

- 入力: Input System only。Editor 再生時は Game ビューにフォーカスしなくても WASD が来る想定。
- 植生の再配置は Play 停止後に Dress メニュー。北崖・スポーン周辺は空けている。
- スタート地点（スポーン）: 崖中腹を脱出し、西側の大草原のせせらぎ池平原（x=265, y=15.2, z=330）の完全な平坦地に設定。カメラは北東の大草原と池を見渡すアングル。メニュー **Adventure → 📍 Reset Spawn to Meadow Plains (大草原のせせらぎ平原)** で即時反映可能。
- ヤシは Hawaii Beach House アセットの高精細プレハブを使用し、URPで美しく描画されるようマテリアルを設定。
- HUD は Play 開始時に作り直す。レイアウト変更後は一度 ■ してから再生。
- Overlay canvas はカメラ解像度（例: 2560×1440）で組まれ、Game ビュー枠（例: 2031×1464）より広いことがある。端寄せUIは欠ける。中央寄せが安全。
- `AdventureGameDirector` の左上クエスト行も余白を広げてあるが、RustAndFlat シーンでは Director は動いていない。

## まだやっていない（次の候補）

ユーザーは一手ずつ、確認質問は1つ。日本語、結論ファースト。

**次にやること: 砂浜に漂着ゴミ（2030〜2050の年代グラデーション）。** AdventureWorld と `Land_Terrain.asset` は触るな。

1. 【済】北の崖を滑空向きに整える
2. 【済】この島の冒頭セリフを2050にする
3. 【済】上部操作ガイドの左欠け（上中央へ移動）
4. 【済】1000m Grand Island 生成（中央タワー・池・川・大滑空北崖）
5. 【済】白亜の床ワイヤーフレーム青グリッド発光ギミック（シェーダー + スクリプト）
6. 砂浜に漂着ゴミ（2030〜2050の年代グラデーション）
7. Rust の対話・油をアイテム化する
8. フォルダ名 RustAndFlat → RustAndFloat のリネーム（参照切れに注意）

## ブランチ

`refactor/island-map-and-terrain-cleanup`  
リモート: `https://github.com/Yosie-lab/AdventureWorld.git`

コミット方針: ユーザーが commit / push を明示したときだけ。AdventureWorld の地形と Demo/Loader へのビルド差し替えは入れない。
