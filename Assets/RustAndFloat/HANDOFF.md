---
tags: [rust-and-float, handoff]
created: 2026-09-11
---

# RustAndFloat 引き継ぎ

次のエージェント（Antigravity 含む）は、このファイルを最初に読む。作業対象は **RustAndFloat**。元ゲーム **AdventureWorld は壊さない**。

## Antigravity への引き継ぎ（2026-09-25）

**いまの正は `main` の `b71b6db`（2026-09-24、PR #14 マージ）と同じ。** 空の実験は戻してある。未コミットの空用スクリプトは残っていない。

- **空**: レバー後も島と同じ `RustAndFloat/ClearBlueSky`。色は天頂 `(0.01, 0.24, 0.85)`、中間 `(0.05, 0.48, 0.98)`、地平 `(0.40, 0.75, 0.98)`、下 `(0.22, 0.58, 0.90)`。実装は `Assets/Game/Scripts/AdventureSkybreakVisuals.cs` の `ApplySkyboxForSkybreak`。
- **やってはいけない**: `Assets/Resources/Skybreak/` の写真（Poly Haven の HDR を `.png` に改名したもの）を空に戻さない。場面と合わず、ユーザーが却下した。`AdventureSkybreakPanorama.cs` と `SkybreakPanorama.shader` は削除済み。作り直さない。
- **天蓋の外の絵**: 昨日の最後の状態。球の稜線・地平・雲は `SpawnWildernessPanorama` のプリミティブ。写真スカイボックスではない。
- **直近で main に入っているもの**:
  - `761f64e` レバー見た目、青空復帰、歩行と木道
  - `b6fa2d5` 進行を `AdventureStoryFlow` と Canopy / Climax / Epilogue partial に分割
  - `654a00d` カメラの寄りすぎを戻し、歩き出しを軽く、歩行速度 10.2
  - `b71b6db` 体感定数を `AdventureRustFloatFeel.cs` に集約
- **確認**: Play を止めてから再生。F9 で天蓋。空はレバー前と同じ青。
- **コミットしない**: ユーザーが明示するまで commit / push しない。シーンファイルはコミットしない。

## 触ってよい / 触るな

| 触ってよい | 触るな |
|---|---|
| `Assets/RustAndFloat/` | `Assets/Scenes/AdventureWorld.unity` |
| `Assets/Game/Scripts/AdventureRust*.cs`（Opening / Island / Drone） | `Assets/Idyllic Fantasy Nature/Demo/Settings/Land_Terrain.asset` |
| `Assets/Game/Scripts/AdventurePalmFactory.cs` | `Assets/Game/Scripts/AdventurePlayerController.cs` の AdventureWorld 専用挙動を壊す変更 |
| `Assets/Game/Scripts/AdventureButterflyDrift.cs` | Build Settings を Demo/Loader に差し替えること |
| `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs` / Open / Build / Shape North Cliff | 秘密情報・`.env`・巨大な未追跡 `Assets/Art` |

フォルダ名・製品名・メニュー名・シーン名は **RustAndFloat** に正式統一済み。

## 開き方

Unity メニュー: **Adventure → Open RustAndFloat Scene (new island)**

シーン: `Assets/RustAndFloat/Scenes/RustAndFloat.unity`

再生中にシーン保存しない。■ で止めてから保存。

## いまの中身

- **Grand Island（1024×120×1024）**。海面 y=5.5。メニュー **Adventure → 🏝️ Build 1000m Grand Sanctuary Island** で生成。
- **広大な白砂ビーチと透明な楽園の海**: 標高6.0m〜8.5m、島を一周する幅約50mの鮮やかな白砂ビーチテラス。
  - **漂着サバイバル情報ボックス（Drift Boxes）**:
    - 白砂ビーチ5箇所（西・南西・北西・真南・東）にチーク木目＆真鍮バンド＆アンテナシグナルランプ付きのチェストを配置。
    - **コンパスHUDリアルタイムナビゲーション（迷子防止）**:
      - **コンパスリボンマーカー**: 画面上部コンパスHUD上に未開封ボックスの「📦」アイコン（視野外時は「◀📦」「📦▶」）がリアルタイムに追従描画（エメラルドグリーン）。
      - **ナビテキスト同時案内**: コンパス下部に「✦ [最寄りパーツ] 約〇m(...) | 📦 [最寄り漂着ボックス] 約〇m(...)」とパーツとボックスの残存位置・距離を1行で常時案内。
      - **相棒Rustの探知セリフ＆ソナー**: 75m圏内に未開封ボックスがあるとRustがソナー音とともに「ピピッ！〇〇の白砂ビーチに『〇〇』の電波反応だよ！」とおしゃべり。
    - **多層発光システム（遠・中・近の圧倒的視認性）**:
      - **遠景（超高輝度・全高110m二重多層ライトビーコン）**:
        - **超高輝度ホワイトゴールド・コア柱**: 直径0.65m・高さ110m、HDR 4.2x輝度により快晴の青空・太陽光下でも強烈なブルームを放ち、島じゅうの遠方砂浜や大滑空中から一目で位置を特定可能。ボックスの傾きに依存せず垂直天頂へ直立。
        - **広域オーラ光芒（コロナ柱）**: 外周直径2.2m・高さ110m、HDR 2.6xの温かなゴールド光芒がコアを包み、遠景でのピクセル視認性を確保。
        - **高速垂直光粒子ビーム**: 速度26m/s・寿命4.2s・粒子サイズ0.75m・HDR 3.6xで秒間30個の光子が天空110mへ昇り続ける光芒ストリーム。
        - **滑らかなフェードアウト**: 開封時に両光柱およびオーラが0.5秒かけて美しく縮小・フェードアウト消滅。
      - **中景**: アンテナ直近に広域Point Light（Range 24m, Intensity 5.6）および足元砂浜の黄金グラウンドグロー（直径4m）を配置し、周囲の砂浜とチェストをあたたかく照らす（開封後はエメラルドグリーン点灯）。ランプ球体も直径0.75m＋HDR 3.5xソフトグローを付与。
      - **近景**: ボックス周囲を金色の星くずスパークル粒子（HDR 2.5x）とダイヤモンドスター（1秒に4〜5回瞬く星プリズム）が漂う。
    - **接近自動獲得＆総合探索ポイント（+2 pt）**:
      - プレイヤーが近づく（2.8m以内）と自動でパカッと開き、祝祭パーティクル＆効果音とともに総合探索ポイント **+2 pt** を獲得。モーダルUIで先人の記録が表示される。
  - **総合20ポイント制・中央タワーレバーロック解除システム**:
    - **配点**: 漂着パーツ（1pt×12個）＋漂着ボックス（2pt×5箇所）＋カピタのピアノ光る古代遺物（3pt×1箇所）＝最大25pt。
    - **レバー解除**: 総合 **20ポイント以上** 達成で中央タワーの真鍮レバーロックが解除される。
    - **手動操作の徹底（自動開放の防止）**: 20pt達成時はレバーが引ける状態になるのみで、天蓋崩壊シーケンスは自動発動しない。プレイヤーが中央タワーの白亜テラスへ行き、レバーの前で実際に引く（Eキー / Space / クリック）ことで初めて天蓋崩壊シーケンスが発動する。
    - **ピアニスト・カピタと光る古代遺物**: カピタに近づく（3.5m以内）とピアノ上の光る遺物を回収して **3 pt** 獲得（`AdventureAncientPianoRelic.cs`）。
  - **砂浜木道スロープ（Boardwalk Ramp）の段差ゼロ・スタック完全解消**:
    - 砂浜から内陸高台へ登る木道の段差（空中浮遊・潜り込み・スタック）を完全解消。Terrain追従＋厚み80cmコライダー＋`TooSteep`急斜面バイパスにより快適に駆け上がれる。
  - **空中浮遊・滑空（グライド）の操作感**:
    - 航空機のように風に乗って飛ぶ自然な滑空モデル（W=ダイブ加速、S=フレア減速・機首上げ、A/D=バンク旋回）。
    - 空中滑空中に専用操作ガイドUI（`【A / D】旋回 【W】ダイブ 【S】フレア 【Space長押し】滑空`）が動的に表示される。
  - **白砂から海へのスムーズな接続**: 段差のない極めて滑らかな渚（スロープ）を形成。
  - **透明なクリスタルオーシャン（`ParadiseOcean_URP.mat`）**: 水深0の汀線不透明度 `0.005`（0.5%）、浅瀬アルファ `0.06`（94%透明）、深度係数 `0.18`。波打ち際がガラスのように白砂に溶け込み、海底の砂地・小魚がクリアに透き通る南国の海を再現。
  - **浅瀬の生き物・環境音・デコレーション**:
    - 浅瀬を優雅に群れ泳ぐ小魚たち（`AdventureShallowFishSchool.cs`）
    - 白砂を歩くカニ・美しい貝殻・ヤシの木（樹上のココナッツ房）
    - 砂浜を舞うトンボ（`AdventureDragonfly.cs`）や優雅に旋回するウミネコ（`AdventureBeachSeagull.cs`）
    - 寄せては返す波の音（`AdventureLappingWaves.cs`）
  - **Nikoの海中移動・足元接地**:
    - 足元オフセット（`Skin = 0.05f`、コライダー調整）により、白砂の上に靴底がぴったり接地して歩行。
    - 海面歩行ではなく、海の中へ自由に入水可能（水深に応じた浅瀬歩行・水泳・入水波紋エフェクト）。
- **西側の大草原（Grand Meadows）と東部〜北部の大樹海（Deep Ancient Forest）の壮大な共存**:
  - **東部〜北部の広大無辺な大樹海（Deep Forest）**: 島の東側から北部全域（x: 460〜890, z: 180〜750）を占める深緑の原生林。モミの木（Fir 01〜05）や深緑の巨木（スケール1.3〜2.5倍）を中心に **2,200本** の木々が高密度に林立し、林冠が空を覆う深い森の天蓋（密林クラスター）を形成。林床には **3,200本** の灌木やシダが生い茂り、木漏れ日と蝉時雨が響く本格的な深い森を再現。
  - **西側の広大な大草原（Grand Meadows）**: スタート地点（せせらぎ池平原 x=265, z=330）を中心とする西側全域は、木を爽やかな一本杉や木立（4%）に留め、地平線まで突き抜ける大草原の開放感を100%維持。Terrain Detail草（描画距離500m）による波打つ緑の絨毯＋要所のアクセント花畑。シーンサイズを27MBに最適化しGit/GitHubへ完全対応。
  - **二面性の絶景**: スタート地点からは眼前一面に大草原と青空が広がり、東の丘陵に目を向けると雄大な大樹海が広がる、絵画のようなコントラストを実現。
- **清流・大渓流・3つの池（Water & Ravine System）**:
  - **東の深林大渓流（Eastern Mountain Torrent）**: 標高40mの東部高地原生林からカルデラ湖（25.5m）へ急流で下る全長300m・落差約15mの本格的な山岳渓谷（V字谷掘り込み、24セグメント水面）。
    - 渓流の川底・川岸に **450個以上の巨石・苔岩・飛び石** を敷き詰め、白波を立てて縫うように激しく下るダイナミックな渓流美を再現。
    - 岸辺には葦・シダ・ガマなどの水辺植物と、岩陰から響くカジカガエルの美声（4箇所）。
  - **白砂ビーチ前・段々池（Beach Step Ponds / RiverSeg_14〜17）の岩組み美化 & 海上突出解消**:
    - スタート地点（白砂ビーチ・座礁艇前）の目の前に広がる段々水面。
    - 海面（5.5m）の上空（6.8m〜5.6m）に浮いて海底へ黒い四角い影を落としていた最下流セグメント（`RiverSeg_18`, `RiverSeg_19`）を完全非アクティブ化。
    - 最下段 `RiverSeg_17` の位置・長さを陸地（標高6.2〜6.5mの白砂渚）の内側へ収まるよう適正化。
    - 各段の左右両岸に、中型岩（`Stone_Medium_01〜03`, `Rock_Medium_01〜03`）および小型岩（`Rock_Small_01〜03`, `Stones_01〜03`）を水際と土手の2列ジグザグで隙間なく配置し、人工的な直線エッジを自然な岩組みに美化。
    - 段差の落ち口（小滝・堰）に平らな石組みを横一列に並べ、水面中央にも飛び石を配置。
    - 河口先端から砂浜にかけて扇状に丸石・小石を敷き詰め、海への突出と不自然な影を100%解消。
    - メニュー: **Adventure → 🏞️ Beautify Beach Step Ponds (白砂ビーチ前段々池の美化)**（`AdventureBeautifyStepPonds.cs`）
  - **オアシス湧水池（SanctuarySpringPond）**: 中央台地足元（x=480, z=455, 標高48.2m, 半径16m）。天然岩の石組みと睡蓮で周囲を囲み美化済み（メニュー: **Adventure → 🪨 Enclose Ponds with Natural Rocks**）。
  - **上流急流（UpperParadiseStream）**: オアシス湧水池からカルデラ湖へ下るせせらぎ。
  - **大カルデラ湖（CalderaLake）**: x=420, z=440, 標高25.5m, 半径38mの広大な湖。水面一面の睡蓮・蓮の葉、湖畔のカエルの大合唱。
  - **大草原の憩いのせせらぎ池（MeadowLowlandPond）**: 西側の大草原の真ん中（x=290, z=320, 標高14.5m, 半径16m）。天然岩の石組みと睡蓮で周囲を囲み美化済み。
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
- **寄せては返す波の音＆ウミネコ（カモメ）音響システム（`AdventureBeachWavesManager.cs`）**:
  - `AdventurePlayerController` の起動時に自動生成。
  - **スタート直後のファーストコール**: ゲーム開始0.5秒後（砂浜で目を覚ました瞬間）、青空高くから「ミャーッ、クエックエッ…」とウミネコが一羽澄んだ声で鳴き抜け、砂浜漂着の映画的情緒を演出。
  - **海岸アンビエンス**: 海岸線・白砂ビーチにいる間、15〜28秒ごとに上空の異なる位置からウミネコ（単体・群れ）が情緒的に鳴き交わす。
  - **青空を舞うウミネコ群（`AdventureSeagullFlightVisual`）**: スポーン地点（西側砂浜）の頭上26m〜32mの青空を、白い翼を羽ばたかせながらゆったりと優雅に円軌道で旋回するウミネコ群をプロシージャル生成。
  - リマスター波音源 `ocean_waves_grand.wav`（時間差2重クロスブレンド）による迫真の潮騒。内陸に入ると自然に静まり、蝉や虫の声が引き立つ。
- 海に沈まない（`waterLevel` で浮く）。急斜面は歩けず落ちる。
- 相棒 **Rust**: 錆びた球ドローン。遅れ・ヒッチ・きしみ音・熱けむり・油垂れ。
  - きしみ音の音量をほんの少し控えめ（`soundVolume = 0.28f`、従来の約65%）に調整。再生間隔も少し落ち着かせ（1.2〜2.2秒）、nikoの足音や波音・蝉の声に優しく寄り添う音響に設定。
  - **ふんわり丸い煙エフェクト（熱気スモーク）**: プロシージャル生成の円形コサイン減衰テクスチャ（`GetSoftSmokeTexture`）を適用し、不自然な四角形板ポリゴン描画を解消。自然なサイズ（0.32〜0.65m）とフェードイン/アウトで、移動やヒッチ時に頭上からポッポッと上品に立ち上る排気スモークに改善。`WhiteSmoke.shader` の深度テストも `ZTest LEqual` に適正化。
- **生き物（トンボ・カエル）**:
  - **トンボの翅アニメーション適正化（`AdventureParadiseCreatures.cs`）**: `DragonflyFlight` 内で翅のスケールが1m四方に破壊されていた不具合を解消。リアルな羽ばたき角度振動に切り替え、宙を飛ぶ「半透明な四角いボックス」を完全に解消。
- **漂着遺物（古代パーツ）の収集と相棒Rustの機能アップグレード（探索＆成長システム）**:
  - **3D回転ギア・エネルギーコア（`AdventureScrapItem.cs`）**:
    - **天空へ伸びる光の柱（ライトビーコン）**: 各パーツの中心から高さ10mの神々しい光の柱が天空へ立ち上り、遠くからでも一目で位置がわかるように設計。
    - **存在感あるサイズと輝き**: 直径約1.15mの黄金ギア＋常時きらめく星くず光粒子＋高輝度エミッション。草むらに埋もれないよう浮遊高度を1.35mに設定。
    - **最初のギア**: スポーン地点（大草原せせらぎ平原）の正面約6m（北東 270, 334）に配置。ゲーム開始時に画面正面で光り輝き、迷わず発見可能。
    - プレイヤーが3.8m以内に近づくと自動で引き寄せられるスムーズなマグネット機能。
    - 取得時に『脳リフレクソ』の神秘的なウインドチャイム音（C5-B6ペンタトニックスケール＋240msステレオピンポンディレイ残響 `brain_reflexo_chime.wav`）とスパークパーティクルを放つ。
  - **探索マネージャー（`AdventureScrapManager.cs`）**:
    - ゲーム開始時に自動生成（`AdventureScrapManager.Ensure()`）。
    - 収集数（3, 6, 9, 12個）に応じて相棒Rustが機能をアンロック：
      - **3個**: 【ブースター修復】ダッシュ速度が 7.8m/s → 9.2m/s に大幅アップ！
      - **6個**: 【反重力機能修復】空中でSpaceを押すと二段ジャンプが可能に！
      - **9個**: 【探知ソナー修復】相棒Rustが45m以内の未取得遺物をソナー音でナビゲート！
      - **12個**: 【大滑空ブースター展開】滑空時の前進速度と滞空力が大幅強化され、島中を爽快に滑空可能に！
  - **uGUI Canvas常設HUD（`AdventureScrapHUD.cs`）**:
    - `CanvasScaler`（1280×720, ScreenSpaceOverlay, sortingOrder 95）により、Retinaやどのような解像度・Gameビュー枠でも米粒にならず美しくスケーリング。
    - 画面右上に大きくてくっきりとしたカウンター（`⚙ 漂着遺物  0 / 12`）。取得時にはパネルが弾むアニメーション。
    - パーツ取得時には画面中央上部に「✨ [アイテム名] を回収！ (X / 12) ✨」がゴールドに輝いてフェードイン・ポップアップ。
    - 3, 6, 9, 12個達成時には画面上部に大きなゴールドのアップグレード通知バナーを表示。
  - **相棒Rustの豊かなリアクション**:
    - パーツ取得時に嬉しそうにピョンと跳ね上がり、ハッピービープ音とともに頭上に吹き出しセリフを表示。
    - ソナー作動時は未取得パーツの距離に応じて音程と間隔が変化するビープ音でNikoを導く。
- **大滑空ウインドリング＆上昇気流サーマル（大空スカイフライトシステム）**:
  - **風の加速リング（`AdventureWindRing.cs`）**:
    - エメラルドグリーンに輝く円環の気流トンネル（直径約5m）。
    - 滑空中にリングをくぐると「シュバッ！」と爽快な風切りブースト音とともに、前進速度が約1.85倍（15〜16m/s）に急加速！
    - リングは通過時にショックウェーブを放って一時消灯し、3.2秒後に再点灯。
    - 相棒Rustが「ヒューッ！ナイスフライト！」と歓声をあげて一緒に加速。
  - **上昇気流サーマル（`AdventureThermalUpdraft.cs`）**:
    - 谷間や海面、草原池から上空35〜65mへ吹き上がるエメラルドホワイトの温かい風の柱。
    - 滑空中に入るとパラグライダーのように垂直速度+5.2m/sでスルスルと再上昇し、滑空の滞空時間を延ばして長距離空中散歩が可能。
  - **スカイライン配置（`AdventureFlightManager.cs`）**:
    - **コース1: 大草原フライトライン**: スタート地点東の小高い丘から池・西草原へ向かうリング4個＋サーマル1箇所（開始直後すぐに体験可能）。
    - **コース2: 北の大滑空崖 エクストリーム・スカイハイウェイ**: 標高92m崖から海へ向かう6連ウインドリング＋海面の巨大サーマル（海抜6m→高度60mへ一気に吹き上げられて島へ帰還可能）。
    - **コース3: 大渓流キャニオン**: 高地原生林からカルデラ湖へ下る渓谷スカイコース。
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
  - 画面最上部中央にミニマルで上品な水平リボンコンパスHUD（`AdventureCompassHUD.cs`）を常時表示。カメラの旋回に合わせて日本語の方角（北、北東、東、南東、南、南西、西、北西）と度数が滑らかに追従し、北はシアン色で視認性抜群。邪魔にならない半透明デザイン。
  - 操作ガイドはコンパスの下（y = -44f）に `【WASD】移動　【マウス / 矢印キー】視点　【Space長押し】崖から滑空　【R】リセット` と表示。

## 主要ファイル

- シーン: `Assets/RustAndFloat/Scenes/RustAndFloat.unity`
- 地形: `Assets/RustAndFloat/Terrain/IslandTerrain.asset`
- Rust: `Assets/RustAndFloat/Prefabs/Rust.prefab` / `Assets/Game/Scripts/AdventureRustDrone.cs`
- 島バウンド: `Assets/Game/Scripts/AdventureRustFloatIsland.cs`（`DefaultExecutionOrder(-300)`）
- 冒頭HUD: `Assets/Game/Scripts/AdventureRustFloatOpening.cs`（ランタイム生成、シーンには無い）
- 滑空: `Assets/Game/Scripts/AdventurePlayerController.cs`
- Boot スキップ: `Assets/Game/Scripts/AdventureWorldBoot.cs` が `RustAndFloat` なら early-return（これを外すと海面 `OceanPlane` が消える）
- 楽園配置: `Assets/Game/Scripts/Editor/AdventureDressRustFloatParadise.cs`（メニュー Adventure → Dress RustAndFloat Paradise）
- 島生成: `Assets/Game/Scripts/Editor/AdventureBuildRustFloatIsland.cs`（メニュー Adventure → 🏝️ Build 1000m Grand Sanctuary Island / Build RustAndFloat Island (256m) / Shape RustAndFloat North Cliff）
- グリッド床シェーダー: `Assets/RustAndFloat/Shaders/SanctuaryGridGlow.shader`
- グリッド床マテリアル: `Assets/RustAndFloat/Materials/SanctuaryGridGlow.mat`
- グリッド床スクリプト: `Assets/Game/Scripts/AdventureSanctuaryGridGlow.cs`（Play中、プレイヤー接近で発光）
- ヤシ生成: `Assets/Game/Scripts/AdventurePalmFactory.cs`（Hawaii Beach House アセットの高精細ヤシの木モデル + URP両面マテリアル `Assets/RustAndFloat/Materials/HawaiiPalm_URP.mat` を使用、高さ約12〜18mのリアル大ヤシ）
- 蝶: `Assets/Game/Scripts/AdventureButterflyDrift.cs` + Idyllic の Butterfly prefab
- 白砂ビーチ前段々池美化: `Assets/Game/Scripts/Editor/AdventureBeautifyStepPonds.cs`（メニュー: Adventure → 🏞️ Beautify Beach Step Ponds）
- 内陸池岩囲み美化: `Assets/Game/Scripts/Editor/AdventureEnclosePondsWithRocks.cs`（メニュー: Adventure → 🪨 Enclose Ponds with Natural Rocks）
- 全池水面質感・水底土肌・水草美化: `Assets/Game/Scripts/Editor/AdventureBeautifyAllPondsWater.cs`（メニュー: Adventure → 💧 Beautify All Ponds & Streams Water）
- 専用水面URPマテリアル: `Assets/RustAndFloat/Materials/PondWater_URP.mat`（`Water.shadergraph`、エメラルド〜セルリアン水深フェード、波紋法線、水際ソフト白泡）

## 設定メモ

- 入力: Input System only。Editor 再生時は Game ビューにフォーカスしなくても WASD が来る想定。
- 植生の再配置は Play 停止後に Dress メニュー。北崖・スポーン周辺は空けている。
- スタート地点（スポーン）: **西側白砂ビーチの座礁脱出艇前（x=158, y=6.5, z=275, 方位75度）**。目の前に大破した二人の脱出艇と最初の黄金ギア、波打ち際、焚き火キャンプ跡が広がる。メニュー **Adventure → 🏝️ Reset Spawn to West Beach (西側白砂ビーチ・座礁艇前)** で即時反映可能。
- **海岸から内陸へ登る12個パーツプログレッション**:
  - **海岸・波打ち際（パーツ1〜3）**: 座礁艇脇、波打ち際、焚き火キャンプ跡。3個回収で【ダッシュ】解禁。大草原へ駆け上がれる。
  - **西側大草原（パーツ4〜6）**: 草原の古木、大河の飛び石、せせらぎ池畔。6個回収で【二段ジャンプ】解禁。カルデラ湖の段差を突破可能。
  - **カルデラ湖・東部大樹海（パーツ7〜9）**: カルデラ湖の浮島岩、大渓流の滝壺、大樹海の巨木。9個回収で【探知ソナー】解禁。
  - **高地・中央タワー（パーツ10〜12）**: 湧水オアシス、北の大滑空崖、中央白亜タワー頂上。12個回収で【大滑空ブースター】解禁。
- **砂浜ナラティブ＆外の社会の追体験（`AdventureBeachNarrativeManager.cs`）**:
  - 二人の座礁脱出艇（破片と火花）、初日の焚き火キャンプ跡。
  - 外のAI管理社会の記憶チップ（全5箇所: 逃亡直前の通信ログ、生体監視網、完全自動化都市の追憶など）。
  - 先人のメッセージボトル（全3箇所: 「ここには監視もスコアもない。ただ風と海があるだけだ」など）。
- **セーブデータ初期化（ニューゲーム機能）**:
  - いつでも砂浜からパーツ0個でやり直せるよう、【F8キー】または Unityメニュー **Adventure → 🗑️ Delete Save Data (セーブ初期化・砂浜0個スタート)** で完全リセット可能。
- ヤシは Hawaii Beach House アセットの高精細プレハブを使用し、URPで美しく描画されるようマテリアルを設定。
- HUD は Play 開始時に作り直す。レイアウト変更後は一度 ■ してから再生。
- Overlay canvas はカメラ解像度（例: 2560×1440）で組まれ、Game ビュー枠（例: 2031×1464）より広いことがある。端寄せUIは欠ける。中央寄せが安全。
- **作品のコア思想（『A Short Hike』×『Outer Wilds』×『巨像・トリコ』）**:
  - **テーマ**: 効率へのアンチテーゼ、無駄の美しさ、非言語の相棒愛、減算法のループ（HUDが削ぎ落とされ世界の解像度が上がる）。
  - **滑空（Float）**: 初速ロケット急加速を廃止。風のうねり（気流）に乗った時にだけフワリと浮く重力と浮力のオーガニックな物理挙動。
  - **相棒愛（Rust）**: 煩わしくも愛おしい重み、手当て、段差の助け合い。
- **オーガニック滑空＆気流フライトシステム（`AdventurePlayerController` / `AdventureWindRing` / `AdventureThermalUpdraft`）**:
  - **心地よい『A Short Hike』スタイル滑空操作**:
    - **A / D キー**: ダイレクトな機首ヨー旋回（毎秒145度でクイック旋回）＋左右バンク角（-22°〜+22°の自然な傾き）。大空を360度自由自在に旋回・旋回半径コントロール可能。
    - **W キー（ダイブ加速）**: 機首を下げて急降下加速（通常7.4m/s → 11.5m/s、降下速度 -2.8m/s）。
    - **S キー（滞空フレア）**: 機首を上げて滞空ブレーキ（通常7.4m/s → 4.6m/s、降下速度 -0.28m/s でふわりと長時間滞空）。
    - **キーを離した巡航**: 心地よい安定滑空（7.4m/s、降下 -1.15m/s）。
    - **着地時の姿勢自動リセット**: 滑空から接地した瞬間にピッチ・ロールを自動で水平姿勢に補正。
  - **相棒Rustの穏やかな語りかけ（対話システム）**:
    - **映画字幕・特大ダイアログウィンドウ**: Retina・大画面環境でも絶対に小さくならない特大フォント（本文26〜44pt、太字）。ネームタグ「✦ 相棒 Rust」＋本文「……」の2行レイアウト、エメラルド光彩アクセントバー、4方向黒アウトラインにより、眩しい青空・白砂浜・上空でも圧倒的な視認性と美しさを誇る。
    - **滑空開始時トリガー**: スペースキー長押しでフワリと浮いた瞬間に「わぁ…！風が気持ちいいね、Niko」「ふわりと浮いたよ…！」と語りかける。
    - **気流搭乗時トリガー**: 風のリング通過時にも穏やかに語りかける。
    - **探索・アイドルつぶやき**: 探索中・滑空中に定期的（30〜50秒）に穏やかなセリフをつぶやく。
  - **気流・風のリング（WindRing）**: 通過すると「サァァー…ン」と澄んだ風のそよぎとチャイム音が響き、気流を孕んでフワリと持ち上がり（高度+3.6m）、心地よい追い風クルージング（11.0m/s、Wダイブ時15.0m/s、持続2.8秒）で大空をスイスイ飛べる。
  - **上昇気流サーマル（ThermalUpdraft）**: 上昇風でパラグライダーのように高度がスルスル再上昇（+4.5m/s）。
  - **カメラ演出＆ダイレクト・マウス視点（`AdventureCameraFollow.cs`）**:
    - **マウス回転は遅延ゼロのダイレクト追従**: 回転のDamping遅延（ゴムを引っ張るような重さ・引っ掛かり感）を完全撤廃し、感度を自然な0.16fへ調整。プレイヤーの指先の動きに1対1でピタッと忠実に反応。
    - **クリックでの確実なカーソル復帰**: Gameビューをクリックすると瞬時にCursorLockが有効化され、操作が確実に反映される設計。
    - **位置のダンピング（段差・揺れの吸収）**: プレイヤーの足踏みや段差ショックだけを滑らかに吸収し、画面酔いを防止。
    - **障害物検知の滑らかな伸縮**: 壁際での急激な伸縮ワープを防止し、0.06秒でスッと滑らかに伸縮。
    - **滑空オートフォロー**: マウス操作後1.2秒以上経過している場合のみ、機首方向へ優美に寄り添うアシスト。
  - **コース配置**: スタート地点正面（273, 50.39, 336、地上高さ1.4m）に第1リング。走るだけでど真ん中を貫通可能。北の大滑空崖（標高92m〜海面の6連リング＆海上大サーマル）、大渓流〜カルデラ湖。
- **漂着パーツ収集 & Rustアップグレード機能（先行実装・完了済み）**:
  - `AdventureScrapItem.cs`: 黄金に輝く3Dギア＋高さ10mのライトビーコン＋常時星くずパーティクル。
  - `AdventureScrapManager.cs`: 12箇所のパーツ配置とアップグレード管理（3個:ダッシュ、6個:二段ジャンプ、9個:ソナー、12個:大滑空）。
  - `AdventureScrapHUD.cs`: uGUI Canvasによるスマート・オートハイド（自動消去）カウンター。
  - `AdventureRustDrone.cs`: プロシージャル円形スモークテクスチャ（四角い板ポリゴン煙の解消）、セリフ吹き出し、ソナー音。
  - `AdventureParadiseCreatures.cs`: トンボの翅が1m四方の半透明四角形に変形して宙を飛ぶスケール破壊バグを完全解消。

- **砂浜の漂着ゴミ・文明年代グラデーション（`AdventureBeachFlotsam.cs` / `AdventureBeachFlotsamManager.cs`）**:
  - 外周白砂ビーチテラス（標高6.0〜8.5m）に散らばる、2030〜2050年代の人類文明の地層を巡るナラティブギミック。
  - **年代グラデーションの配置**:
    - **波打ち際（2050年代・最新AI管理社会）**: 『AIドローンの破損プロペラ』『生体追跡IDリング』『生分解性AIカプセル』『ナノ結晶バッテリー』。
    - **砂浜中央（2040年代・管理社会全盛期）**: 『AI健康最適化バンド』『規格化合成樹脂ボトル』『旧型物流ロボットの摩耗ギア』。
    - **砂浜奥・草むら境界（2030年代・前時代アナログ）**: 『ひび割れた旧世代スマートフォン』『有線イヤホンの残骸』『磁気カセットテープ』『手巻き機械式時計の文字盤』。
  - **インタラクション**:
    - 近づくと「【E】調べる」が出現。Eキーを押すと砂を払うアコースティック効果音と共に、相棒Rustが特大ダイアログで感慨深く語りかけ、上部HUDに遺物の名称・年代・解説が表示される。

- **Rustの油アイテム化＆手当て・対話システム（`AdventureRustDrone.cs` / `AdventureRustOilDrop.cs`）**:
  - **油（潤滑油）のアイテム化**:
    - Rustがヒッチ（ガタつき）で漏らした油滴が地面に「黒光りする油溜まり」として残留。Nikoが近づいて拾うと「✦ 潤滑油を採取した（所持数ストック）」となりアイテム化される。
  - **Rustの手当て（オイル補給）インタラクション**:
    - Rustがオーバーヒートやヒッチで煙を吹いている際、近づいて【Eキー】を押すと油を塗って手当てできる。
    - 煙がスーッと消え、Rustが嬉しそうに宙返りジャンプし「わぁ…！ありがとうNiko、身体がすごく軽くなったよ…！」と感謝。
    - 手当て後80秒間はヒッチが発生せず、軽快に機敏追従する好調状態（画面右上に「✦ 良好（整備済）」バッジ表示）。
  - **Rustとの直接対話**:
    - 通常時に近づいて【Eキー】を押すと、場所（砂浜・高い崖・草原）やパーツ収集状況に応じたあたたかい対話をしてくれる。

- **天蓋開放シークエンス & エンディング基盤実装（`AdventureSanctuaryTowerManager.cs`）**:
  - **白亜の古代階段**: オアシス湧水池（480, 455）からタワー台地（512, 512, 標高52m）へ登る白亜の階段道を整備。
  - **天蓋開放レバー**: 台地上に操作レバーを設置。12個のパーツを集めるとロックが解除され、Eキーまたはクリックで天蓋破壊シークエンスが発動。
  - **天蓋破壊演出**: 地鳴り、火花、衝撃波とともに空の天蓋が音を立てて砕け散り、天空に巨大な割れ目が出現。
  - **ハイパー光柱上昇気流**: タワー中心に天空へ突き抜ける光柱（巨大サーマル）が出現。中心に入るだけで自動的に高度105mまで射出され滑空へ移行。
  - **空中Rust凍結危機シークエンス**: 天蓋直下でスローモーション（0.35倍）になり、Rustが極寒気流で機能停止寸前に。「【E または クリック長押し】最後の常備油を注ぐ」でRustを救出。
  - **オーバードライブ突破**: 魂の再点火！蒼いロケット噴射とともに高度150mの天蓋の割れ目を突き抜ける大跳躍。
  - **エピローグ字幕**: 割れ目の外側の山脈シルエットと光芒、映画のような特大字幕（フルHDでタイトル56pt、本文36pt）。
  - **ゲームクリア達成モーダル**: 『✦ GAME CLEAR ✦』モーダル（リプレイ、フリーフライト、クリア記録永続化 `IsGameCleared`）。

## 直近の完了作業（Antigravityセッション 2026-09-18）

1. **白砂ビーチ前・段々池（RiverSeg_14〜17）の岩組み美化 & 海上突き出し・影の完全解消**:
   - **海上の青い板・影の根本原因**: `RiverSeg_17` の後半から海底（標高2.2m）へ地形が急落しており、下流の `RiverSeg_18`（標高6.85m）と `RiverSeg_19`（標高5.6m）が海面（標高5.5m）の上空に浮いて海面に黒い四角い影を落としていた。
   - **解消措置**:
     - `RiverSeg_18`, `RiverSeg_19` を完全に非アクティブ化（`SetActive(false)`）。
     - 最下段 `RiverSeg_17` を陸地の波打ち際内側（標高6.2〜6.5mの白砂渚）に収まるよう位置・長さを適正化（`pos=(196, 7.95, 195)`, `scale=(1.5, 1.0, 0.65)`）。
     - 各段（`RiverSeg_14〜17`）の両岸に、中型岩（`Stone_Medium_01〜03`, `Rock_Medium_01〜03`）と小型岩（`Rock_Small_01〜03`, `Stones_01〜03`）を水際と土手の2列ジグザグで隙間なく配置し、人工的な直線エッジを自然な岩組みに美化。
     - 段差の落ち口（小滝・堰）に平らな石組みを横一列に並べ、水面中央にも飛び石を配置。
     - 最下流の河口から白砂ビーチにかけて扇状に丸石・小石を敷き詰め、海への突出と不自然な影を100%解消。
   - **エディタ拡張**: `Assets/Game/Scripts/Editor/AdventureBeautifyStepPonds.cs`（メニュー: **Adventure → 🏞️ Beautify Beach Step Ponds**）

2. **内陸2池（オアシス湧水池・大草原せせらぎ池）の岩囲み美化**:
   - 水面の円柱側面露出を天然岩（`Stone_Big`, `Stone_Medium`, `Rock_Medium`）と睡蓮（Water Lily）で囲み、自然な池に美化。
   - **エディタ拡張**: `Assets/Game/Scripts/Editor/AdventureEnclosePondsWithRocks.cs`（メニュー: **Adventure → 🪨 Enclose Ponds with Natural Rocks**）

3. **全ての池・小川の水面質感刷新・フラット水面ディスク・水底土肌ペイント・水草美化（完了）**:
   - **水面マテリアル刷新**: プロジェクト内の高品質 URP `Water.shadergraph` を活用した専用マテリアル `PondWater_URP.mat` を作成。水深グラデーション（浅瀬のエメラルド〜深水のセルリアン）、波紋法線スクロール、水際ソフト白泡（Foam）、フレネル反射を実装。
   - **水面メッシュ適正化**: 従来の円柱（Cylinder）メッシュは厚みがあるため半透明シェーダー適用時に側面や底面が透けて黒ずみ・影の二重描画が発生していた問題を解消。上面のみを滑らかに描画する64分割「円形平面（Flat Circular Disc）メッシュ」を自動生成して差し替え。コライダー・影落とし・受影を適正化。
   - **水底土肌・砂地ペイント（Terrain AlphaMap）**: 水深0m〜3mの池底および浅瀬の Terrain Alphamap に泥・土肌・砂利（`Dirt_Stone_Layer` / `SandLayer`）をペイント。水面下に草が生えず、水底が美しく透き通るリアルな水辺を表現。
   - **浅瀬の水草・葦・ガマ・睡蓮配置**: `Reeds`（葦）、`Cattail`（ガマ）、`Waterlily`（睡蓮の花と葉）を岸辺の岩陰や浅瀬に自然なスケールと向きで群生配置。
   - **シーン保存完了**: Edit Mode にて `Assets/RustAndFloat/Scenes/RustAndFloat.unity` に確実に保存済み。
   - **エディタ拡張**: `Assets/Game/Scripts/Editor/AdventureBeautifyAllPondsWater.cs`（メニュー: **Adventure → 💧 Beautify All Ponds & Streams Water**）

4. **Cursor・Antigravity プロジェクト同期 & Git 状況**:
   - Cursorワークスペース: `/Users/user/Unity project/Unity project`（Gitリポジトリルート）
   - Antigravity作業ディレクトリ: `/Users/user/Unity project/RustAndFloat`
   - 両プロジェクト間で `Assets/RustAndFloat/` および `Assets/Game/Scripts/` を完全同期（rsync）。
   - GitHub リモート: `origin/main` (`https://github.com/Yosie-lab/AdventureWorld.git`) へコミット・プッシュ済み。

## 次の作業（Cursorへの引き継ぎタスク）

**【最優先】エンディング演出・ビジュアル・モーション・字幕の全体ブラッシュアップ計画**

エンディングへ向けた一連のシークエンス（天蓋開放 → 光柱ダイブ → 空中Rust凍結危機・注油 → オーバードライブ突破 → 映画的エピローグ字幕 → ゲームクリア）について、散らかっていた画面UI・カメラワーク・エフェクト・セリフ送りを整理し、**「息をのむような美しさと感動の高揚感」** を生み出すためのブラッシュアップを実施する。

### 1. シネマティックカメラ＆空中滑空の安定化
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`, `Assets/Game/Scripts/AdventurePlayerController.cs`, `Assets/Game/Scripts/AdventureCameraFollow.cs`
- **【済・Cursor】**:
  - クライマックス突入時に `AdventureCameraFollow.SetCinematicMode(true)`（後方5.0m・高さ1.6m・FOV70°へブレンド、最低距離3.2mで後頭部ドアップ回避）。
  - 危機〜エピローグで `AdventurePlayerController.SetAutoGlideMode(true, 120)`（高度115〜125mを水平旋回、手離れでも墜落しない）。
  - クリアモーダル「閉じる」でオートグライド／シネマ解除。リダイブでは再有効化。
- **残課題（次へ）**: 2〜4（Rust連動モーション、光芒・フラッシュ、字幕3幕／HUD非表示）

### 2. Rust & Nikoのドラマチック連動モーション
- **対象ファイル**: `Assets/Game/Scripts/AdventureRustDrone.cs`
- **【済・Cursor】**:
  - **危機時**: 胸元にしがみつき＋激しいShake、凍結スモーク＋放電火花、瞳ライト減衰。
  - **注油時**: ふわり浮遊、黄金治癒オーラ＋星くず、瞳ライト／ポイントライト復活。
  - **オーバードライブ**: 前上方へ先導、蒼いジェット＋TrailRenderer軌跡、シアン発光。
- **残課題（次へ）**: 3〜4（光芒・フラッシュ、字幕3幕／HUD非表示）

### 3. 背景ビジュアル・光芒・天蓋突破フラッシュ
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs` (`SpawnWildernessPanorama`)
- **課題**: 天蓋の向こうの外の世界や光芒がシンプルな円柱で、解放感が薄い。
- **改善仕様**:
  - 天蓋の割れ目の外側に、朝焼け〜黄金に輝く地平線と雄大な山脈グラデーション、雲海（浮かぶ雲の層）を配置。
  - 天蓋から差し込む光芒（God Rays）を半透明・加算ブレンド風の柔らかい光柱にし、周囲に金色の光の粒子（解放の光粉）を漂わせる。
  - 天蓋突破の瞬間に、画面全体が一瞬ホワイトアウト（金色の全画面グローフラッシュ）して、暗い箱庭ドーム内から眩しい外の世界へ出た瞬間の感動（露出のコントラスト）を表現。

### 4. セリフ & 字幕の整理（映画字幕の3幕構成）
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`
- **【済・Cursor・テロップ修正】**:
  - 長文一括＋Bold／多重シャドウを廃止。Hiragino 細字＋下方向のみのソフトシャドウ。
  - 下帯付近に1フレーズずつ表示する3幕構成（欠け防止・余韻フェード）。
  - クライマックス〜エピローグ中はコンパス／スクラップHUD／操作ガイドを非表示。
### 5. 古びたピアノと光る遺物・ピアニストカピタの実装
- **対象ファイル**: `Assets/Game/Scripts/AdventureAncientPianoRelic.cs`, `Assets/RustAndFloat/Scenes/RustAndFloat.unity`
- **内容**:
  - 島じゅうに流れていた優しいフェルトピアノBGM（島のBGMの源）の設定を具体化。
  - プロシージャル造形のアンティーク・グランドピアノ、光る古代遺物（エメラルドシアンに輝くクリスタル＆オーブ、オーラパーティクル）を実装。
  - 丸椅子（Stool）にちょこんと座って鍵盤を奏でる「ピアニスト・カピタ」を実装。リズムスウェイ・頭上音符パーティクル・Eキー連弾インタラクション・相棒Rustのセリフ対応。
  - ゲーム開始／探索ごとに島内5箇所の絶景・秘境候補地から毎回ランダムに出現。

### 6. サバイバルケース（Drift Box）ボード閉じる不具合解消
- **対象ファイル**: `Assets/Game/Scripts/AdventureBeachDriftBox.cs`
- **内容**: 新Input System環境下での旧Input呼び出し例外を解消、全画面透明オーバーレイボタン配置によるワンクリック閉じる機能、ESC/Spaceキー、カーソルロック解除を完全両立。

### 7. 台地でのNiko歩行不具合（地面ごと揺れる現象）の完全解消
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`, `Assets/Game/Scripts/AdventurePlayerController.cs`, `Assets/Game/Scripts/AdventureCameraFollow.cs`
- **内容**:
  - わずか2cmの足元沈み込みで毎フレーム5cm空中テレポート引き上げを起こしていたループを完全根絶。
  - 70m×70mの巨大直方体BoxColliderおよび重複コライダーを撤去し、白大理石テラスに正確な円形MeshColliderを適用。
  - StepOffsetGroundを1.35mから0.45mへ適正化。
  - カメラのSphereCastによるテラス床面誤検知防止および歩行時ピボット垂直スムージング時間を0.085fに最適化し、画面・地面の激震を完全に解消。

### 8. ゲームクリア後【N】はじめから再スタート時の全ポイント0化＆HUD・ビーコン完全初期化復元
- **対象ファイル**: `Assets/Game/Scripts/AdventureSaveManager.cs`, `Assets/Game/Scripts/AdventureScrapManager.cs`, `Assets/Game/Scripts/AdventureScrapHUD.cs`, `Assets/Game/Scripts/AdventureBeachDriftBox.cs`
- **内容**:
  - ゲームクリア後モーダルで【N】はじめからを選択した際、漂着パーツ・ドリフトボックス・古代遺物を含めた全探索ポイントを0pt（0/12、0/5、0/20pt）へ完全初期化。
  - セーブファイル（`.bak`含む）の完全削除、PlayerPrefsキーの完全消去、漂着ボックスの未開封化とビーコン復元、ScrapHUDの初期状態（0/3個・計0/20pt目標表示）への完全同期を実施。
  - `AdventureScrapHUD.RefreshQuestDisplay()` において、リセット時にキャッシュガードが旧カウントを維持しないよう0への完全同期に対応。

### 9. レバー操作キーのEキー／クリック専用化（ジャンプSpace誤爆の防止）
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`
- **内容**:
  - レバーの前でジャンプしようとしてSpaceキーを押した際に意図せず天蓋開放シークエンスが発動・即座にダイブ完了して空中浮遊になってしまう事故を防止。
  - レバー操作判定（タップおよび長押し判定）からSpaceキーを除外し、【Eキー】・画面プロンプトクリック・Enterキー・ゲームパッド専用に変更。プロンプトUIも「【ここを押す / E】巨大真鍮レバーを引く」に更新。

### 10. タワー（中央オベリスク）へのNiko・Rustの埋まり込み＆すり抜け防止
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`, `Assets/Game/Scripts/AdventureRustDrone.cs`
- **内容**:
  - 中央タワー本体（`CentralMonolith`）のコライダーが光の柱演出や初期化の影響で消失・無効化され、Nikoが内部をすり抜けて埋まってしまう不具合を修正。天蓋開放前は強固なコライダーを常時維持するよう保証。
  - Rustがタワー先導時や追従時にタワー中心へ直進してオベリスク内部にめり込まないよう、タワー接近時（14m以内）に先導からNiko肩追従へ自動切り替えし、かつタワー中心（半径5.2m以内）への進入防止クランプ処理を追加。

### 11. 崩壊シーケンスBGM再生と古代ピアノ演奏・ダッキングの完全フェードアウト停止
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`, `Assets/Game/Scripts/AdventureAncientPianoRelic.cs`
- **内容**:
  - **天蓋崩壊シーケンスBGM再生**: レバーを引いた直後の `BeginCanopyScriptBeats()` で誤って探索用アンビエントBGM復帰（`RestoreExplorationTheme()`）が呼ばれていたため、天空突破テーマBGMが流れない状態だった問題を解消。`KeepEndingThemeActive()` を呼び出し、壮大な天空テーマ（`_skybreakThemeClip`）を確実に開始・維持するように修正。
  - **ピアノ演奏・ダッキングの確実な停止**: シーケンス開始時に `AdventureAncientPianoRelic` へ `_isSilencedForEnding` フラグをセットし、Updateでの毎フレームダッキング再計算および接近演奏判定を完全にブロック。同時にスポットダッキングを即座に0解除し、2秒間のオーディオフェードアウト＆`src.Stop()` を実行。シーン内に存在する全ピアノインスタンスを確実にサイレント化。ニューゲーム時には適切にフラグをリセット。

### 12. 砂浜のウミネコ（カモメ）モデル・羽ばたき滑空モーションの鳥らしい造形への刷新
- **対象ファイル**: `Assets/Game/Scripts/AdventureBeachSeagull.cs`
- **内容**:
  - 砂浜から空へ飛び立つオブジェクトが四角いブロック（Cube）のままに見えていた問題を解消。
  - プロシージャルメッシュ（先端の尖った円錐クチバシメッシュ `ProcBeakCone`、厚みから先端へ細く伸びる流線型翼メッシュ `ProcBirdWing`、尾羽 `Tail`）を動的に自動生成・換装。
  - 胴体・頭部のスケールバランスをリアルな海鳥の紡錘形プロポーションに最適化。
  - 飛翔モーションを刷新し、離陸直後の力強い羽ばたきから、上昇後の自然な滑空（グライディング）と微細な風揺れ、旋回飛行を実装。

### 13. スタートからエンドまでの包括的リファクタリング（全フェーズ完了）
- **対象ファイル**:
  - `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs` (Phase 1)
  - `Assets/Game/Scripts/AdventureRustDrone.cs` (Phase 2)
  - `Assets/Game/Scripts/AdventurePlayerController.cs` (Phase 3)
  - `Assets/Game/Scripts/AdventureScrapManager.cs` (Phase 4)
  - `Assets/Game/Scripts/AdventureGameDirector.cs` (Phase 5)
  - `Assets/Game/Scripts/AdventureMusicDirector.cs`, `AdventureSaveManager.cs`, `AdventureScrapHUD.cs` (Phase 6)
- **内容**:
  - **毎フレーム検索の徹底排除**: `Update` 内で毎フレーム実行されていた `FindAnyObjectByType`（オープニング、PlayerController、RustDrone等）および 5 回連続の `PlayerPrefs` クエリをすべてキャッシュ参照へ移行。
  - **コード構造と#region整理**: クラス内の役割（定数、内部状態、物理、入力、UI、演出）ごとに一貫した `#region` を配置。
  - **冗長コード・GC負荷の解消**: レガシー未使​​用コード（旧IMGUI描画など）の完全削除、8体分の個別トークカウンタを辞書＋共通メソッドへ集約、HUDの無駄なDestroy/再生成を排除して安定したシングルトン保持型へ改善。

### 14. Nikoが歩いてタワーの台座に乗れるようにする改修（完了・コミット `8585448`）
- **対象ファイル**: `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`
- **背景**:
  - 中央タワー周辺（アプローチ階段、テラス外周、レバー台座）に急な段差や引っ掛かりがあり、ジャンプなしでスムーズに歩行・登頂できない問題があった。
  - `CharacterController.stepOffset` は `0.45m`（StepOffsetGround）のため、0.45mを超える垂直段差は乗り越えられない。
- **実装内容**:
  1. **テラス外周エントリー斜面（`BuildPodiumEntryRamps`）**:
     - `FixPodiumColliders()` 末尾から呼び出し。
     - 地面（標高62m）から白大理石テラス（標高63m）の1m段差を解消するため、北・東・西の3方向に幅8m・長さ5m・厚み0.8mの傾斜BoxColliderを自動配置。
  2. **レバー台座アプローチステップ（`BuildLeverApproachSteps`）**:
     - `CreateLeverStation()` 末尾から呼び出し（`isMain == true` 時）。
     - テラス（63m）からレバー台座上面（64.02m）へ南側から登れるよう、白大理石ステップ3段（段差約0.34m）＋傾斜BoxCollider（`LeverStepRampCollider`）を生成。
  3. **古代アプローチ階段のスムーズスロープコライダー（`AddStairsRampColliders`）**:
     - `BuildTowerStairs()` 末尾から呼び出し。
     - オアシス湧水池から台地への階段全5区間を、厚み0.8mの傾斜BoxColliderでカバー。BoardwalkRamp方式で1段0.7mの引っ掛かりを完全解消。
  4. **`GetTerraceSurfaceY()` の補間範囲拡張**:
     - 北・東・西スロープおよびレバーステップの座標範囲（Z: 490〜501.5等）で正確な補間Y値を返却するよう拡張。
     - `AdventurePlayerController.SurfaceY()` がこの値を参照することで、足元の吸着・接地判定（`isGrounded`）を強固に維持。
- **既知の軽微な注意点**:
  - オアシス湧水池付近（階段最下部 `StairRamp_0〜1`）に `SanctuarySpringPond_Rocks` の岩（`Rock_Medium_01` 等）が一部コライダーと重なっており、完全な歩行には岩を飛び越えるか、岩コライダーの `isTrigger = true` 化が推奨される。

## 開発上の注意（Cursorエージェントへ）

- **絶対厳守ルール**:
  - `Assets/Scenes/AdventureWorld.unity` と `Land_Terrain.asset` は絶対に変更禁止。
  - シーンファイル（`RustAndFloat.unity` 等）はGit LFS/100MB制限があるためコミット・プッシュしない（スクリプト修正のみで実現する）。
  - 言語: 日本語、結論先出し、1ステップずつ、確認質問1つ。
- **Unity Play Mode**:
  - 現在はPlay Modeを停止済み（`isPlaying: false`）。作業確認時はPlay Modeを開始し、完了後は停止すること。
- **天蓋開放〜エンディングの確認ショートカット**:
  - Play中に **F9** → タワー台地へワープし、天蓋開放シークエンスを最初から再生。
  - またはメニュー **Adventure → ▶ Jump to Canopy Opening (天蓋開放から確認)**（未Playなら自動でPlayして起動）。
  - 流れ: 天蓋破壊ボード → Spaceでダイブ → 光柱で上昇 → 高度105mでRust危機・注油 → オーバードライブ → 3幕テロップ → GAME CLEAR。
- **セーブデータリセット**:
  - 探索リセットは **F8** または メニュー **Adventure → 🗑️ Delete Save Data**。

## 直近の完了作業（Antigravityセッション 2026-09-25）

### 物語・エンディングの感動強化（クライマックス演出・映画字幕・ドローンショット）
1. **クライマックス（凍結危機〜注油〜オーバードライブ突破）のドラマ・手触り強化**:
   - `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`, `AdventureSanctuaryTowerManager.Climax.cs`
   - 「警告」タイトルを廃止し、天蓋直下の凍てつく突風とRustのギアの悲鳴、Nikoの必死な掛け合いへとドラマチックに刷新。
   - 注油UIの進行度グラデーション（冷たいシアン〜温かい黄金）、長押し中の心拍パルス脈動演出、注油完了時のカメラシェイク＆温かい黄金ソフトグロー演出を追加。
   - オーバードライブ突入時のカメラシェイクと滑空推進ブーストを強化。
2. **映画字幕3幕構成のタイポグラフィ・滑らかなイージング・余韻の極上化**:
   - `Assets/Game/Scripts/AdventureSanctuaryTowerManager.Epilogue.cs`
   - 第1幕（真実と優しい風）、第2幕（最適へのアンチテーゼと躓きの美しさ）、第3幕（二人の旅立ちとタイトルコール）の詩的テキストにブラッシュアップ。
   - `Mathf.SmoothStep` による極上フェードイン/アウト、微細Ken Burnsスケールドリフト（0.985→1.0→1.025）、幕間余韻1.35秒、ソフトシャドウによる視認性向上。
3. **エピローグ中のシネマティック・ドリフトカメラ（ドローンショット構図）**:
   - `Assets/Game/Scripts/AdventureCameraFollow.cs`
   - オートグライド＋シネマティック時に、斜め後方（+14度）からゆっくり揺らぐドリフト旋回、カメラ距離（7.8m）と高さ（1.95m）、FOV（76°）を適用し、大空を飛ぶ二人と眼下の島を雄大に捉えるドローン構図を実現。
4. **クライマックス最終セリフの表示時間確保と誤スキップ防止（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureSanctuaryTowerManager.Climax.cs`
   - 「ピピッ！……ありがとう、Niko！これで僕たちの翼は折れることはないよ！　大空の向こうまで、全力で行こう！！」が4.5秒の保険タイマーや直前の入力残り（0.85秒）ですぐに消えてシネマエピローグへ飛んでしまう不具合を修正。
   - 強制遷移タイマーを9.5秒に延長し、手動スキップ受付開始を5.0秒（長押し判定0.45秒）に保護することで、指定の **8.0秒間** 確実に表示・堪能できるよう調整。
5. **映画的BGM2段階ドロップ演出（コミカル音排除・重厚ベース解禁）（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureMusicDirector.cs`, `AdventureSanctuaryTowerManager.Climax.cs`
   - コミカルに聴こえる裏拍スタブ音やベースのピッチブレ（ポヨン感）を完全撤去。
   - 前半（天蓋破壊〜ダイブ〜凍結危機〜注油）は今まで通りの壮大なシネマティック・ブラスパッド＋駆け上がるキラキラしたアルペジオのみで演奏。
   - Rustの最終セリフ「……大空の向こうまで、全力で行こう！！」の表示と同時に、コード進行の位置を引き継いだままMoog風の太くブリブリうねる16分アナログシンセベース（ルート重低音＋オクターブ上レイヤーのデュアルオシレーター構成、フィルターエンベロープ＋ノコギリ倍音＋サチュレーション）＋タイトなキック・スネアが一気に解禁（ドロップ）するカタルシス演出を実装。

6. **ゲームクリア後【N】はじめから（New Game）スポーン位置リセット不具合の修正（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureSaveManager.cs`, `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`
   - **原因**:
     1. クリア後のエピローグ・オートグライド飛行中に【N】を押した際、`ResetToNewGame()` 内で `player.SetAutoGlideMode(false)` を呼ぶ前に `player.Teleport(beachSpawn)` を実行していたため、`AdventureStoryFlow.KeepsAirborne()` が `true` と判定され、地面への吸着（`Stick`）や `ForceGroundReset()` がスキップされていた。
     2. その結果、滑空フラグや空中慣性が残ったままになり、さらに `ForceGroundReset()` 内のカメラ追従リセット（`SnapBehindTarget()`）が走らず、シネマティックカメラがタワー上空を取り残して映し続けていた。
     3. 相棒Rust（ドローン）のプレイヤー近傍テレポート（`drone.TeleportNearPlayer()`）が抜けており、ドローンがタワー上空に置き去りになっていた。
     4. クリアモーダルのキー入力判定が Input System のみで旧 `Input.GetKeyDown(KeyCode.N)` のフォールバックが欠けていた。
   - **対策**:
     1. `ResetToNewGame()` 内でテレポート前に `player.SetAutoGlideMode(false)` と `player.ForceGroundReset()` を先行実行し、確実に滞空・滑空状態を完全解除。
     2. プレイヤーを西側白砂ビーチ（158, 6.5, 275）へテレポートさせ、Terrain高さを加味して完全に接地させた上で `player.ForceGroundReset()` を再実行。
     3. カメラ（`AdventureCameraFollow`）のシネマティックモードを解除し、`SnapBehindTarget()` でプレイヤー背後（ヨー75度・水平）へ即時スナップ。
     4. ドローンRustも `drone.TeleportNearPlayer()` でプレイヤーの横へ確実にテレポート。
     5. クリアモーダルのキー入力判定に旧 Input（`Input.GetKeyDown(KeyCode.N)` 等）のフォールバックを追加。

7. **神殿アプローチ階段＆オアシス池周辺の歩行引っかかり・段差スタック完全解消（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs`
   - **原因**:
     1. `BuildTowerStairs()` 内で生成していた各ステップ（`stepObj`）のデフォルト Cube コライダー（BoxCollider）が削除されておらず、22段の物理エッジによる微小段差スタックが発生していた。
     2. スロープコライダー `StairRamp` の回転計算で `Quaternion.LookRotation(segForward.normalized, Vector3.up) * Quaternion.Euler(-slopeAngle, 0f, 0f)` と角度が2重に掛かって不自然に急勾配になり、厚み 0.8m の上面がステップ表面より 0.24m 浮き上がって最下段手前に 0.4m 近い垂直段差（見えない壁）が生じていた。
     3. オアシス池〜階段アプローチ周辺の巨大岩（`SanctuarySpringPond_Rocks` 等）の足元物理コライダーが階段進入路やステップ側面に食い込んでいた。
   - **対策**:
     1. 各ステップの Cube コライダーを `Object.Destroy(stepCol)` で完全撤去し、ビジュアルのみに変更。
     2. スロープコライダーの回転を `Quaternion.LookRotation(segForward.normalized, Vector3.up)` に是正し、厚み 0.35m・上面をステップ表面（+0.16m）とミリ単位で正確に一致化。
     3. 最下段の手前（オアシス池側地面）から滑らかにステップ上面へと導く「進入ウェッジ（`StairRamp_Entry`）」を新設し、地面からの段差ゼロ（完全バリアフリー）化。終端もテラス床に食い込ませて段差を排除。
     4. 階段中心線から全幅 17m 範囲のコリドー内岩コライダー、および階段足元半径 24m・オアシス池周辺半径 36m の全岩コライダーを `isTrigger = true` 化（見た目は100%残し、足元の激突・引っかかりを完全根絶）。

8. **相棒Rustの自律感情＆愛着仕草・喜び宙返りシステム（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureRustDrone.Curiosity.cs` (新規)
   - `Assets/Game/Scripts/AdventureRustDrone.cs`, `AdventureScrapItem.cs`, `AdventureBeachDriftBox.cs`
   - **機能内容**:
     1. **自律好奇心（Curiosity Investigation）**:
        - プレイヤーが立ち止まった際（静止時）、周囲12m以内の蝶（`AdventureButterflyDrift`）や水辺（海面・オアシス）、足元の草花を自律検知し、ふわりと近づいて観察・ホバリング。
        - 「わぁ、チョウチョだ！」「水面がきらきら光ってる！」など状況に応じたセリフとおしゃべりチャイムを再生。
        - プレイヤーが走り出したり6.5m以上離れると即座に追従復帰。
     2. **愛らしい首かしげ（Curious Tilt）**:
        - アイドル中や観察中に、ボディがコテンと左右に16〜22度傾く（犬や鳥のような首かしげモーション）。
        - 興味対象のない立ち止まり時にもNikoの視線の先へ回り込み、首をかしげてアイコンタクト。
     3. **パーツ獲得・宝箱開封時の喜び宙返り＆星スパークル（Victory Somersault）**:
        - スクラップ獲得時および漂着ボックス開封時に `TriggerCelebration()` が発動。
        - Nikoの斜め前上空へ浮上しながら360度ループ（SmoothStepによる美しい縦宙返り＋横ロール）を実行し、頭上からゴールド＆シアンの星型スパークル粒子（`RustHappyStars`）を散らしながらピロリロリン♪と歓喜チャイムを奏でる。

9. **ピアニスト・カピタのピアノ無音解消＆右手単音・控えめ音量への再調整（2026-09-25追加）**:
   - `Assets/Game/Scripts/AdventureAncientPianoRelic.cs`
   - **調整内容**:
     1. **左手伴奏コードの完全排除**:
        - プロのピアニストのような左手重低音アルペジオを全撤去。カピタ（動物）が小さな前足でぽろん、ぽろんと鍵盤を叩いているような、素朴で愛らしい右手単音メロディ（E5, D5, B4, C5, A4, G5...）のみに特化。
     2. **音量の適正化（控えめで優しい3D音響）**:
        - 音量が大きすぎたため、`volume = 0.38f`、合成波形ピークノーマライズを `0.42f` に抑制。
        - `minDistance = 3.5f`、`maxDistance = 28.0f`、`spatialBlend = 0.70f` に調整し、カピタの木陰のすぐそばで自然に香るような穏やかな3D音響に最適化。
        - 接近時のBGMダッキングも22m以内で最大 `0.22f` とほんのり下げる程度に留め、環境BGMとの調和を確立。
     3. **連弾和音（Eキー）の音量調整**:
        - `CreateFeltPianoChord` のピークノーマライズを `0.48f`、再生音量を `0.55f` に調整。

10. **オープニング演出：遭難直後の波音とRustが心配そうに覗き込んで起こしに来る目覚めシークエンス（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventurePrologueDrama.cs`
    - `Assets/Game/Scripts/AdventurePlayerController.cs`
    - `Assets/Game/Scripts/AdventureRustDrone.cs`
    - **機能内容**:
      1. **波音と暗闇（意識の微睡み）**:
         - PLAYボタン押下後、全画面黒幕（まぶたUI: 上下スライド方式）で暗転し、BGMがダッキング。
         - 実録波音（`ocean_waves_grand.wav`）およびフォールバックのプロシージャル波音（ピンクノイズ＋周期エンベロープ合成）が耳元で静かにフェードイン。
         - 字幕テロップで「……ザザァ……ザザァ……」「……遠くで、波の音が聴こえる。」と表示。
      2. **Rustの必死な呼びかけと薄目アニメーション**:
         - Nikoが仰向けで倒れている足元・胸元から空を見上げるシネマティック視点（地面0.35m, ピッチ-75度）。
         - Rustが顔の真上（0.88m）で心配そうに首をかしげながらホバリングし、「……Niko？　……Niko……？」。
         - まぶたが薄く開き（開度0.28）、青空とRustの輪郭がうっすら見えるが、意識が薄れて再度閉じる（暗転）。
         - Rustが顔のすぐ前（0.65m）まで近づき、頬を小突くようにバウンスしながら「Niko……！　目を覚まして、Niko……！！」。
      3. **完全開眼と起き上がりカメラワーク**:
         - まぶたが完全に開き、光が満ちる。
         - Nikoが上半身を起こして起き上がるように、カメラが仰向け視点から通常の後方俯瞰視点（後方3.6m, 高さ1.55m）へ2.4秒かけてSmoothStepで滑らかにドリー＆パン移動。
      4. **歓喜宙返りと注油ドラマへの接続**:
         - Nikoが起き上がったのを見て、Rustが「ピピッ！……よかったぁぁ！！気がついた……！」と歓喜の宙返りジャンプ（`TriggerCelebration`）。
         - 「脱出ポッドが海に落ちて……ボクたち、この島に打ち上げられたんだ！」と状況を説明。
         - BGMがふんわりとフェードインし、波音が静かな環境音へと引いていく。
         - カメラ操作・プレイヤー操作を解放し、既存の「キキッ……塩水で古いギアが凍りついて動かない……」という注油ドラマへシームレスに接続。

11. **オープニングボードのレイアウト＆操作説明テキストの視認性改善（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventureRustFloatOpening.cs`
    - ボードサイズを `700x515` へゆったり拡張。
    - 本文末尾とPLAYボタンの間に約95pxの余白を確保し、最終行の文字重なりを完全根絶。
    - 最下部の操作説明テキスト（`PlayHint`）を従来の11pt薄色・縁取りなしから、**14pt 太字・くっきりホワイト＋黒アウトライン（フチ取り）** へ強化し、視認性を大幅向上。

12. **サバイバルケースボードと二人の漂着艇ボードの重複重なり解消（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventureBeachDriftBoxManager.cs`
    - `Assets/Game/Scripts/AdventureBeachDriftBox.cs`
    - `Assets/Game/Scripts/AdventureBeachNarrativeManager.cs`
    - **原因**: 漂着サバイバルケース#1の座標 `(151, 270)` と二人の漂着艇の座標 `(152, 275)` がわずか 5m しか離れておらず、サバイバルケースの4.5m接近自動開封と漂着艇のEキー調べが同時にトリガーされ、画面上で2つのボードが重なっていた。
    - **対策**:
      1. サバイバルケース#1の配置を漂着艇から北東へ30m離れた波打ち際 `(178, 290)` へ移動。
      2. `AdventureBeachDriftBox` と `AdventureBeachNarrativeManager` の間に相互排他制御（一方が開いている時はもう一方が絶対に開かないガード）を実装し、画面上でのボード重なりを完全根絶。

13. **漂着航海カプセル#3とキーストーン・手動の自由ボードの重複重なり解消（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventureBeachDriftBoxManager.cs`
    - `Assets/Game/Scripts/AdventureBeachDriftBox.cs`
    - `Assets/Game/Scripts/AdventurePrologueDrama.cs`
    - **原因**: 3個目のスクラップパーツ座標 `(140, 320)` と漂着航海カプセル#3の座標 `(145, 320)` がわずか 5m しか離れておらず、3個目取得時の「キーストーン I：手動の自由（ダッシュ解禁）」演出とカプセルの4.5m接近自動開封が同時に発火して画面上でボードが重なっていた。
    - **対策**:
      1. 漂着航海カプセル#3の配置をパーツ3番から26m離れた木道手前 `(135, 345)` へ移動。
      2. `AdventurePrologueDrama` のダッシュ解禁ルーチン冒頭で既存の情報ボードを安全に閉じる処理を追加。
      3. `AdventureBeachDriftBox` に `IsShowingDashBoard` の排他ガードを追加し、キーストーンボード表示中の自動開封・モーダル表示を完全防止。

14. **マウス視点操作感度の微調整（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventureRustFloatFeel.cs`
    - `Sensitivity`: `0.62f` → `0.50f`（約19%低減し、過敏さを抑えて自然に狙いやすく調整）。
    - `LookSmoothTime`: `0.006f` → `0.008f`（手ブレをわずかに吸収し、滑らかな視点移動に最適化）。

15. **Rust回復後の天空突破BGM（オーバードライブ）のリズム隊強化＆スネア2拍・4拍化（2026-09-25追加）**:
    - `Assets/Game/Scripts/AdventureMusicDirector.cs`
    - 天蓋開放後、Rust回復・完全修復時のドロップBGM（Skybreak Overdrive）において：
      - キック（4つ打ち）: `0.24f` → `0.30f`（+25%）
      - スネアタイミング: 従来のハーフタイム（3拍目のみ: `0.75s, 2.25s`）から、**BPM 160基準の王道ストレートな2拍目・4拍目バックビート（`0.375s, 1.125s, 1.875s, 2.625s`）** へ変更。
      - スネア音量: `0.16f` → `0.22f`（音抜けとタイトなキレを強化）
      - シンセベース（16分Moog風）: `0.26f` → `0.34f`（+30%）
      - アナログ風ソフトリミッター（tanh）を適用し、音割れ（クリッピング）を防ぎつつ力強い推進力と音圧を両立。

16. **風の音の音量強化 & 天空BGMの段階的ビルドアップ展開（2026-09-25追加）**:
    - **風の音の音量強化**（[AdventureSanctuaryTowerManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs)）:
      - 天蓋崩壊・天空滑空時の風音アンビエンス（`_skybreakWindSource`）のフェード目標音量を `0.22f` → **`0.45f`**（約2倍）へ引き上げ、天蓋の開けた大空を切り裂く風の臨場感と迫力を大幅強化。
    - **段階的BGMビルドアップ展開**（[AdventureMusicDirector.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureMusicDirector.cs)）:
      - 4段階トラック（`Intro`, `BassOnly`, `Full`, `DrumsOnly`）を導入。
      - **BGM 1周目（天蓋レバー操作〜12秒間）**: 神聖なブラスとアルペジオのみ（ドラムなし・ベースなし）で天蓋開放の荘厳さを表現。
      - **BGM 2周目（12秒〜24秒）**: 2周目の頭から**ベースが入り**、力強いグルーヴと前進感をプラス。
      - **BGM 3周目（24秒〜Rust回復まで）**: 3周目の頭から**ドラムも加わり**、キック・スネア・ハット＋ベース＋ブラスの完全フル編成へ！
      - **Rust回復時（「全力で行こう！！」のクライマックス）**: 今までと同じタイミングで再生位置をシームレスに引き継ぎ、**ベースが抜けてドラムとBGMになる**！爽快で軽快な疾走感で大空の滑空へ飛び立つ。
17. **トンボの「歯ブラシ」違和感解消＆リアル昆虫造形・前後位相差羽ばたきへの刷新（2026-09-25追加）**:
    - **原因**:
      - 従来のトンボ（`AdventureParadiseCreatures.cs` / `AdventureDragonfly.cs`）は、頭部や目がなく、棒状シリンダーの先端に幅広のCube（板1枚）が乗り、さらに羽ばたきが前後回転（X軸）で高速振動していたため、先端のブラシが小刻みに震える「電動歯ブラシ」に見えていた。
    - **改修内容**:
      - **頭部＆エメラルドブラック複眼（Head & Left/Right Eye）**: 丸みのある頭部に、左右のつぶらな光沢複眼を追加（エメラルドブラック光沢マテリアル）。
      - **胸部＆スマートな節尾（Thorax & Tail & TailTip）**: しっかりとした胸部から、後方へ極細でスマートに伸びるリアルな節尾を造形。
      - **4枚の繊細な透明翅（Fore/Hind Left/Right Wings）**: 1枚の四角い板を廃止し、オーガンジーのように薄く透き通る前翅2枚・後翅2枚の計4枚を配置。
      - **自然なロール羽ばたき＆前後位相差**: 前後振動を廃止し、Z軸ロールによる上下羽ばたき（約60Hz）に修正。前翅と後翅で羽ばたきの位相をずらすことで、本物のトンボ特有の優雅で軽快なホバリング飛翔を実現。
      - 起動時に旧パーツを自動撤去・新パーツへ自動再構築するフェイルセーフを実装。

18. **通常探索BGM（エンディング以外のアンビエントテーマ）音量の微調整（2026-09-25追加）**:
    - [AdventureMusicDirector.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureMusicDirector.cs)
    - `AmbientThemeVolume`: `0.30f` → `0.20f`（約33%引き下げ）。
    - エンディング（天蓋突破・クライマックス・エピローグ）の大音量演出は維持したまま、通常探索中の穏やかなフェルトピアノ＆アンビエントパッドの音量を少し抑え、波音・風音・蝉時雨・足音などの環境音と心地よく調和するように最適化。

## 次の推奨タスク

1. **探索の手触り向上：白砂ビーチの貝殻・漂着物・スクラップ採取インタラクション＆収集ポップ演出**:
   - ビーチ散策時にキラキラ光る貝殻や古代の漂着ボトルをワンボタンで拾える小気味よい収集ループ（拾うとチャリン音と小さな浮遊アイコンポップアップ）。
2. **海・波打ち際・水しぶきの環境美化**:
   - 白砂ビーチ周辺の波打ち際エフェクトや水面の反射・環境音の微細チューニング。

## ブランチ

`main`
リモート: `https://github.com/Yosie-lab/AdventureWorld.git`
コミット方針: ユーザーが commit / push を明示したときだけ。AdventureWorld の地形と Demo/Loader へのビルド差し替えは入れない。

