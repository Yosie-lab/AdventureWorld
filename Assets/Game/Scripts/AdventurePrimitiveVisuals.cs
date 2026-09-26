using UnityEngine;

public static class AdventurePrimitiveVisuals
{
    public static Material CreateLitMaterial(Color color, bool emissive = false)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (emissive)
        {
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.8f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        return mat;
    }

    public static Material CreateWaterMaterial(Color waterColor)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.color = waterColor;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", waterColor);

        // 透明設定
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f); // Transparent for URP
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f); // Alpha blend for URP

        mat.SetFloat("_Mode", 3); // Transparent for Standard
        mat.SetInt("_SrcBlend", 5);
        mat.SetInt("_DstBlend", 10);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.92f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.10f);

        return mat;
    }

    public static void ApplyLitColor(Renderer renderer, Color color, bool emissive = false)
    {
        if (renderer == null)
            return;
        renderer.material = CreateLitMaterial(color, emissive);
    }

    public static void ApplyWood(Renderer renderer, Color color, float smoothness = 0.12f)
    {
        if (renderer == null)
            return;

        var mat = CreateLitMaterial(color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        renderer.material = mat;
    }

    public static void FixAnimalMaterials()
    {
        foreach (var rend in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (rend == null)
                continue;

            var mat = rend.material;
            if (mat == null)
                continue;

            string name = mat.name;
            if (name.Contains("Sparrow") || name.Contains("Colobus") || name.Contains("Gecko") ||
                name.Contains("Pudu") || name.Contains("Muskrat"))
            {
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", Color.white);
            }
        }
    }
}
