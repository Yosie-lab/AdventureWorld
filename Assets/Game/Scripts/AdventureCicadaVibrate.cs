using UnityEngine;

/// <summary>
/// 濃い緑の木の幹に止まっている蝉のアニメーションコンポーネント。
/// 時折翅を小刻みに震わせたり、呼吸のように体を微動させて生命感を演出する。
/// </summary>
public class AdventureCicadaVibrate : MonoBehaviour
{
    Transform wingsTrans;
    Vector3 basePos;
    Quaternion baseRot;
    float nextBuzzTime;
    float buzzDuration;
    float buzzTimer;
    bool isBuzzing;

    void Start()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        wingsTrans = transform.Find("Wings");
        nextBuzzTime = Time.time + Random.Range(1.5f, 6.0f);
    }

    void Update()
    {
        float t = Time.time;

        // 呼吸のようなわずかな体の上下微動
        float breathe = Mathf.Sin(t * 2.5f) * 0.002f;
        transform.localPosition = basePos + transform.forward * breathe;

        // 定期的に発生する「ジジッ…」という羽の微振動
        if (!isBuzzing && t >= nextBuzzTime)
        {
            isBuzzing = true;
            buzzDuration = Random.Range(0.8f, 2.2f);
            buzzTimer = 0f;
        }

        if (isBuzzing)
        {
            buzzTimer += Time.deltaTime;
            float vibration = Mathf.Sin(buzzTimer * 75f) * 1.8f;
            transform.localRotation = baseRot * Quaternion.Euler(vibration * 0.5f, 0f, vibration);

            if (wingsTrans != null)
            {
                float wingShake = Mathf.Sin(buzzTimer * 90f) * 3.5f;
                wingsTrans.localRotation = Quaternion.Euler(wingShake, 0f, 0f);
            }

            if (buzzTimer >= buzzDuration)
            {
                isBuzzing = false;
                transform.localRotation = baseRot;
                if (wingsTrans != null)
                    wingsTrans.localRotation = Quaternion.identity;
                nextBuzzTime = t + Random.Range(2.0f, 7.5f);
            }
        }
    }
}
