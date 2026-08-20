using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Interaccion tactil para el Jenga AR.
///
/// Flujo:
///   1. El jugador toca un bloque -> se selecciona
///   2. Arrastra el dedo -> el bloque sale SOLO a lo largo de su eje largo
///   3. Si lo saca del todo -> vuela POR FUERA de la torre y se apoya arriba
///   4. Si la cima esta muy inclinada -> el bloque se SUELTA y la fisica decide
///   5. Si caen suficientes bloques -> derrota
///
/// Colocar este script en el MISMO GameObject que JengaBuilder (JengaTower).
/// </summary>
public class JengaInteraction : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Si queda vacio lo busca en este mismo GameObject.")]
    public JengaBuilder builder;

    [Tooltip("Camara AR. Si queda vacio usa Camera.main.")]
    public Camera arCamera;

    [Header("Arrastre")]
    [Tooltip("Que fraccion del largo del bloque hay que sacar para que cuente " +
             "como extraido. 0.85 = hay que sacarlo casi entero.")]
    [Range(0.3f, 1.2f)] public float extractThreshold = 0.85f;

    [Tooltip("Cuanto se puede pasar del largo del bloque al arrastrar.")]
    [Range(1f, 2f)] public float maxPullFactor = 1.3f;

    [Tooltip("Multiplicador de sensibilidad. 1 = el bloque sigue al dedo exacto.")]
    [Range(0.2f, 3f)] public float dragSensitivity = 1f;

    [Header("Vuelo hasta el tope")]
    [Tooltip("Segundos totales del viaje del bloque hasta la cima.")]
    public float placeDuration = 0.9f;

    [Tooltip("Cuanto se aleja de la torre antes de subir, en largos de bloque.")]
    [Range(0.5f, 3f)] public float clearanceFactor = 1.2f;

    [Tooltip("Altura extra sobre la cima antes de bajar al hueco.")]
    public float hoverHeight = 0.25f;

    [Tooltip("Busca con un raycast la superficie REAL sobre la que apoyar, en " +
             "lugar de la altura teorica del piso siguiente.")]
    public bool snapToRealSurface = true;

    [Tooltip("Inclinacion maxima (grados) que puede tener la superficie para " +
             "apoyar el bloque con precision. Si la cima esta mas torcida que " +
             "esto, el bloque se SUELTA en vez de apoyarse: es lo que hace " +
             "posible perder por apilar mal.")]
    [Range(0f, 45f)] public float maxSurfaceTilt = 22f;

    [Tooltip("Altura desde la que se suelta el bloque cuando la cima esta " +
             "torcida, medida en alturas de bloque. Chico a proposito: " +
             "soltarlo desde la altura del vuelo revienta la cima y hace que " +
             "cada colocacion deje peor la torre para la siguiente.")]
    [Range(0f, 4f)] public float freeDropHeight = 0.6f;

    [Header("Girar la torre")]
    [Tooltip("Arrastrar el dedo donde NO hay bloque hace girar la torre sobre " +
             "su eje, para poder sacar piezas de cualquier cara sin tener que " +
             "caminar alrededor.")]
    public bool allowRotation = true;

    [Tooltip("Grados de giro por cada 100 pixeles de arrastre.")]
    [Range(30f, 360f)] public float rotationSensitivity = 180f;

    [Tooltip("Pixeles que hay que arrastrar antes de que empiece a girar. " +
             "Evita que un toque suelto mueva la torre sin querer.")]
    [Range(0f, 60f)] public float rotationDeadZone = 12f;

    [Tooltip("Giro maximo por frame, en grados. Un manotazo puede pedir " +
             "cientos de grados en un solo frame: sin tope la torre se " +
             "teletransporta y al soltar los bloques salen disparados.")]
    [Range(2f, 45f)] public float maxRotationStep = 15f;

    [Header("Reglas")]
    [Tooltip("Protege el piso superior completo: en el Jenga real no se puede " +
             "sacar un bloque de la capa mas alta terminada.")]
    public bool protectTopLayer = true;

    [Header("Derrota")]
    [Tooltip("Cuantos bloques tienen que salirse de la torre para perder.")]
    [Range(1, 10)] public int fallenBlocksToLose = 2;

    [Tooltip("Radio permitido alrededor del eje de la torre, en largos de bloque. " +
             "Un bloque mas lejos que esto se considera caido.")]
    [Range(0.5f, 2f)] public float fallRadiusFactor = 0.9f;

    [Tooltip("Inclinacion (grados) a partir de la cual un bloque cuenta como " +
             "descolocado. Un bloque bien apoyado tiene su cara superior " +
             "paralela a la de la torre, o sea 0 grados.")]
    [Range(5f, 60f)] public float maxBlockTilt = 25f;

    [Tooltip("Cuantos bloques descolocados hacen que la torre cuente como " +
             "derrumbada aunque siga en pie. Con friccion alta una pila " +
             "torcida se sostiene sola indefinidamente, y ahi el juego se " +
             "queda sin final.")]
    [Range(1, 20)] public int tiltedBlocksToLose = 5;

    [Tooltip("Cada cuantos segundos se revisa si la torre colapso.")]
    public float collapseCheckInterval = 0.3f;

    [Header("Feedback visual")]
    public bool highlightSelected = true;
    public Color highlightColor = new Color(1f, 1f, 0.6f);

    [Header("Eventos")]
    [Tooltip("Se dispara una sola vez cuando la torre colapsa.")]
    public UnityEvent onTowerCollapsed;

    [Tooltip("Se dispara cada vez que se coloca un bloque arriba con exito.")]
    public UnityEvent onBlockPlaced;

    /// <summary>
    /// El jugador acaba de tomar un bloque. El GameManager lo usa para saber
    /// quien responde por la torre si esta se derrumba.
    /// </summary>
    public event System.Action OnBlockGrabbed;

    /// <summary>
    /// Movimiento rechazado por las reglas (capa superior, bloque anclado,
    /// bloque no extraido del todo...). Lleva el motivo listo para mostrar.
    /// </summary>
    public event System.Action<string> OnInvalidMove;

    // ---- estado publico de solo lectura ----
    public bool IsGameOver => collapsed;
    public int BlocksPlaced => placedCount;
    public bool InputEnabled => inputEnabled;

    /// <summary>Hay un bloque agarrado o volando hacia la cima.</summary>
    public bool MoveInProgress => busy || grabbed != null;

    // ---- estado interno ----
    private Transform blocksRoot;

    private Rigidbody grabbed;
    private Rigidbody flying;
    private Renderer grabbedRend;
    private Color grabbedOriginalColor;
    private Vector3 grabStartPos;
    private Quaternion grabStartRot;
    private Vector3 axisWorld;
    private Vector2 lastPointer;
    private float offset;

    private Collider grabbedCollider;
    private Vector3 dragTarget;
    private bool hasDragTarget;

    private int topLayer;
    private int slotsFilled;
    private int placedCount;
    private bool busy;
    private bool collapsed;
    private float nextCollapseCheck;

    // Mientras esta en false el jugador no puede tocar la torre: es el candado
    // que usa el GameManager para el seguimiento AR y para el menu.
    private bool inputEnabled = true;

    // ---- giro de la torre ----
    private bool rotating;
    private bool rotationPending;      // dedo apoyado fuera de un bloque
    private Vector2 rotationStart;
    private string pendingInvalidReason;
    // Todos los cuerpos de la torre, incluida la base anclada: hay que
    // sincronizarlos a mano con el transform mientras se gira.
    private readonly System.Collections.Generic.List<Rigidbody> rotatingBodies =
        new System.Collections.Generic.List<Rigidbody>();

    // Solo los que estaban dinamicos, para devolverlos a dinamicos al soltar.
    private readonly System.Collections.Generic.List<Rigidbody> frozenForRotation =
        new System.Collections.Generic.List<Rigidbody>();

    private readonly System.Collections.Generic.List<RigidbodyInterpolation> rotationInterpolation =
        new System.Collections.Generic.List<RigidbodyInterpolation>();

    // ---- input ----
    private Vector2 pointerPos;
    private bool pointerDown, pointerHeld, pointerUp;

    void Awake()
    {
        if (builder == null) builder = GetComponent<JengaBuilder>();
        if (arCamera == null) arCamera = Camera.main;
        if (arCamera == null) arCamera = FindAnyObjectByType<Camera>();
    }

    void Update()
    {
        if (blocksRoot == null)
        {
            blocksRoot = builder != null ? builder.BlocksRoot : null;
            if (blocksRoot == null) return;

            topLayer = builder.layers;
            slotsFilled = 0;
        }

        // --- deteccion de derrota, continua y throttleada ---
        if (Time.time >= nextCollapseCheck)
        {
            nextCollapseCheck = Time.time + collapseCheckInterval;
            CheckCollapse();
        }

        if (collapsed) return;

        // Regla: la manipulacion solo se habilita cuando el seguimiento AR
        // es estable. El candado lo abre y cierra el GameManager.
        if (!inputEnabled) return;

        ReadPointer();

        if (busy) return;

        if (grabbed != null)
        {
            if (pointerHeld) DragBlock();
            if (pointerUp) ReleaseBlock();
            return;
        }

        if (rotating)
        {
            if (pointerHeld) RotarTorre();
            if (pointerUp) TerminarRotacion();
            return;
        }

        if (rotationPending)
        {
            // Todavia no se sabe si es un toque o un arrastre para girar.
            if (pointerUp)
            {
                rotationPending = false;
                if (!string.IsNullOrEmpty(pendingInvalidReason))
                {
                    OnInvalidMove?.Invoke(pendingInvalidReason);
                    pendingInvalidReason = null;
                }
            }
            else if (pointerHeld &&
                     Mathf.Abs(pointerPos.x - rotationStart.x) >= rotationDeadZone)
            {
                pendingInvalidReason = null;   // fue un arrastre, no un toque
                IniciarRotacion();
            }
            return;
        }

        if (pointerDown && !PointerSobreUI() && !TryGrab() && allowRotation)
        {
            rotationPending = true;
            rotationStart = pointerPos;
            lastPointer = pointerPos;
        }
    }

    // ===============================================================
    // GIRAR LA TORRE
    //
    // Los Rigidbody viven en coordenadas de mundo y no siguen a su padre:
    // si se gira la raiz con la fisica activa, PhysX pelea contra el giro
    // y la torre se desarma sola. Por eso durante el giro los bloques
    // quedan kinematicos y se sueltan al levantar el dedo.
    // ===============================================================

    private void IniciarRotacion()
    {
        rotationPending = false;
        rotating = true;

        rotatingBodies.Clear();
        frozenForRotation.Clear();
        rotationInterpolation.Clear();

        foreach (Rigidbody rb in blocksRoot.GetComponentsInChildren<Rigidbody>())
        {
            rotatingBodies.Add(rb);

            // La interpolacion reescribe la pose desde el ultimo paso de
            // fisica y pelearia contra el giro del padre.
            rotationInterpolation.Add(rb.interpolation);
            rb.interpolation = RigidbodyInterpolation.None;

            if (rb.isKinematic) continue;      // la base anclada ya lo es

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            frozenForRotation.Add(rb);
        }
    }

    private void RotarTorre()
    {
        float dx = pointerPos.x - lastPointer.x;
        lastPointer = pointerPos;

        if (Mathf.Approximately(dx, 0f)) return;

        // Signo negativo: arrastrar a la derecha lleva hacia la derecha la
        // cara que estas mirando, como si giraras un plato.
        float grados = -dx / 100f * rotationSensitivity;
        grados = Mathf.Clamp(grados, -maxRotationStep, maxRotationStep);

        blocksRoot.RotateAround(blocksRoot.position, blocksRoot.up, grados);

        // La jerarquia ya movio los transforms, pero PhysX no se entera: el
        // Rigidbody es dueno de su pose y conserva la anterior. Sin esta
        // sincronizacion los bloques se van despegando del padre a medida que
        // gira, la torre real deja de coincidir con el sistema de coordenadas
        // desde el que se calcula donde apoyar la pieza siguiente, y al girar
        // rapido la deriva es tanta que los bloques terminan superpuestos.
        foreach (Rigidbody rb in rotatingBodies)
        {
            if (rb == null) continue;

            rb.position = rb.transform.position;
            rb.rotation = rb.transform.rotation;
        }
    }

    private void TerminarRotacion()
    {
        rotating = false;

        for (int i = 0; i < rotatingBodies.Count; i++)
        {
            Rigidbody cuerpo = rotatingBodies[i];
            if (cuerpo != null) cuerpo.interpolation = rotationInterpolation[i];
        }

        foreach (Rigidbody rb in frozenForRotation)
        {
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Sin Sleep(): dormirlos aca congelaba la torre en la pose que
            // tuviera, aunque fuera imposible. Despiertos, la fisica termina
            // de acomodarlos o los tira, que es lo que corresponde.
        }

        rotatingBodies.Clear();
        frozenForRotation.Clear();
        rotationInterpolation.Clear();
    }

    // ===============================================================
    // CANDADO DE ENTRADA
    // ===============================================================

    /// <summary>
    /// Habilita o bloquea la manipulacion. Si se bloquea con un bloque en la
    /// mano, el bloque vuelve a su hueco: el movimiento queda anulado y el
    /// turno no avanza.
    /// </summary>
    public void SetInputEnabled(bool value)
    {
        if (inputEnabled == value) return;

        inputEnabled = value;

        if (value) return;

        if (grabbed != null) DevolverBloqueAgarrado();
        if (rotating) TerminarRotacion();

        rotationPending = false;
        pendingInvalidReason = null;
    }

    private void DevolverBloqueAgarrado()
    {
        RestaurarFriccion();

        grabbed.transform.SetPositionAndRotation(grabStartPos, grabStartRot);
        grabbed.isKinematic = false;
        grabbed.linearVelocity = Vector3.zero;
        grabbed.angularVelocity = Vector3.zero;
        grabbed.Sleep();

        if (highlightSelected) RestoreColor();

        grabbed = null;
        grabbedRend = null;
        offset = 0f;
        hasDragTarget = false;
    }

    /// <summary>Devuelve al bloque su friccion de madera.</summary>
    private void RestaurarFriccion()
    {
        if (grabbedCollider != null && builder.WoodMaterial != null)
        {
            grabbedCollider.sharedMaterial = builder.WoodMaterial;
        }

        grabbedCollider = null;
    }

    /// <summary>
    /// Un toque sobre un boton del HUD no debe agarrar bloques por detras.
    /// </summary>
    private bool PointerSobreUI()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
            return es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
#else
        if (Input.touchCount > 0)
            return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif

        return es.IsPointerOverGameObject();
    }

    // ===============================================================
    // AGARRAR
    // ===============================================================
    /// <returns>true si se agarro un bloque.</returns>
    private bool TryGrab()
    {
        pendingInvalidReason = null;

        Ray ray = arCamera.ScreenPointToRay(pointerPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;

        Rigidbody rb = hit.rigidbody;
        if (rb == null) return false;                 // el piso invisible no tiene Rigidbody

        // El motivo no se anuncia todavia: si el dedo termina arrastrando, la
        // intencion era girar la torre y el aviso seria ruido.
        if (rb.isKinematic)                           // base anclada
        {
            pendingInvalidReason = "Ese bloque es la base: no se puede mover.";
            return false;
        }

        if (!IsGrabbable(rb.name, out string motivo))
        {
            pendingInvalidReason = motivo;
            return false;
        }

        grabbed = rb;
        grabStartPos = rb.transform.position;
        grabStartRot = rb.transform.rotation;

        // El eje largo del bloque es su Z local (blockSize.z es el mayor)
        axisWorld = rb.transform.forward;

        offset = 0f;
        lastPointer = pointerPos;
        hasDragTarget = false;

        grabbed.linearVelocity = Vector3.zero;
        grabbed.angularVelocity = Vector3.zero;
        grabbed.isKinematic = true;

        // Mientras sale, el bloque no roza: si no, arrastra por friccion a
        // los vecinos de arriba y abajo y parece moverse la torre entera.
        grabbedCollider = grabbed.GetComponent<Collider>();
        if (grabbedCollider != null && builder.SlipperyMaterial != null)
        {
            grabbedCollider.sharedMaterial = builder.SlipperyMaterial;
        }

        if (highlightSelected) ApplyHighlight();

        OnBlockGrabbed?.Invoke();
        return true;
    }

    private bool IsGrabbable(string blockName, out string motivo)
    {
        motivo = null;

        int layer = ParseLayer(blockName);
        if (layer < 0) return false;                  // no es un bloque de la torre

        if (layer == 0)
        {
            motivo = "Ese bloque es la base: no se puede mover.";
            return false;
        }

        // Regla: no se pueden retirar bloques del nivel superior.
        if (protectTopLayer && layer >= topLayer - 1)
        {
            motivo = "No se pueden retirar bloques del nivel superior.";
            return false;
        }

        return true;
    }

    private int ParseLayer(string blockName)
    {
        // formato: Block_L{layer}_{index}
        int start = blockName.IndexOf("_L");
        if (start < 0) return -1;
        start += 2;

        int end = blockName.IndexOf('_', start);
        if (end < 0) return -1;

        return int.TryParse(blockName.Substring(start, end - start), out int n) ? n : -1;
    }

    // ===============================================================
    // ARRASTRAR
    // ===============================================================
    private void DragBlock()
    {
        Vector3 s0 = arCamera.WorldToScreenPoint(grabStartPos);
        Vector3 s1 = arCamera.WorldToScreenPoint(grabStartPos + axisWorld);

        Vector2 screenAxis = (Vector2)(s1 - s0);
        float pixelsPerUnit = screenAxis.magnitude;
        if (pixelsPerUnit < 0.01f) return;            // eje apuntando a la camara

        screenAxis /= pixelsPerUnit;

        Vector2 delta = pointerPos - lastPointer;
        lastPointer = pointerPos;

        offset += Vector2.Dot(delta, screenAxis) / pixelsPerUnit * dragSensitivity;

        float maxPull = builder.blockSize.z * maxPullFactor;
        offset = Mathf.Clamp(offset, -maxPull, maxPull);

        // MovePosition pertenece al paso de fisica: aca solo se anota el
        // destino y FixedUpdate lo aplica, que es lo que evita el tironeo.
        dragTarget = grabStartPos + axisWorld * offset;
        hasDragTarget = true;
    }

    void FixedUpdate()
    {
        if (grabbed != null && hasDragTarget) grabbed.MovePosition(dragTarget);
    }

    // ===============================================================
    // SOLTAR
    // ===============================================================
    private void ReleaseBlock()
    {
        float needed = builder.blockSize.z * extractThreshold;

        if (Mathf.Abs(offset) >= needed)
        {
            Vector3 outDir = axisWorld * Mathf.Sign(offset);
            RestaurarFriccion();
            StartCoroutine(PlaceOnTop(grabbed, outDir));

            grabbed = null;
            grabbedRend = null;
            hasDragTarget = false;
            return;
        }

        // No lo saco lo suficiente: vuelve a su sitio y el turno no cambia.
        DevolverBloqueAgarrado();
        OnInvalidMove?.Invoke("Movimiento invalido: saca el bloque del todo. Sigue tu turno.");
    }

    // ===============================================================
    // COLOCAR ARRIBA
    // ===============================================================
    private IEnumerator PlaceOnTop(Rigidbody rb, Vector3 outDir)
    {
        busy = true;
        flying = rb;

        if (highlightSelected) RestoreColor();

        // En transito no colisiona con nada
        Collider col = rb.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Durante el vuelo el bloque se mueve a mano, no por fisica.
        RigidbodyInterpolation interpolacion = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;

        // --- destino teorico, calculado igual que en JengaBuilder ---
        float layerHeight = builder.blockSize.y + builder.gap;
        bool rotated = topLayer % 2 == 1;

        float y = topLayer * layerHeight + builder.blockSize.y * 0.5f;
        float off = (slotsFilled - (builder.blocksPerLayer - 1) / 2f)
                    * (builder.blockSize.x + builder.gap);

        Vector3 localPos = rotated
            ? new Vector3(0f, y, off)
            : new Vector3(off, y, 0f);

        Quaternion localRot = rotated
            ? Quaternion.Euler(0f, 90f, 0f)
            : Quaternion.identity;

        Vector3 targetPos = blocksRoot.TransformPoint(localPos);
        Quaternion targetRot = blocksRoot.rotation * localRot;

        // --- validar la superficie real de apoyo ---
        // El collider del bloque esta apagado, asi que el rayo lo ignora. El
        // origen va alto y fijo: atarlo a hoverHeight hacia que bajar el vuelo
        // arrancara el rayo dentro de la torre y no viera nada.
        float alturaRayo = builder.blockSize.y * 12f;
        Vector3 origenRayo = targetPos + blocksRoot.up * alturaRayo;

        Vector3 restPos = targetPos;
        bool dropFree = false;

        if (snapToRealSurface)
        {
            if (Physics.Raycast(origenRayo, -blocksRoot.up, out RaycastHit surface,
                                alturaRayo + builder.blockSize.y * 4f))
            {
                // Justo apoyado: medio bloque mas un pelo para no penetrar.
                // Antes sobraban 0.05 alturas de bloque, y al dormirlo ahi el
                // bloque quedaba flotando y la torre crecia de mas.
                restPos = surface.point
                          + blocksRoot.up * (builder.blockSize.y * 0.5f + 0.0005f);
                dropFree = Vector3.Angle(surface.normal, blocksRoot.up) > maxSurfaceTilt;
            }
            else
            {
                dropFree = true;   // no hay nada debajo: la torre ya no llega aca
            }
        }

        // Punto de suelta. Con la cima torcida no se apoya con precision, pero
        // se lo suelta desde apenas encima en vez de tirarlo desde la altura
        // del vuelo: asi una mala colocacion puede tumbar la torre sin que
        // ademas destroce la cima para las jugadas siguientes.
        Vector3 releasePos = dropFree
            ? restPos + blocksRoot.up * (builder.blockSize.y * freeDropHeight)
            : restPos;

        Vector3 hoverPoint = releasePos + blocksRoot.up * hoverHeight;

        // --- waypoints del vuelo ---
        Vector3 p0 = rb.transform.position;
        Quaternion r0 = rb.transform.rotation;

        Vector3 p1 = p0 + outDir * (builder.blockSize.z * clearanceFactor);
        Vector3 p2 = new Vector3(p1.x, hoverPoint.y, p1.z);
        Vector3 p3 = hoverPoint;

        float seg = Mathf.Max(0.05f, placeDuration) / 4f;

        yield return Fly(rb, p0, p1, r0, r0, seg);
        yield return Fly(rb, p1, p2, r0, targetRot, seg);
        yield return Fly(rb, p2, p3, targetRot, targetRot, seg);

        yield return Fly(rb, p3, releasePos, targetRot, targetRot, seg);

        rb.position = releasePos;
        rb.rotation = targetRot;
        rb.transform.SetPositionAndRotation(releasePos, targetRot);

        // --- se integra a la torre ---
        rb.interpolation = interpolacion;
        if (col != null) col.enabled = true;

        rb.name = $"Block_L{topLayer}_{slotsFilled}";
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Apoyo limpio: el bloque queda quieto donde lo pusiste, como cuando
        // apoyas una pieza de verdad. Si la cima estaba torcida no se duerme:
        // se lo solto un poco mas arriba y la fisica decide si aguanta.
        if (!dropFree) rb.Sleep();

        slotsFilled++;
        if (slotsFilled >= builder.blocksPerLayer)
        {
            slotsFilled = 0;
            topLayer++;
        }

        placedCount++;
        onBlockPlaced?.Invoke();

        flying = null;
        busy = false;
    }

    /// <summary>
    /// Mueve el bloque a mano entre dos poses. Escribe rb.position/rb.rotation
    /// y no el transform: con la interpolacion activa Unity reescribe el
    /// transform en cada frame a partir de la ultima pose de fisica, se pisa
    /// con lo que escribe la corrutina y el bloque termina soltandose en una
    /// pose distinta de la calculada. Por eso PlaceOnTop apaga la
    /// interpolacion mientras dura el vuelo.
    /// </summary>
    private IEnumerator Fly(Rigidbody rb, Vector3 from, Vector3 to,
                            Quaternion fromRot, Quaternion toRot, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            rb.position = Vector3.Lerp(from, to, e);
            rb.rotation = Quaternion.Slerp(fromRot, toRot, e);

            yield return null;
        }
    }

    // ===============================================================
    // DERROTA
    //
    // Un bloque cuenta como caido si se alejo del eje de la torre o si
    // quedo por debajo del primer piso. Con 'fallenBlocksToLose' o mas,
    // se considera que la torre colapso.
    // ===============================================================
    private void CheckCollapse()
    {
        if (collapsed || blocksRoot == null) return;

        float radius = builder.blockSize.z * fallRadiusFactor;
        float minY = builder.blockSize.y * 0.75f;

        int fallen = 0;    // bloques que ya no estan en la torre
        int tilted = 0;    // bloques que siguen en la torre pero torcidos

        foreach (Transform child in blocksRoot)
        {
            if (!child.name.StartsWith("Block_")) continue;
            if (child.name.EndsWith("_ANCHOR")) continue;

            // Ignorar el bloque que el jugador esta manipulando
            if (grabbed != null && child == grabbed.transform) continue;
            if (flying != null && child == flying.transform) continue;

            Vector3 local = blocksRoot.InverseTransformPoint(child.position);

            bool outOfBounds = Mathf.Abs(local.x) > radius || Mathf.Abs(local.z) > radius;
            bool tooLow = local.y < minY;

            if (outOfBounds || tooLow)
            {
                fallen++;
                continue;
            }

            // Un bloque bien apoyado tiene su cara de arriba paralela a la de
            // la torre. Cuanto mas se aparta, peor esta la estructura.
            if (Vector3.Angle(child.up, blocksRoot.up) > maxBlockTilt) tilted++;
        }

        if (fallen >= fallenBlocksToLose || tilted >= tiltedBlocksToLose)
        {
            collapsed = true;
            onTowerCollapsed?.Invoke();
        }
    }

    // ===============================================================
    // Reiniciar el estado de juego (la torre la reconstruye JengaBuilder)
    // ===============================================================
    public void ResetGameState()
    {
        StopAllCoroutines();
        collapsed = false;
        busy = false;
        grabbed = null;
        flying = null;
        rotating = false;
        rotationPending = false;
        pendingInvalidReason = null;
        rotatingBodies.Clear();
        frozenForRotation.Clear();
        rotationInterpolation.Clear();
        grabbedCollider = null;
        hasDragTarget = false;
        placedCount = 0;
        slotsFilled = 0;
        topLayer = builder != null ? builder.layers : 18;
        blocksRoot = null;
    }

    // ===============================================================
    // HIGHLIGHT
    // ===============================================================
    private void ApplyHighlight()
    {
        grabbedRend = grabbed.GetComponent<Renderer>();
        if (grabbedRend == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        grabbedRend.GetPropertyBlock(mpb);
        grabbedOriginalColor = mpb.GetColor("_BaseColor");

        mpb.SetColor("_BaseColor", highlightColor);
        mpb.SetColor("_Color", highlightColor);
        grabbedRend.SetPropertyBlock(mpb);
    }

    private void RestoreColor()
    {
        if (grabbedRend == null) return;

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        grabbedRend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", grabbedOriginalColor);
        mpb.SetColor("_Color", grabbedOriginalColor);
        grabbedRend.SetPropertyBlock(mpb);
    }

    // ===============================================================
    // INPUT (funciona con Input System nuevo y con el viejo)
    // ===============================================================
    private void ReadPointer()
    {
        pointerDown = pointerHeld = pointerUp = false;

#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && (ts.primaryTouch.press.isPressed ||
                           ts.primaryTouch.press.wasReleasedThisFrame))
        {
            pointerPos = ts.primaryTouch.position.ReadValue();
            pointerDown = ts.primaryTouch.press.wasPressedThisFrame;
            pointerHeld = ts.primaryTouch.press.isPressed;
            pointerUp = ts.primaryTouch.press.wasReleasedThisFrame;
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            pointerPos = mouse.position.ReadValue();
            pointerDown = mouse.leftButton.wasPressedThisFrame;
            pointerHeld = mouse.leftButton.isPressed;
            pointerUp = mouse.leftButton.wasReleasedThisFrame;
        }
#else
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            pointerPos  = t.position;
            pointerDown = t.phase == TouchPhase.Began;
            pointerHeld = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            pointerUp   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
        }
        else
        {
            pointerPos  = Input.mousePosition;
            pointerDown = Input.GetMouseButtonDown(0);
            pointerHeld = Input.GetMouseButton(0);
            pointerUp   = Input.GetMouseButtonUp(0);
        }
#endif
    }
}