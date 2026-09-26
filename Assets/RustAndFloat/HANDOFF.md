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
      - **原因と解消（2026-09-26改善）**: 従来のコルーチン待機だと、外部からの `KeepEndingThemeActive()` 呼び出しや `StopAllCoroutines`、曲終了時のリスタート判定によりコルーチンが途中で強制終了され、2周目以降へ遷移できない問題があった。これをコルーチン完全廃止し、**`Update()` による再生秒数・周回監視の堅牢なステートマシン**へ刷新。外部呼び出し時も再生中なら絶対に頭へ巻き戻さないよう保護。
      - **BGM 1周目（天蓋レバー操作〜12秒間）**: 神聖なブラスパッドとアルペジオ、温かいパッド和音のみで静謐かつ荘厳に大空の開放を演出。
      - **BGM 2周目（12秒〜24秒）**: 12秒到達で**ブリブリベースが鳴り響き合流**、推進力と前進感をプラス。
      - **BGM 3周目（24秒〜Rust回復まで）**: 24秒到達で**ドラムが合流**（キック・スネア・ハット＋ベース＋ブラス＋アルペジオ＋パッド和音による力強いフル編成！※バイオリン主旋律は一時ミュート中）。
      - **Rust回復時（「全力で行こう！！」のクライマックス）**: 今までと同じタイミングで再生位置をシームレスに引き継ぎ、**ベースが消え、軽快なドラムと爽快なブラス・アルペジオBGMのみになる**！大空へ抜け出すクリアな疾走感へスイッチ。
17. **トンボの「歯ブラシ」違和感解消＆リアル昆虫造形・前後位相差羽ばたきへの刷新（2026-09-25追加）**:
    - **原因**:
      - 従来のトンボ（`AdventureParadiseCreatures.cs` / `AdventureDragonfly.cs`）は、頭部や目がなく、棒状シリンダーの先端に幅広のCube（板1枚）が乗り、さらに羽ばたきが前後回転（X軸）で高速振動していたため、先端のブラシが小刻みに震える「電動歯ブラシ」に見えていた。
    - **改修内容**:
      - **頭部＆エメラルドブラック複眼（Head & Left/Right Eye）**: 丸みのある頭部に、左右のつぶらな光沢複眼を追加（エメラルドブラック光沢マテリアル）。
      - **胸部＆スマートな節尾（Thorax & Tail & TailTip）**: しっかりとした胸部から、後方へ極細でスマートに伸びるリアルな節尾を造形。
      - **4枚の繊細な透明翅（Fore/Hind Left/Right Wings）**: 1枚の四角い板を廃止し、オーガンジーのように薄く透き通る前翅2枚・後翅2枚の計4枚を配置。
      - **自然なロール羽ばたき＆前後位相差**: 前後振動を廃止し、Z軸ロールによる上下羽ばたき（約60Hz）に修正。前翅と後翅で羽ばたきの位相をずらすことで、本物のトンボ特有の優雅で軽快なホバリング飛翔を実現。
      - 起動時に旧パーツを自動撤去・新パーツへ自動再構築するフェイルセーフを実装。

18. **通常探索BGM（フェルトピアノ＆アンビエントテーマ）音量の微調整＆カピタのピアノ生演奏ダイナミック音響（2026-09-26再調整）**:
    - [AdventureMusicDirector.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureMusicDirector.cs)
    - [AdventureAncientPianoRelic.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureAncientPianoRelic.cs)
    - `AmbientThemeVolume`: `0.20f` → **`0.11f`**（約45%引き下げ）。
    - ピアノメロディの生成ゲイン: `0.14f` → **`0.075f`**（約46%カット）。
    - パッド和音ゲイン: `0.045f` → **`0.030f`**。
    - **カピタのピアノ生演奏・ダイナミック接近音量連携**:
      - 島の奥でピアノを弾いているカピタ（`AdventureAncientPianoRelic`）に近づくにつれて、3D音響のピアノ生演奏がダイナミックに増大（60m手前から聴こえ始め、目の前で最大0.95fの豊かな響きに）。
      - カピタに近づくにつれて全体探索BGMがスムーズにダッキング（最大80%減衰）され、「島に流れるあのピアノ曲はカピタがここで奏でていた」という極上の情緒と生演奏への引き込みを演出。

19. **エピローグ・映画字幕第3幕の2行分割と精密タイミング調整（2026-09-25追加）**:
    - [AdventureSanctuaryTowerManager.Epilogue.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSanctuaryTowerManager.Epilogue.cs)
    - 従来の長い1行を2行に分割し、指定された精密なフェード・保持時間へ更新：
      1. **「傷つくかもしれない自由と、」**: 0.5秒フェードイン ➔ **2.0秒保持** ➔ **0.4秒フェードアウト**
      2. **「生きることの重みを取り戻した二人の旅が、」**: **0.5秒フェードイン** ➔ **2.8秒保持** ➔ **0.5秒フェードアウト**
      3. **「ここから、また始まる。—— 『Rust & Float』」**: 最終結びタイトルへシームレスに接続。

20. **Jキー（小ジャンプ専用）とSpaceキー（大ジャンプ＆滑空ジャンプ）の操作分離＆小岩乗り越え・高度・SE最適化（2026-09-26追加）**:
    - [AdventurePlayerController.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventurePlayerController.cs)
    - [AdventureCameraFollow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCameraFollow.cs)
    - [AdventureNikoFootsteps.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureNikoFootsteps.cs)
    - [AdventureSanctuaryTowerManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs)
    - **「まだ全然岩に乗れない」根本原因の完全解消**:
      - **インスペクターキャッシュ問題**: Unity Editor上で以前のインスペクターシリアライズ値（0.8f）がメモリキャッシュされており、コード変更が上書きされていたため、`Start()` および `HandleJump` にて `Mathf.Max(shortJumpHeight, 2.6f)` を強制適用し、確実に高度を保証。
      - **岩の実寸への最適化**: 島の小岩・中岩（スケール1.3〜1.9倍、高さ約1.8m〜2.4m）を足元に捉えられるよう、小ジャンプ高さを **`2.6m`（初速約11.2m/s）** に引き上げ。
      - **常時段差乗り上げ判定**: `_cc.stepOffset = StepOffsetGround`（0.45m）を常時有効化し、岩の上面のフチや角に足先が触れた際、弾き落とされずに吸い付くように天面へ乗れるよう改善。
      - **コヨーテタイム強化**: `_airborneTime < 0.25f` に拡大し、助走中や起伏でジャンプ入力が抜けるのを防止。
    - **Jキー**: 軽快な小ジャンプ専用（高さ約2.6m / `shortJumpHeight = 2.6f`、踏み切りポップ音 `PlayJumpSound()` 再生、滑空には移行しない）。
    - **Spaceキー**: 通常〜大ジャンプ（高さ2.2m）＆長押しでの滑空（グライダー展開）。湖・砂浜・崖でのサーマル大上昇ジャンプもSpaceキー専用。
    - 台本送りや注油ホールド、クリア後の「大空へダイブ」はどちらのキーでも操作可能。

21. **木道スロープ（Boardwalk Ramps）の地下完全埋め込み＆頭上挟まり自動脱出の実装（2026-09-26追加）**:
    - [AdventureBeachEscapeManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachEscapeManager.cs)
    - [AdventureRebuildBoardwalkRamps.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/Editor/AdventureRebuildBoardwalkRamps.cs)
    - [AdventurePlayerController.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventurePlayerController.cs)
    - **「下から登ってたら板をすり抜けようとして挟まった」根本原因と解消**:
      - **原因**: 木道スロープが地面の窪みの上を通る際、板の厚みが0.80m（下へ0.65m）しかなかったため、板の底面と地面との間に1m〜2.5mの空洞（隙間）ができていた。海岸から登るプレイヤーがこの空洞に潜り込み、傾斜が狭まる途中で頭上の板と地面に挟まってスタックしていた。
      - **板の地下完全埋め込み**: 各セグメントの底面を地形の深さよりさらに1.2m深く（`bottomY = Mathf.Min(center.y - 1.2f, groundUnder - 1.2f)`）埋め込み、下方向の隙間・空洞を100%消滅。下から潜り込むこと自体を物理的に不可能にした。
      - **頭上挟まり自動脱出（`UnstuckFromOverheadPlanks`）**: 万が一頭上に板や構造物が接触・圧迫した際、板の上面（歩行面）へスッと自動リフト・脱出させる救済判定をPlayerControllerに導入。

22. **スタートからエンドまでの包括的リファクタリング＆高速化（2026-09-26追加）**:
    - [AdventureStoryFlow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureStoryFlow.cs)
    - [AdventureCameraFollow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCameraFollow.cs)
    - [AdventureRustFloatOpening.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustFloatOpening.cs)
    - [AdventureCapytaBodyCollider.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCapytaBodyCollider.cs)
    - [AdventureCapytaBlessing.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCapytaBlessing.cs)
    - [AdventureScrapManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureScrapManager.cs)
    - [AdventureScrapHUD.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureScrapHUD.cs)
    - [AdventureSanctuaryTowerManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSanctuaryTowerManager.cs) (& `.Canopy.cs`, `.Climax.cs`, `.Epilogue.cs`)
    - [AdventureRustDrone.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.cs)
    - [AdventureSaveManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSaveManager.cs)
    - **実施内容**:
      - **毎フレームの重いシーン探索（`FindObjectsByType` / `FindAnyObjectByType`）の根絶**:
        - `AdventureCapytaBlessing.FindNearestCapyta()`: 毎フレーム実行されていた全Transform走査（`FindObjectsByType<Transform>`）を完全撤廃し、`AdventureCapytaBodyCollider.AllCapytas` の静的レジストリ（`OnEnable`/`OnDisable` で自動登録）へ移行。
        - `AdventureCameraFollow`: `Instance` シングルトン化を行い、他クラスからの毎フレームの `Camera.main.GetComponent<AdventureCameraFollow>()` や探索を直結キャッシュへ置換。
        - 各マネージャーの `Ensure()`: 既にインスタンスが存在する場合の早期リターン（`if (_instance != null) return;`）を徹底し、無駄なオブジェクト検索・GCアロケーションを抑止。
      - **イベント駆動連携とフレームキャッシュ**:
        - `AdventureStoryFlow`: `OnPhaseChanged` イベントを新設。また、1フレーム内に各所から数十回呼び出される `Current` プロパティに `Time.frameCount` によるフレームキャッシュを導入し、重複プロパティ判定を1回に集約。
        - `AdventureScrapManager`: `OnProgressChanged` イベントを新設し、パーツ回収・ボックス開封・ピアノ遺物回収・ニューゲームリセット時に発火。
        - `AdventureScrapHUD`: イベント購読（`OnPhaseChanged`, `OnProgressChanged`）により、毎フレームの過剰な文字列組み立てや状態ポーリングを最小化。
      - **UI・シネマティック参照の整理**:
        - `AdventureRustFloatOpening.GuideText` の直接参照化により、エピローグ等での `FindObjectsByType<Text>` による名前検索を根絶。
        - エンディング〜クリアモーダル〜ニューゲーム再開（F8）のライフサイクルにおいて、不要なオブジェクト探索や状態の競合を解消。

23. **相棒Rustの「自律感情＆無駄の愛おしさ・生命感」システムの実装（2026-09-26追加）**:
    - [AdventureRustDrone.Curiosity.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.Curiosity.cs)
    - [AdventureRustDrone.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.cs)
    - **実施内容**:
      - **水濡れ嫌がり・肩退避パニック（`WaterPanic`）**: 機械であるRustは水が大の苦手。Nikoが波打ち際や池に足を踏み入れると、「ヒャッ！水だ！」「錆びちゃう錆びちゃう！」「抱っこして〜！」と大慌てでNikoの頭上高く・肩へ退避し、体を激しくブルブル震わせて水滴を払う。陸地に戻ると「ふぅ…助かったぁ」とホッと安堵する。
      - **足元うたた寝＆びっくり飛び起き（`SleepNap`）**: Nikoが立ち止まって4.5秒以上経つと、足元（地上約0.28m）へゆっくり降りてちょこんと丸まり、「すぅ…すぅ…」「……Zzz」と寝息を立てる。シアンのインジケーターライトが寝息に合わせてゆったり呼吸パルス明滅。Nikoが歩き出すと「ハッ！寝てないよ！」「いつでも行けるよ！」と上にピョコンと飛び起きて慌てて体勢を立て直す。
      - **足元の花をじっと見つめる仕草（`Flora`）**: 足元の草花を見つけると、真上0.35mで下向き（ピッチ-42度）に見下ろしてホバリングし、左右にコテンコテンと愛らしく小首を傾げる。「わぁ、小さな花が咲いてる…！」「いい匂いがするよ」と嬉しそうに語りかける。
      - **蝶との優雅な螺旋ダンス（`Butterfly`）**: 蝶を見つけると、蝶の周囲を半径0.6mでふわりふわりと螺旋状に周回しながら並走。「わぁ、チョウチョさん！一緒に飛ぼう！」と楽しそうに舞う。
      - **愛らしいアイコンタクト（`NikoEyeContact`）**: Nikoの胸〜顔の前に回り込んで上目遣いで見つめ、「どうしたの、Niko？」「ずっと一緒だよ」と語りかける。
      - これら無駄だが愛おしい自律生命感により、終盤の「相棒が凍りつき、最後の油を全部注ぐクライマックス」の感情移入と切なさが最高潮に引き立つ設計へ昇華。

24. **ESCポーズ＆設定メニュー（BGM/SE音量・マウス感度・操作チートシート・リスタート）の実装（2026-09-26追加）**:
    - [AdventurePauseMenu.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventurePauseMenu.cs)
    - [AdventureCameraFollow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCameraFollow.cs)
    - [AdventureMusicDirector.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureMusicDirector.cs)
    - [AdventurePlayerController.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventurePlayerController.cs)
    - **実施内容**:
      - **洗練された全画面ポーズUI**: 【ESCキー】でゲーム時間を完全一時停止（`Time.timeScale = 0f`）し、カーソルを解放して美しい2カラム設定パネルを表示。
      - **BGM音量スライダー**: 0〜100%のリアルタイム制御（`AdventureMusicDirector.MasterBgmVolumeScale`）。PlayerPrefsに永続化。
      - **SE音量スライダー**: 0〜100%の制御。スライダー操作時に心地よいクリスタルチャイム音で試聴可能。PlayerPrefsに永続化。
      - **マウス視点感度スライダー**: 0.05〜0.60の範囲で直感調整可能（`AdventureCameraFollow.MasterSensitivity`）。
      - **操作ガイドチートシート**: 移動・視点・小ジャンプ(J)・滑空(Space)・ダイブ/滞空(W/S)・手当て/撫でる(E)・遠隔指示(F)・クエスト切替(Tab)・セーブ(F5)を一覧で即座に確認可能。
      - **最初からやり直す（リスタート）機能**: 誤爆防止の確認ダイアログを挟み、砂浜座礁艇前からのニューゲーム初期化をワンクリックで実行可能。

25. **白砂ビーチの貝殻・シーグラス収集ループ＆水しぶき足音・飛沫エフェクトの実装（2026-09-26追加）**:
    - [AdventureNikoFootsteps.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureNikoFootsteps.cs)
    - [AdventureBeachSeashellItem.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeashellItem.cs)
    - [AdventureBeachSeashellManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeashellManager.cs)
    - [AdventureSaveManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSaveManager.cs)
    - **実施内容**:
      - **水しぶき足音＆飛沫パーティクル（Splash Footsteps & FX）**:
        - 波打ち際（標高5.85m〜6.25m）や池の浅瀬に入った瞬間、通常の乾いた足音から**「チャプッ、ピシャッ」**という澄んだ水しぶき足音へシームレスに自動切替。
        - 足元から水滴がパッと跳ね上がる半透明の飛沫パーティクルエフェクトを左右交互に生成。水面着地時には「バシャァン！」と水飛沫が弾ける。
      - **白砂ビーチの貝殻・シーグラス・小琥珀収集ループ**:
        - 西側白砂ビーチの波打ち際に全24個の可憐なアイテム（桜色のサクラガイ、エメラルド/サファイアのシーグラス、黄金の小琥珀、純白の巻貝）を散滅配置。太陽光のキラキラスパークル付き。
        - Nikoが近寄って【Eキー】またはクリックで採取。澄んだクリスタルチャイム音とともに手元へフワリと吸い込まれるポップアニメーション。
        - 画面下部に洗練された収集トースト（「✦ 桜色のサクラガイ を拾った」）を表示し、相棒Rustも「わぁ、花びらみたいな貝殻だね！」と嬉しそうにリアクション。
        - ニューゲーム（F8 / ポーズ初期化）時には自動で全貝殻が再配置される連携を完備。

26. **バイオーム別の空間立体アンビエンス音響システムの実装（2026-09-26追加）**:
    - [AdventureBiomeAmbienceManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBiomeAmbienceManager.cs)
    - **実施内容**:
      - **4大バイオーム＆滑空気流のシームレス音響クロスフェード**:
        - プレイヤーの座標・標高・飛行状態をリアルタイム判定し、時定数約2.0秒のスムーズな音量フェードでグラデーション再生：
          - **Coast（砂浜・波打ち際）**: 既存のリアルな潮騒・波音（`AdventureBeachWavesManager`）とカモメの声を活かすため、内陸風音を静かに抑え波音へ譲る設計。
          - **Meadow（大草原）**: 草を優しく撫でるサラサラとした乾いた風音と微細な草擦れのゆらぎ。
          - **Forest（東部大樹海・カルデラ湖・オアシス）**: 深みのある木々のこずえのざわめき（重厚な低域風＋葉擦れのサワサワ音）に加え、**5〜10秒間隔で梢から澄んだ小鳥のさえずり（ピピ、チッチッ…）** が愛らしく響く。
          - **Highland（中央タワー・北崖テラス）**: 標高54m以上の高空に吹き抜けるピューという澄んだ風音と天蓋共鳴。
          - **SkyGliding（大空滑空時）**: 翼を切り裂く爽快なゴーッという疾走風切り音。ブースト時には音量とピッチが跳ね上がり、滑空中は地上の環境音をダッキングして疾走感を際立たせる。
      - **ポーズ設定音量との完全連動**:
        - ポーズメニューの「SE音量スライダー（`Adventure_SeVolume`）」とリアルタイム連動。
      - **完全スタンドアロンなプロシージャル波形合成**:
        - 外部オーディオファイルへの依存なしに、実行時に高品質なループクリップ・小鳥の鳴き声を自動生成して常時動作。

27. **カピタの体躯すり抜け防止の多重強化（2026-09-26追加）**:
    - [AdventureCapytaBodyCollider.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCapytaBodyCollider.cs)
    - [AdventureAncientPianoRelic.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureAncientPianoRelic.cs)
    - **原因と解消**:
      - **原因**: カピタはアニメーションや首振り等で毎フレームTransformが動いていたが、`Rigidbody` がなかったためPhysXがStatic Colliderと見なして内部キャッシュが狂い、CharacterControllerとのトンネリング（すり抜け）が発生していた。また、コライダー高さ（1.45m）がNikoのジャンプ・ステップ登攀で踏み越えられやすく、ピアノの椅子（高低差）でプッシュバックのY軸判定が外れるケースがあった。
      - **改修内容**:
        - **Kinematic Rigidbody（ContinuousSpeculative）の自動付与**: アニメーション中もPhysXが毎フレーム衝突面を完全トラッキングし、高速移動時も物理レベルでトンネリングを遮断。
        - **目標ワールド寸法の強化**: 横幅 `1.25m`、全長 `1.85m`、高さ **`1.95m`** に拡大し、Nikoが頭上を踏み越えたりステップクライムできない十分な防壁を形成。
        - **高低差対応プッシュバック＆ハードフェイルセーフ**: ピアノ丸椅子や起伏のある地形でも上下判定が外れないよう足元〜頭上交差判定へ刷新し、Move()が引っかかった場合でも瞬時に外側へスナップ押し出しする二重防御を実装。

28. **貝殻・シーグラスの報酬ループ＆Rust着せ替え工房の実装（2026-09-26追加）**:
    - [AdventureRustCosmetics.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustCosmetics.cs)
    - [AdventureRustWorkshopUI.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustWorkshopUI.cs)
    - [AdventureBeachSeashellManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeashellManager.cs)
    - [AdventureRustDrone.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.cs)
    - [AdventureSaveManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSaveManager.cs)
    - **実施内容**:
      - **収集アイテムのインベントリストック化**:
        - サクラガイ・エメラルド硝子・サファイア硝子・太陽琥珀・純白巻貝の採取時にストック数を加算・PlayerPrefs保存。
      - **全5種類のRustアクセサリー（ドレスアップ・コスメティクス）**:
        - 🌸 **サクラガイの花冠 (Head)**: 桜色貝殻のティアラ＋花びらパーティクル。
        - 🟢 **エメラルド・アンテナランプ (Antenna)**: 深緑のクリスタル発光＋周囲を照らすPointLight。
        - 🔷 **サファイアの翼チャーム (Wings)**: 左右サファイア小翼＋飛行時の蒼いTrail（光の軌跡）。
        - ☀️ **太陽の琥珀コア (Core)**: 黄金色の琥珀コア＋胴体ライト黄金化＋推進ゴールドスパーク。
        - 🐚 **純白巻貝のホイッスル (Side)**: 側面の純白巻貝。
      - **Rust着せ替え工房UI＆砂浜の作業台（Workbench）**:
        - 【Bキー】または座礁艇前の作業台で【Eキー】を押すと、洗練された2カラムのガラスモフィズム工房UIが起動。
        - 左側に素材ポーチの所持数、右側にアクセサリーカタログと「つくる」「そうびする」「はずす」ボタンを配置。
        - クラフト・装備時にはRustが宙返り（Celebrate）し、「わぁ…！すっごく可愛い！ありがとう！」と嬉しそうにリアクション。
        - ニューゲーム（F8 / ポーズ初期化）時のコスメティクス初期化連携を完備。

29. **カーソル消失・ロック競合の完全根絶（2026-09-26追加）**:
    - [AdventureStoryFlow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureStoryFlow.cs)
    - [AdventureCameraFollow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCameraFollow.cs)
    - [AdventureRustWorkshopUI.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustWorkshopUI.cs)
    - [AdventureBeachDriftBox.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachDriftBox.cs)
    - [AdventureBeachNarrativeManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachNarrativeManager.cs)
    - **原因と改修内容**:
      - **原因**:
        1. 各種UIモーダル（Rust工房UI、漂流箱手記、海岸日誌・石碑、ポーズメニュー等）が開いた際、`AdventureStoryFlow.WantsFreeCursor` がそれらを網羅していなかったため、カメラUpdate側でマウス移動やクリック時に即座に `Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;` と再ロック・不可視化されてしまっていた。
        2. UI上のボタンクリック時（`IsPointerOverGameObject`）でもカメラのクリック判定が走り、カーソルが再ロックされていた。
        3. Unityウィンドウのフォーカス復帰時（`OnApplicationFocus`）にカーソル復帰処理がなかった。
      - **改修内容**:
        - `AdventureStoryFlow.WantsFreeCursor` をすべてのUIモーダル（ポーズ、工房UI、手記、海岸日誌、オープニング、聖域台本、クリア画面等）を統合判定するプロパティに刷新。
        - `AdventureCameraFollow` で `WantsFreeCursor` 中はカーソル表示とアンロック（`CursorLockMode.None`）を強固に維持し、UIクリック等による意図しない再ロックを完全遮断。UI表示中の背景カメラ誤回転も停止。
        - 通常探索時も `EventSystem.IsPointerOverGameObject()` ガードを設け、UI要素クリックでカーソルが消えないように保護。
        - 各UIを閉じる際も、他に開いているモーダルがあればカーソルを解放し続ける安全ガードを徹底。

30. **白砂ビーチ＆浅瀬の4大情緒ビジュアル・環境音響強化（2026-09-26追加）**:
    - [AdventureBeachVisualEnhancer.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachVisualEnhancer.cs)
    - [AdventureNikoFootsteps.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureNikoFootsteps.cs)
    - **実装内容**:
      - **1. 浅瀬の光の揺らめき（コースティクス・網目状の波光投影）**:
        - 西側海岸線（Z: 165m〜425m）の地形表面を二分探索で自動スキャンし、水深0m〜1.8mの浅瀬底面にぴったり沿うリボンメッシュを生成。
        - 太陽光が水面で屈折して生まれるプロシージャルVoronoi光網目テクスチャを多重UVスクロールさせ、ゆらゆらと波打つ美しい海底の光を再現。
      - **2. 寄せては返す波打ち際の白波ライン（Shoreline Wave & Foam）**:
        - 汀線から砂浜にかけて広がるサーフフォームメッシュを自動生成。
        - 周期約4.2秒の非線形ウェーブにより、海から砂浜へ白泡が押し寄せ、引くときにすっと消える情緒的な波打ち際を演出。
      - **3. 白砂に残る愛らしい足跡（Sand Footprints）**:
        - Nikoが白砂ビーチを歩いたとき、左右の足元に小さな足跡デカール（クワッド）を生成。
        - 約3〜7秒かけて砂に溶けるようにフェードアウト。波打ち際近くの足跡は波によって洗い流される。
      - **4. 砂浜サクサク足音 & 波打ち際のきらめく潮煙（Sea Mist）**:
        - 砂浜を歩いたときに心地よい「サクッ、サクッ」という乾いた細粒白砂の擦過音をプロシージャル合成して自動ブレンド。
        - 海岸線沿いにふんわりと漂い、陸地への海風に乗ってきらめく微細な潮煙（シーミスト）パーティクルを配置。

31. **RustのNiko体躯食い込み完全防止＆寄り添い距離の適正化（2026-09-26追加）**:
    - [AdventureRustDrone.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.cs)
    - [AdventureRustDrone.Curiosity.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.Curiosity.cs)
    - **原因と改修内容**:
      - **原因**:
        1. 水嫌がり（`CuriosityKind.WaterPanic`）や見つめ合い（`NikoEyeContact`）、スキンシップ（`Petting`）時の目標オフセットがNikoの首〜肩・胸元に近すぎ（水平距離約0.5m）、Rust本体やアクセサリーがNikoの体や頭にめり込んでいた。
        2. Rustにコライダーがないため、Nikoの急停止や旋回時に慣性で体内へ突入してしまっていた。
      - **改修内容**:
        - **体躯クリアランス安全ガード（`EnforceNikoBodyClearance`）の実装**:
          - Nikoの体躯（足元〜頭上、半径0.82m）への食い込みを、`Update` および `LateUpdate` の二重ループで毎フレーム監視・物理的プッシュアウト。体内方向への速度ベクトルもカットし、めり込みを100%遮断。
        - **各寄り添いアクション目標位置の適正化**:
          - 水嫌がり退避: 右肩斜め上（右0.85m, 後方0.35m, 高さ1.82m）へ外出しし、肩越しに怖がる愛らしい姿がクリアに見えるよう調整。
          - 見つめ合い: 前方1.60m, 右0.65m, 高さ+0.22m に調整。
          - 撫でスキンシップ（Petting）: 前方1.35m, 右0.45m, 高さ+0.18m に適正化。
          - 通常追従（Follow）: 右1.25m, 後方1.55m にわずかに広げ、アクセサリーが映える構図を確保。

32. **相棒Rustの探索ナビ＆お宝レーダー（2026-09-26追加）**:
    - [AdventureRustDrone.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustDrone.cs)
    - [AdventureBeachSeashellItem.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeashellItem.cs)
    - [AdventureBeachDriftBox.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachDriftBox.cs)
    - **機能概要**:
      - **3大お宝の優先探知**:
        1. **スクラップパーツ（35m以内、最優先）**: 「ピピピッ！あそこにパーツの反応があるよ！」（黄金の光）
        2. **漂流木箱（26m以内）**: 「見て見て！あっちに漂着した木箱が落ちてるよ！」（シアンブルーの光）
        3. **貝殻・シーグラス（22m以内）**: 「ピピッ！あっちに綺麗な『{itemName}』があるよ！」（貝殻固有のテーマカラーの光）
      - **レーダーPointLight（9Hz点滅）＆ソナーチャイム音**:
        - Rust上部のアンテナ位置に配置した `Rust_RadarLight` が、お宝発見中にお宝カラーで9Hz高速点滅。
        - 探知開始時および約4.0秒おきに高周波のソナーピピピ音を再生。
      - **先行飛行＆指差し姿勢**:
        - Nikoとお宝を結ぶベクトル上、前方2.1mへ先行飛行。
        - 胸元より少し高い位置でリズミカルに上下バウンスしながら、お宝の方向へピシッと機首を向けて「あそこ！」と小刻みに指差し合図。
      - **アイテム取得時の大喜び宙返り（Celebration）連動**:
        - スクラップ、漂流木箱、貝殻を拾い上げた瞬間に、Rustが空中で360度宙返りして大喜び＆感想を喋る。

33. **ウミネコ（カモメ）の翼展開＆力強い羽ばたき・滑空飛行の完全刷新（2026-09-26追加）**:
    - [AdventureBeachSeagull.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeagull.cs)
    - **原因と改修内容**:
      - **原因**: 翼メッシュの座標軸定義と飛行時の回転計算において、地上で閉じた姿勢から飛行時の「横に広げる角度」への展開が行われず、胴体に沿った閉じた角度のまま羽ばたき成分が微弱に加算されていたため、翼を閉じたまま滑るように飛んでいた。
      - **改修内容**:
        - **流線型の外向き翼メッシュ（左右個別）**: 付け根を原点とし、左右外側へ滑らかに広がる翼型メッシュ（上面・下面・前縁・後縁）を新規生成。
        - **地上での折りたたみ姿勢**: 地上に佇んでいるときは、翼を背中・お尻側にキュッと美しく折りたたんで休むリアルな鳥の姿勢を再現。
        - **離陸時の翼オープン＆ダイナミック羽ばたき**: 飛び立つ瞬間に0.25秒で翼を左右へバッと全開展開。
        - **3軸連動羽ばたき（Flapping）**:
          - 上下フラッピング（振幅 ±38度）
          - 迎え角ひねり（打ち下ろし時は前傾・推進力、打ち上げ時は後傾）
          - 前後スイング
        - **上昇後の優雅な滑空（グライディング）**: 巡航高度に達すると、風に乗って羽を水平に広げて揺れる滑空モードと羽ばたきを周期的に交互に実施。胴体・尾羽も羽ばたきと同期して上下に連動バウンス。

34. **カメラ視点の反応異常の根絶＆極上TPS操作感への全面最適化（2026-09-26追加）**:
    - [AdventureCameraFollow.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCameraFollow.cs)
    - [AdventureRustFloatFeel.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureRustFloatFeel.cs)
    - **原因と改修内容**:
      - **原因**:
        1. **過敏すぎる感度**: `Sensitivity` が 0.50f（過剰値）に設定されており、高DPIや微小なマウス移動で視点が吹っ飛ぶような過剰反応を起こしていた。
        2. **勝手なピッチ水平引き戻し**: マウス操作停止後わずか0.55秒で、ピッチが勝手に 0度（完全水平）へ強制リセットされていたため、見下ろしたり見上げたりしてもすぐに正面に戻されていた。
        3. **腰への強制LookRotationブレンド**: 地上歩行中にカメラ姿勢をNikoの腰（1.05m）を見る向きと55%強制ブレンドしていたため、上下入力が潰れたり引っかかる歪みが生じていた。
        4. **狭すぎる上下視野制限**: `pitchMin = -12f, pitchMax = 32f` で足元も空も見えず窮屈だった。
      - **改修内容**:
        - **感度をキビキビと軽快に動く適正値 0.28f へ引き上げ**（鈍すぎた 0.12f から約2.3倍へ改善。過去の保存値も安全に自動補正）。
        - **マウス操作時のスムージング遅延を完全排除**: 立ち止まり中・ジャンプ中・歩行中を問わず、マウスを動かした瞬間にダイレクト100%即時追従。
        - **勝手なピッチ引き戻し処理を完全撤廃**（プレイヤーが向けた上下アングルを100%忠実に維持）。
        - **腰への強制フレーミングを完全撤廃**し、入力角度をダイレクトにカメラへ適用。
        - **上下可動域を拡大**（見下ろし -42度、見上げ +58度）し、足元の貝殻から大空のウミネコまで快適に見回せるよう改善。
        - **リアルタイム感度微調整ホットキー**: 【 [ / ] 】キー またはテンキーの【 - / + 】でゲームプレイ中にいつでも好みの感度に微調整可能。

35. **カピタとのふれあい・物々交換（トレード）システムの実装（2026-09-26追加）**:
    - [AdventureCapytaBlessing.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureCapytaBlessing.cs)
    - [AdventureBeachSeashellManager.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachSeashellManager.cs)
    - **機能概要**:
      - **ふれあいインタラクション（【Eキー】）**:
        - カピタに近づくと、優しく鼻を鳴らしてこちらを向く。
        - 【E】でスキンシップ。カピタが喜びのダンス（`CapytaDance`）を踊り、頭上にハート＆星のキラキラ微粒子が舞い上がる。
        - 初回はスーパージャンプ獲得、2回目以降は機嫌に応じた潤滑油（+5〜30）をもらえる。
      - **貝殻・シーグラスの物々交換（【Qキー】）**:
        - 貝殻を1個以上持っていると、プロンプトに `🐚 【Q】貝殻を渡して物々交換（所持: N個）` が自動表示。
        - 【Q】で貝殻をプレゼントすると、カピタが貝殻の種類（サクラガイ、エメラルド、サファイア、琥珀、巻貝）に応じた大歓喜の感想を喋る。
        - **頭乗せみかんのプロシージャル生成**: カピタの頭ボーンに、オレンジ色のみかん（ヘタ・緑の葉っぱ付き）がちょこんと乗る愛らしいアクセサリーを自動装着（弾むポップイン付き）。
        - **お返し**: 大盤振る舞いの潤滑油（+26〜45）をプレゼント。相棒Rustも「カピタの頭にみかんが乗ったよ！かわいい…！」と大喜びで360度宙返り（Celebration）して一緒に喜ぶ。

36. **浅瀬の小魚の群れ・熱帯魚＆サンゴ礁・ヤドカリの海辺生態系システム（2026-09-26追加）**:
    - [AdventureBeachEcosystem.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachEcosystem.cs)
    - [AdventureSchoolingFish.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureSchoolingFish.cs)
    - [AdventureHermitCrab.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureHermitCrab.cs)
    - **機能概要**:
      - **1. 浅瀬の小魚の群れ（Schools of Minnows）**:
        - 浅瀬3箇所（約40匹）に銀白色・メタリック光沢の小魚の群れが回遊。
        - 尾びれを左右にフリフリとスイングしながら群れで滑らかに泳ぐ。
        - Nikoが4.2m以内に近づくと、ピシャッと微細な飛沫を上げて外海・深海へ一斉にダッシュ逃走（Flee）。危険が去ると穏やかに戻る。
      - **2. 優雅な熱帯魚（Tropical Fish）**:
        - サンゴ礁の周りを回遊するキイロハギ（レモンイエロー）、ナンヨウハギ（コバルトブルー＆黄色尾びれ）、ツノダシ（白黄黒ストライプ）。
        - ふわりと上下に浮遊しながらパトロール。
      - **3. サンゴ礁＆揺れる海草（Corals & Seaweed）**:
        - 桃色・瑠璃色のテーブルサンゴ、パイプサンゴ。
        - 水流に合わせてゆらゆらと波打つ緑の海草（KelpRibbon）。
        - 透き通る海底コースティクスと完璧に調和。
      - **4. 波打ち際の小さなヤドカリ（Hermit Crabs）**:
        - スパイラル巻貝の殻を背負った愛らしい朱色のヤドカリが濡れ砂をチョコチョコ横歩き。
        - Nikoが走って近づく（1.8m以内）と、「コロンッ！」と足を引っ込めて殻の中に隠れる。
        - 立ち止まって見守ると、おそるおそる黒い目玉とハサミを出して周囲を確認し、再び歩き出す。

37. **情緒的な時間の移ろい（夕焼けマジックアワー〜満天の星空・夜光虫・発光ホタル）システムの実装（2026-09-26追加）**:
    - [AdventureDayNightDirector.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureDayNightDirector.cs)
    - [AdventureStarrySky.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureStarrySky.cs)
    - [AdventureBeachVisualEnhancer.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureBeachVisualEnhancer.cs)
    - [AdventureFireflies.cs](file:///Users/user/Unity%20project/RustAndFloat/Assets/Game/Scripts/AdventureFireflies.cs)
    - **機能概要**:
      - **1. リアルタイム時間進行＆【Tキー】トグル切り替え**:
        - 【Tキー】を押すごとに `[探索連動] → [昼☀️] → [夕暮れマジックアワー🌅] → [満天の星空🌌] → [自然時間サイクル⏳（約8分/日）]` を瞬時にトグル切り替え可能。
        - 画面上部トースト通知で現在の時間帯モードを一目で案内。
      - **2. 黄金と茜色の夕暮れマジックアワー（Sunset / Golden Hour）**:
        - 太陽光が水平線近くへ沈み、鮮烈な茜色（オレンジ〜深紅〜黄金）へと徐変。
        - 浅瀬コースティクスと白波が温かい夕暮れ光に染まり、島全体が息をのむ美しさに包まれる。
      - **3. 満天の星空・天の川・月光ドーム（Starry Sky & Moon）**:
        - 650個の瞬く星々（シリウス風の青白星、カペラ風の黄金星、純白の一等星）と帯状の天の川が頭上一面に広がる。
        - 東の空に柔らかな月光を放つ月が静かに昇り、カメラ見上げ時に壮大な宇宙感を演出。
      - **4. 波打ち際の神秘的な夜光虫（Bioluminescent Waves & Footsteps）**:
        - 夜間になると、寄せては返す波頭（Shoreline Wave）が妖艶なネオンシアン（`Color(0.18f, 0.95f, 1.0f) * 1.6f`）に青白く発光。
        - Nikoが夜の濡れ砂を踏みしめて歩くと、足元から小さな青白い夜光虫スパークル（発光飛沫）がポワンと舞い散る。
      - **5. 水辺・ヤシの木・草むらに舞う発光ホタル（Fireflies）**:
        - 夕暮れ〜夜になると、ヤシの木林や水辺、草むらに黄緑色・エメラルド・黄金の光を点滅させながらフワフワと舞うホタルの群れが自然発生。

## 次の推奨タスク

1. **相棒Rustのドローン視点フォトモード（思い出アルバム撮影機能）**:
   - 【Pキー】でRustの目線カメラに切り替え。自由アングル、被写界深度（ボケ）、フィルター、ズームで記念撮影。
2. **カピタの温泉・水浴びエリア（浅瀬や段々池でプカプカ浮かぶカピタ）**:
   - 浅瀬や温泉池でカピタたちが気持ちよさそうに目を細めてプカプカ浮かんで休む癒やしスポット。
3. **海辺の波音・風・生き物たちの環境環境音（オーディオ空間音響）**:
   - 寄せては返す潮騒、風のざわめき、ウミネコの鳴き声、夜の虫の音。

## ブランチ

`main`
リモート: `https://github.com/Yosie-lab/AdventureWorld.git`
コミット方針: ユーザーが commit / push を明示したときだけ。AdventureWorld の地形と Demo/Loader へのビルド差し替えは入れない。



