using UnityEngine;

public partial class AdventureRustDrone
{
    private float _lastTurnCheerTime = -30f;
    private float _lastHighDiveCheerTime = -30f;
    private float _lastSkimmingCheerTime = -30f;
    private float _prevGliderYaw = 0f;

    /// <summary>
    /// 相棒Rustのリアルタイム・シンクロ相槌＆歓声（Companion Connection ➔ 100点満点）
    /// 高空ダイブ・急旋回・海面スキミング・レッジクライム等に自然に応答
    /// </summary>
    public void UpdateRealtimeCompanionReactions()
    {
        var player = AdventurePlayerController.Instance;
        if (player == null) return;

        if (!player.IsGliding)
        {
            _prevGliderYaw = player.transform.eulerAngles.y;
            return;
        }

        float now = Time.time;
        Vector3 pos = player.transform.position;

        // 1. 急旋回（バンクターン）時の歓声
        float curYaw = player.transform.eulerAngles.y;
        float yawDelta = Mathf.Abs(Mathf.DeltaAngle(_prevGliderYaw, curYaw)) / Mathf.Max(Time.deltaTime, 0.001f);
        _prevGliderYaw = curYaw;

        if (yawDelta > 65f && now > _lastTurnCheerTime + 22f && !HasActiveSpeech)
        {
            _lastTurnCheerTime = now;
            string[] turnLines = new string[]
            {
                "お見事なターン！くるっと風を切ったね！",
                "ピピッ♪ 綺麗なバンク！風のつかみ方、最高だよ！",
                "すごい旋回！翼の傾きがピッタリ合ってるね！"
            };
            SpeakCustom(turnLines[Random.Range(0, turnLines.Length)], 3.5f);
            return;
        }

        // 2. 超高空ダイブ開始時（高度70m以上かつ下向き速度 < -11m/s）
        if (pos.y > 70f && player.VerticalSpeed < -11f && now > _lastHighDiveCheerTime + 26f && !HasActiveSpeech)
        {
            _lastHighDiveCheerTime = now;
            string[] diveLines = new string[]
            {
                "わああぁぁっ、大空へダイブだー！！",
                "すごいスピード！風が叫んでるよ、Float！",
                "ピキーッ！一気に雲を突き抜けるよーー！！"
            };
            SpeakCustom(diveLines[Random.Range(0, diveLines.Length)], 3.8f);
            return;
        }

        // 3. 海面スキミング（海抜 < 8.0m での超低空高速滑走）
        if (pos.y < 8.0f && player.HorizontalSpeed > 10f && now > _lastSkimmingCheerTime + 24f && !HasActiveSpeech)
        {
            _lastSkimmingCheerTime = now;
            string[] skimLines = new string[]
            {
                "海がすぐそこ！水しぶきがキラキラ光ってるよ！",
                "水鳥みたいだね、Float！波の音が心地いい〜！",
                "ピピッ！海面スレスレのクルーズ、大成功だね♪"
            };
            SpeakCustom(skimLines[Random.Range(0, skimLines.Length)], 3.6f);
            return;
        }
    }
}
