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

    public static void ApplyLitColor(Renderer renderer, Color color, bool emissive = false)
    {
        if (renderer == null)
            return;
        renderer.material = CreateLitMaterial(color, emissive);
    }
}
