using UnityEngine;

public class PlayerIndicator : MonoBehaviour
{
    public Renderer[] renderers;

    public Color normalColor = Color.gray;
    public Color activeColor = Color.white;

    public float normalEmission = 0f;
    public float activeEmission = 2f;

    public void SetActive(bool active)
    {
        Color color = active ? activeColor : normalColor;
        float emission = active ? activeEmission : normalEmission;

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            Material material = rend.material;

            material.color = color;

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    color * emission
                );
            }
        }
    }
}