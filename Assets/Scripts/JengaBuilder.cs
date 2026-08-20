using UnityEngine;

// Colocá este script en un GameObject vacío (por ejemplo "JengaTower")
// y asignale un Material base en el Inspector (uno cualquiera, con Shader
// Standard o URP/Lit). El script genera toda la torre y le pone un color
// distinto a cada bloque.
public class JengaBuilder : MonoBehaviour
{
    [Header("Bloque")]
    public Vector3 blockSize = new Vector3(1.5f, 0.5f, 4.5f); // ancho, alto, largo
    public Material baseMaterial;                              // material base (mismo shader para todos)

    [Header("Torre")]
    public int layers = 18;          // pisos
    public int blocksPerLayer = 3;   // bloques por piso
    public float gap = 0.02f;        // separacion chica entre bloques

    [Header("Colores")]
    public bool randomColors = true;
    public Gradient colorByHeight;   // si randomColors = false, usa este degradado por altura

    [Header("AR / Vuforia")]
    [Tooltip("Si esta tildado, arma la torre apenas empieza el juego (sin AR). " +
             "Destildalo si vas a llamar BuildTower() desde el evento de Vuforia (Target Found).")]
    public bool buildOnStart = true;

    private bool alreadyBuilt = false;

    void Start()
    {
        if (buildOnStart) BuildTower();
    }

    // Llamá este metodo publico desde tu handler de Vuforia (OnTargetFound / OnObserverStatusChanged)
    // para que la torre aparezca recien cuando la camara detecta el Image Target.
    public void BuildTower()
    {
        if (alreadyBuilt) return; // evita duplicar la torre si el target se pierde y se vuelve a detectar
        alreadyBuilt = true;

        for (int layer = 0; layer < layers; layer++)
        {
            bool rotated = layer % 2 == 1; // pisos alternados 90 grados
            float y = layer * (blockSize.y + gap);

            for (int i = 0; i < blocksPerLayer; i++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Block_L{layer}_{i}";
                block.transform.parent = transform;

                float offset = (i - (blocksPerLayer - 1) / 2f) * (blockSize.x + gap);

                if (!rotated)
                {
                    block.transform.localPosition = new Vector3(offset, y, 0f);
                    block.transform.localScale = blockSize;
                }
                else
                {
                    block.transform.localPosition = new Vector3(0f, y, offset);
                    block.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    block.transform.localScale = blockSize;
                }

                // Fisica
                Rigidbody rb = block.AddComponent<Rigidbody>();
                rb.mass = 1f;

                // Color por bloque sin crear un Material nuevo por objeto
                Renderer rend = block.GetComponent<Renderer>();
                if (baseMaterial != null) rend.sharedMaterial = baseMaterial;

                Color c = randomColors
                    ? Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.7f, 1f)
                    : colorByHeight.Evaluate((float)layer / layers);

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c); // URP/HDRP
                mpb.SetColor("_Color", c);     // Built-in Standard
                rend.SetPropertyBlock(mpb);
            }
        }
    }
}
