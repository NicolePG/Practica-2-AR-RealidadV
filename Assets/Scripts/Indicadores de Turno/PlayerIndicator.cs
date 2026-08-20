using UnityEngine;

/// <summary>
/// Munequito 3D que marca el sitio de un jugador alrededor de la torre.
/// Se ilumina cuando le toca el turno y se apaga cuando no.
/// </summary>
public class PlayerIndicator : MonoBehaviour
{
    public Renderer[] renderers;

    public Color normalColor = Color.gray;
    public Color activeColor = Color.white;

    public float normalEmission = 0f;
    public float activeEmission = 2f;

    private void Reset()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
    }

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

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_EmissionColor"))
            {
                // Sin la keyword el shader de URP ignora la emision.
                if (emission > 0f) material.EnableKeyword("_EMISSION");
                else material.DisableKeyword("_EMISSION");

                material.SetColor("_EmissionColor", color * emission);
            }
        }
    }
}
