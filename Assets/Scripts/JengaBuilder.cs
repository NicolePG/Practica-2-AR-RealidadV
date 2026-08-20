using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera una torre de Jenga anclada a un ImageTarget de Vuforia.
///
/// IMPORTANTE (AR): los Rigidbody viven en coordenadas de MUNDO y no siguen
/// a su transform padre. Como Vuforia reposiciona el ImageTarget en cada
/// frame, dejar la torre colgando de el hace que los bloques dinamicos se
/// desincronicen del bloque base kinematico y la torre se "hunda".
/// Por eso, apenas se construye, la torre se DESENGANCHA del marcador y
/// queda plantada en coordenadas de mundo.
/// </summary>
public class JengaBuilder : MonoBehaviour
{
    [Header("Bloque (en unidades de Unity)")]
    [Tooltip("Proporciones de un bloque de Jenga real (ancho, alto, largo). " +
             "El largo debe ser 3x el ancho para que la capa quede cuadrada.")]
    public Vector3 blockSize = new Vector3(0.15f, 0.09f, 0.45f);

    [Tooltip("Material base. Asignalo SIEMPRE: si queda vacio los bloques " +
             "pueden salir magenta en el build de URP.")]
    public Material baseMaterial;

    [Header("Torre")]
    public int layers = 18;
    public int blocksPerLayer = 3;
    [Tooltip("Separacion entre bloques. Dejar en 0 para maxima estabilidad.")]
    public float gap = 0f;

    [Header("Colores")]
    public bool randomColors = true;
    public Gradient colorByHeight;

    [Header("Fisica - Cuerpo")]
    [Tooltip("Masa de cada bloque. Valores muy bajos (<0.1) vuelven inestable el solver.")]
    public float blockMass = 0.5f;

    [Tooltip("Friccion dinamica. La madera real ronda 0.5 - 0.7.")]
    [Range(0f, 1f)] public float dynamicFriction = 0.6f;

    [Tooltip("Friccion estatica. Un poco mayor que la dinamica.")]
    [Range(0f, 1f)] public float staticFriction = 0.7f;

    [Tooltip("Iteraciones del solver POR BLOQUE. El default global es 6.")]
    [Range(6, 60)] public int solverIterations = 30;

    [Tooltip("Iteraciones de velocidad POR BLOQUE. El default global es 1.")]
    [Range(1, 20)] public int solverVelocityIterations = 10;

    [Tooltip("Limite de giro. El default de Unity (50) permite que un bloque " +
             "salga disparado girando y arrastre a los vecinos.")]
    public float maxAngularVelocity = 7f;

    [Tooltip("Friccion del bloque MIENTRAS lo estas sacando. Casi cero a " +
             "proposito: con la friccion normal el bloque se lleva por " +
             "rozamiento a sus vecinos de arriba y abajo, y parece que se " +
             "moviera la torre entera.")]
    [Range(0f, 0.5f)] public float extractionFriction = 0.02f;

    [Tooltip("Rozamiento del aire. Amortigua los empujoncitos que se " +
             "transmiten al sacar una pieza sin volver flotante la caida.")]
    [Range(0f, 2f)] public float linearDamping = 0.15f;

    [Tooltip("Rozamiento del aire para el giro. Evita que un bloque quede " +
             "trompeando sobre la torre.")]
    [Range(0f, 5f)] public float angularDamping = 0.8f;

    [Header("Fisica - Estabilidad")]
    [Tooltip("Los bloques nacen congelados (isKinematic).")]
    public bool startFrozen = true;

    [Tooltip("Ancla el piso 0 permanentemente. Hace las veces de mesa.")]
    public bool anchorBaseLayer = true;

    [Tooltip("Tras liberar, manda los bloques a dormir. Un cuerpo dormido no " +
             "se simula: no acumula error ni vibra, pero despierta al tacto.")]
    public bool sleepAfterRelease = true;

    [Tooltip("Frames de fisica que se dejan correr antes de dormir los bloques.")]
    [Range(0, 30)] public int settleFrames = 2;

    [Tooltip("Crea un piso grueso invisible bajo la torre.")]
    public bool createFloor = true;

    [Header("AR / Vuforia")]
    [Tooltip("CLAVE EN EL CELULAR. Tras construir, desengancha la torre del " +
             "ImageTarget para que la fisica trabaje en coordenadas de mundo. " +
             "Sin esto, el temblor de tracking de Vuforia hunde la torre.")]
    public bool detachFromMarker = true;

    [Tooltip("Endereza la torre segun la gravedad al desengancharla. El eje Y " +
             "de un ImageTarget sale perpendicular a la imagen: si el marcador " +
             "esta en una pared o en la pantalla de un monitor, la torre nace " +
             "acostada y se desarma sola. Con esto siempre crece hacia arriba, " +
             "y sobre una mesa no cambia nada porque ahi ya coincide.")]
    public bool keepUprightWithGravity = true;

    [Tooltip("Se lleva a los hijos que ya cuelgan de este GameObject (los " +
             "munequitos de turno) dentro de la torre, para que la acompanen " +
             "al desengancharla y no queden pegados al marcador.")]
    public bool carryChildrenAlong = true;

    [Tooltip("Segundos de espera entre construir y liberar la fisica. " +
             "Da tiempo a que todos los bloques existan antes del primer " +
             "frame de simulacion. En el celular hace falta mas que en el PC.")]
    public float autoReleaseDelay = 0.5f;

    [Tooltip("Si esta tildado, libera la fisica sola despues de construir. " +
             "Dejalo en TRUE para el build de Android: ahi no existe el menu " +
             "del Inspector para llamar a ReleasePhysics a mano.")]
    public bool autoRelease = true;

    [Tooltip("Dejar en FALSE cuando se usa con Vuforia.")]
    public bool buildOnStart = false;

    // ---- acceso publico para JengaInteraction y GameManager ----
    public Transform BlocksRoot => blocksRoot;

    /// <summary>La torre ya esta levantada (o levantandose).</summary>
    public bool IsBuilt => alreadyBuilt;

    private bool alreadyBuilt = false;
    private Transform blocksRoot;
    private PhysicsMaterial woodMat;
    private PhysicsMaterial slipperyMat;

    /// <summary>Material normal de los bloques (madera con friccion).</summary>
    public PhysicsMaterial WoodMaterial => woodMat;

    /// <summary>Material sin friccion para el bloque que se esta extrayendo.</summary>
    public PhysicsMaterial SlipperyMaterial => slipperyMat;

    // Hijos del marcador (los munequitos) que viajan con la torre.
    private readonly List<Transform> carried = new List<Transform>();

    void Start()
    {
        if (buildOnStart) BuildTower();
    }

    // ---------------------------------------------------------------
    // Llamar desde el evento "On Target Found ()" de Vuforia
    // ---------------------------------------------------------------
    public void BuildTower()
    {
        if (alreadyBuilt) return;
        alreadyBuilt = true;

        CreateWoodMaterial();

        blocksRoot = new GameObject("JengaBlocks").transform;
        blocksRoot.SetParent(transform, false);

        // Los munequitos de turno pasan a colgar de la torre para viajar con
        // ella. blocksRoot esta en identidad respecto de este transform, asi
        // que mover con worldPositionStays=false no les cambia la pose.
        carried.Clear();
        if (carryChildrenAlong)
        {
            foreach (Transform child in transform)
            {
                if (child != blocksRoot) carried.Add(child);
            }

            foreach (Transform child in carried)
            {
                child.SetParent(blocksRoot, false);

                // Los munequitos son decorativos y vienen de primitivas, o sea
                // con collider. Como colliders estaticos dentro de un padre que
                // se mueve y gira obligan a PhysX a rehacerlos todo el tiempo,
                // y encima el rayo que busca la cima de la torre puede pegarles.
                foreach (Collider c in child.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }
        }

        if (createFloor) CreateFloor();

        float layerHeight = blockSize.y + gap;

        for (int layer = 0; layer < layers; layer++)
        {
            bool rotated = layer % 2 == 1;
            float y = layer * layerHeight + blockSize.y * 0.5f;

            for (int i = 0; i < blocksPerLayer; i++)
            {
                CreateBlock(layer, i, y, rotated);
            }
        }

        // --- DESENGANCHE: la torre pasa a coordenadas de mundo ---
        // true = conserva la posicion/rotacion/escala que tiene ahora
        if (detachFromMarker)
        {
            blocksRoot.SetParent(null, true);

            // Enderezado: se gira la torre sobre su propia base hasta que su
            // eje Y apunte al cielo. Todos los bloques siguen kinematicos en
            // este punto, asi que moverlos no altera la simulacion.
            if (keepUprightWithGravity)
            {
                blocksRoot.rotation = Quaternion.FromToRotation(blocksRoot.up, Vector3.up)
                                      * blocksRoot.rotation;
            }
        }

        if (autoRelease) StartCoroutine(AutoReleaseRoutine());
    }

    private IEnumerator AutoReleaseRoutine()
    {
        // Espera a que la torre exista entera y el tracking se estabilice
        yield return new WaitForSeconds(autoReleaseDelay);
        ReleasePhysics();
    }

    // ---------------------------------------------------------------
    // Material fisico compartido: friccion tipo madera, sin rebote
    // ---------------------------------------------------------------
    private void CreateWoodMaterial()
    {
        woodMat = new PhysicsMaterial("Wood");
        woodMat.dynamicFriction = dynamicFriction;
        woodMat.staticFriction = staticFriction;
        woodMat.bounciness = 0f;
        // Minimum en lugar de Maximum: entre dos bloques da lo mismo porque
        // los dos tienen la misma friccion, pero deja que un bloque suelto
        // imponga su propia friccion baja mientras se lo esta extrayendo.
        woodMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        woodMat.bounceCombine = PhysicsMaterialCombine.Minimum;

        slipperyMat = new PhysicsMaterial("WoodSliding");
        slipperyMat.dynamicFriction = extractionFriction;
        slipperyMat.staticFriction = extractionFriction;
        slipperyMat.bounciness = 0f;
        slipperyMat.frictionCombine = PhysicsMaterialCombine.Minimum;
        slipperyMat.bounceCombine = PhysicsMaterialCombine.Minimum;
    }

    private void CreateBlock(int layer, int index, float y, bool rotated)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"Block_L{layer}_{index}";
        block.transform.SetParent(blocksRoot, false);

        float offset = (index - (blocksPerLayer - 1) / 2f) * (blockSize.x + gap);

        if (!rotated)
        {
            block.transform.localPosition = new Vector3(offset, y, 0f);
            block.transform.localRotation = Quaternion.identity;
        }
        else
        {
            block.transform.localPosition = new Vector3(0f, y, offset);
            block.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        block.transform.localScale = blockSize;

        // --- Collider con friccion ---
        BoxCollider col = block.GetComponent<BoxCollider>();
        col.material = woodMat;

        // --- Rigidbody ---
        Rigidbody rb = block.AddComponent<Rigidbody>();
        rb.mass = blockMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.solverIterations = solverIterations;
        rb.solverVelocityIterations = solverVelocityIterations;
        rb.maxAngularVelocity = maxAngularVelocity;

        bool isAnchor = anchorBaseLayer && layer == 0;
        rb.isKinematic = isAnchor || startFrozen;
        if (isAnchor) block.name += "_ANCHOR";

        // --- Color ---
        Renderer rend = block.GetComponent<Renderer>();
        if (baseMaterial != null) rend.sharedMaterial = baseMaterial;

        Color c = randomColors
            ? Random.ColorHSV(0f, 1f, 0.5f, 0.9f, 0.7f, 1f)
            : colorByHeight.Evaluate((float)layer / Mathf.Max(1, layers));

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c); // URP / HDRP
        mpb.SetColor("_Color", c);     // Built-in Standard
        rend.SetPropertyBlock(mpb);
    }

    private void CreateFloor()
    {
        GameObject floor = new GameObject("Floor");
        floor.transform.SetParent(blocksRoot, false);

        BoxCollider col = floor.AddComponent<BoxCollider>();
        col.material = woodMat;

        float side = (blockSize.z + gap) * 3f;
        col.size = new Vector3(side, 0.2f, side);
        col.center = new Vector3(0f, -0.1f, 0f);
    }

    // ---------------------------------------------------------------
    // Libera la fisica y duerme los bloques.
    // ---------------------------------------------------------------
    [ContextMenu("Release Physics")]
    public void ReleasePhysics()
    {
        if (blocksRoot == null) return;
        StopAllCoroutines();
        StartCoroutine(ReleaseRoutine());
    }

    private IEnumerator ReleaseRoutine()
    {
        foreach (Rigidbody rb in blocksRoot.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.name.EndsWith("_ANCHOR")) continue;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        for (int i = 0; i < settleFrames; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        if (sleepAfterRelease)
        {
            foreach (Rigidbody rb in blocksRoot.GetComponentsInChildren<Rigidbody>())
            {
                if (rb.isKinematic) continue;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }
        }
    }

    [ContextMenu("Wake Up Tower")]
    public void WakeUpTower()
    {
        if (blocksRoot == null) return;

        foreach (Rigidbody rb in blocksRoot.GetComponentsInChildren<Rigidbody>())
        {
            if (!rb.isKinematic) rb.WakeUp();
        }
    }

    [ContextMenu("Freeze Physics")]
    public void FreezePhysics()
    {
        if (blocksRoot == null) return;
        StopAllCoroutines();

        foreach (Rigidbody rb in blocksRoot.GetComponentsInChildren<Rigidbody>())
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    public void ResetTower()
    {
        StopAllCoroutines();

        // Rescatar los munequitos: cuelgan de blocksRoot y se irian con el.
        foreach (Transform child in carried)
        {
            if (child != null) child.SetParent(transform, false);
        }
        carried.Clear();

        if (blocksRoot != null) Destroy(blocksRoot.gameObject);
        blocksRoot = null;
        alreadyBuilt = false;
    }
}