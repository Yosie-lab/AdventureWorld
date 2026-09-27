using UnityEngine;
using UnityEditor;

public static class AdventureFixCrabClaws
{
    [MenuItem("Adventure/Crabs/Fix All Crab Claws")]
    public static void FixClaws()
    {
        var crabs = Object.FindObjectsByType<CrabWander>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var crab in crabs)
        {
            Undo.RegisterFullObjectHierarchyUndo(crab.gameObject, "Fix Crab Claws");

            // 甲羅の取得
            Transform shell = crab.transform.Find("Shell");
            Material crabMat = null;
            if (shell != null)
            {
                var rend = shell.GetComponent<Renderer>();
                if (rend != null) crabMat = rend.sharedMaterial;
            }

            if (crabMat == null)
            {
                var rends = crab.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0) crabMat = rends[0].sharedMaterial;
            }

            // 既存の LeftClaw, RightClaw, または古い Arm を探して削除
            Transform oldLeft = crab.transform.Find("LeftClaw");
            if (oldLeft != null) Object.DestroyImmediate(oldLeft.gameObject);

            Transform oldRight = crab.transform.Find("RightClaw");
            if (oldRight != null) Object.DestroyImmediate(oldRight.gameObject);

            Transform oldLeftArm = crab.transform.Find("LeftArm");
            if (oldLeftArm != null) Object.DestroyImmediate(oldLeftArm.gameObject);

            Transform oldRightArm = crab.transform.Find("RightArm");
            if (oldRightArm != null) Object.DestroyImmediate(oldRightArm.gameObject);

            // 新しい胴体直結型ハサミを作成（胴体の斜め前から前方に突き出す）
            for (int side = -1; side <= 1; side += 2)
            {
                string clawName = side < 0 ? "LeftClaw" : "RightClaw";

                // ピボット（関節）：胴体（甲羅）の斜め前縁に配置
                var clawRoot = new GameObject(clawName);
                clawRoot.transform.SetParent(crab.transform, false);
                clawRoot.transform.localPosition = new Vector3(side * 0.085f, 0.055f, 0.055f);
                clawRoot.transform.localRotation = Quaternion.Euler(0f, side * 22f, 0f);

                // 1. 腕（アーム）：胴体からハサミの付け根へ繋ぐ節
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "Arm";
                arm.transform.SetParent(clawRoot.transform, false);
                arm.transform.localScale = new Vector3(0.045f, 0.035f, 0.065f);
                arm.transform.localPosition = new Vector3(side * 0.02f, 0f, 0.028f);
                arm.transform.localRotation = Quaternion.Euler(0f, side * 20f, 0f);
                if (crabMat != null) arm.GetComponent<Renderer>().sharedMaterial = crabMat;
                Object.DestroyImmediate(arm.GetComponent<Collider>());

                // 2. メインのハサミ（親爪）：腕の先端に一体化して接続
                var clawMain = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clawMain.name = "ClawMain";
                clawMain.transform.SetParent(clawRoot.transform, false);
                clawMain.transform.localScale = new Vector3(0.065f, 0.045f, 0.09f);
                clawMain.transform.localPosition = new Vector3(side * 0.038f, 0.005f, 0.075f);
                clawMain.transform.localRotation = Quaternion.Euler(0f, side * 15f, side * -10f);
                if (crabMat != null) clawMain.GetComponent<Renderer>().sharedMaterial = crabMat;
                Object.DestroyImmediate(clawMain.GetComponent<Collider>());

                // 3. ハサミの先端の爪（可動指・ピンサー）：V字に開いた可愛いハサミ
                var pincer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pincer.name = "Pincer";
                pincer.transform.SetParent(clawRoot.transform, false);
                pincer.transform.localScale = new Vector3(0.038f, 0.03f, 0.06f);
                pincer.transform.localPosition = new Vector3(side * 0.022f, 0.005f, 0.115f);
                pincer.transform.localRotation = Quaternion.Euler(0f, side * -25f, 0f);
                if (crabMat != null) pincer.GetComponent<Renderer>().sharedMaterial = crabMat;
                Object.DestroyImmediate(pincer.GetComponent<Collider>());
            }

            EditorUtility.SetDirty(crab.gameObject);
            count++;
        }

        Debug.Log($"Successfully fixed claws for {count} crabs! All claws are now seamlessly attached to bodies.");
    }
}
