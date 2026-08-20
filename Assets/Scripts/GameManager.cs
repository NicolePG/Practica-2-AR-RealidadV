using System.Collections;
using UnityEngine;

/// <summary>
/// Cerebro de la partida: aplica las reglas y conecta la interfaz
/// (<see cref="UIManager"/>) con el Jenga AR (<see cref="JengaBuilder"/> +
/// <see cref="JengaInteraction"/>) y con los indicadores de turno 3D
/// (<see cref="PlayerIndicatorsManager"/>).
///
/// Reglas implementadas:
///   1. La partida comienza con el Jugador 1.
///   2. Cada jugador mueve un unico bloque por turno.
///   3. No se pueden retirar bloques del nivel superior.
///   4. El bloque retirado debe colocarse sobre la torre.
///   5. Un movimiento invalido no cambia el turno.
///   6. El jugador que derriba la torre pierde.
///   7. Despues del Jugador 3, el turno regresa al Jugador 1.
///   8. La manipulacion solo se habilita cuando el seguimiento AR es estable.
///
/// Las reglas 2, 3 y 4 las hace cumplir <see cref="JengaInteraction"/>
/// (un bloque a la vez, capa superior protegida, vuelo forzado a la cima).
/// Aca se controla el resto y se le da voz a la UI.
/// </summary>
[DefaultExecutionOrder(-50)]
public class GameManager : MonoBehaviour
{
    public enum Estado
    {
        Menu,          // pantalla de inicio / registro de jugadores
        EsperandoAR,   // partida arrancada, falta apuntar al marcador
        Jugando,       // se puede manipular la torre
        Fin            // la torre cayo
    }

    [Header("Referencias (si quedan vacias se buscan solas en la escena)")]
    public UIManager ui;
    public JengaBuilder builder;
    public JengaInteraction interaction;
    public PlayerIndicatorsManager indicadores;

    [Header("Seguimiento AR")]
    [Tooltip("Segundos que el marcador tiene que verse seguido antes de " +
             "habilitar la manipulacion. Evita que un destello de tracking " +
             "deje tocar la torre mientras Vuforia todavia se acomoda.")]
    [Range(0f, 3f)] public float retardoTrackingEstable = 0.75f;

    [Header("Turnos")]
    [Tooltip("Segundos que el jugador que acaba de mover sigue siendo " +
             "responsable de la torre. Si se derrumba dentro de esta ventana " +
             "pierde el, no el jugador siguiente.")]
    [Range(0f, 8f)] public float graciaColapso = 2.5f;

    [Tooltip("Segundos que se muestra el aviso de movimiento invalido antes " +
             "de volver al mensaje normal del turno.")]
    [Range(0.5f, 5f)] public float duracionAviso = 2f;

    public const int NumJugadores = 3;

    // ---- estado publico de solo lectura ----
    public Estado EstadoActual => estado;
    public int NumeroJugadorActual => turno + 1;        // 1..3
    public string NombreJugadorActual => nombres[turno];

    // ---- estado interno ----
    private readonly string[] nombres = new string[NumJugadores];

    private Estado estado = Estado.Menu;
    private int turno;                 // 0..2
    private int responsable;           // a quien se le imputa un colapso ahora
    private bool marcadorVisible;
    private bool trackingEstable;

    private Coroutine rutinaEstabilizar;
    private Coroutine rutinaResponsable;
    private Coroutine rutinaAviso;

    // ===============================================================
    // ARRANQUE
    // ===============================================================
    void Awake()
    {
        if (ui == null) ui = FindAnyObjectByType<UIManager>();
        if (builder == null) builder = FindAnyObjectByType<JengaBuilder>();
        if (interaction == null) interaction = FindAnyObjectByType<JengaInteraction>();
        if (indicadores == null) indicadores = FindAnyObjectByType<PlayerIndicatorsManager>();

        if (interaction != null)
        {
            interaction.SetInputEnabled(false);
            interaction.onBlockPlaced.AddListener(BloqueColocado);
            interaction.onTowerCollapsed.AddListener(TorreDerribada);
            interaction.OnBlockGrabbed += BloqueAgarrado;
            interaction.OnInvalidMove += MovimientoInvalido;
        }

        if (ui != null)
        {
            ui.OnPartidaIniciada += ComenzarPartida;
            ui.OnPartidaReiniciada += VolverAlRegistro;
            ui.OnVueltaAlMenu += VolverAlMenu;
        }
    }

    void OnDestroy()
    {
        if (interaction != null)
        {
            interaction.onBlockPlaced.RemoveListener(BloqueColocado);
            interaction.onTowerCollapsed.RemoveListener(TorreDerribada);
            interaction.OnBlockGrabbed -= BloqueAgarrado;
            interaction.OnInvalidMove -= MovimientoInvalido;
        }

        if (ui != null)
        {
            ui.OnPartidaIniciada -= ComenzarPartida;
            ui.OnPartidaReiniciada -= VolverAlRegistro;
            ui.OnVueltaAlMenu -= VolverAlMenu;
        }
    }

    void Start()
    {
        if (ui != null)
        {
            ui.MostrarHudTurno(true);
            ui.MostrarEstadoAR("Buscando marcador...", false);
        }
    }

    // ===============================================================
    // CICLO DE LA PARTIDA
    // ===============================================================

    /// <summary>Regla 1: la partida comienza con el Jugador 1.</summary>
    private void ComenzarPartida()
    {
        nombres[0] = ui != null ? ui.Jugador1 : "Jugador 1";
        nombres[1] = ui != null ? ui.Jugador2 : "Jugador 2";
        nombres[2] = ui != null ? ui.Jugador3 : "Jugador 3";

        turno = 0;
        responsable = 0;
        estado = Estado.EsperandoAR;

        DetenerRutinas();
        ReconstruirTorre();
        RefrescarTurno();

        if (ui != null) ui.MostrarHudTurno(true);

        // Si el marcador ya se estaba viendo cuando se pulso CONTINUAR,
        // la partida arranca en el acto.
        EvaluarTracking();
    }

    private void VolverAlRegistro()
    {
        estado = Estado.Menu;
        DetenerRutinas();
        BloquearManipulacion();
        LimpiarTorre();
    }

    private void VolverAlMenu()
    {
        estado = Estado.Menu;
        DetenerRutinas();
        BloquearManipulacion();
        LimpiarTorre();

        if (ui != null) ui.MostrarEstadoAR("Buscando marcador...", trackingEstable);
    }

    // ===============================================================
    // SEGUIMIENTO AR  (regla 8)
    //
    // Se llaman desde los eventos OnTargetFound / OnTargetLost del
    // DefaultObserverEventHandler que lleva el ImageTarget.
    // ===============================================================

    public void OnARTrackingFound()
    {
        marcadorVisible = true;

        if (ui != null) ui.MostrarEstadoAR("Estabilizando seguimiento...", false);

        if (rutinaEstabilizar != null) StopCoroutine(rutinaEstabilizar);
        rutinaEstabilizar = StartCoroutine(EstabilizarTracking());
    }

    public void OnARTrackingLost()
    {
        marcadorVisible = false;
        trackingEstable = false;

        if (rutinaEstabilizar != null)
        {
            StopCoroutine(rutinaEstabilizar);
            rutinaEstabilizar = null;
        }

        BloquearManipulacion();

        if (ui != null)
        {
            ui.MostrarEstadoAR("Seguimiento perdido", false);

            if (estado == Estado.Jugando || estado == Estado.EsperandoAR)
                ui.MostrarMensaje("Apunta la camara al marcador para seguir jugando.");
        }

        if (estado == Estado.Jugando) estado = Estado.EsperandoAR;
    }

    private IEnumerator EstabilizarTracking()
    {
        yield return new WaitForSeconds(retardoTrackingEstable);

        trackingEstable = marcadorVisible;
        rutinaEstabilizar = null;

        EvaluarTracking();
    }

    private void EvaluarTracking()
    {
        if (!trackingEstable)
        {
            if (ui != null)
                ui.MostrarEstadoAR(marcadorVisible ? "Estabilizando seguimiento..."
                                                   : "Buscando marcador...", false);
            return;
        }

        if (ui != null) ui.MostrarEstadoAR("Seguimiento estable", true);

        if (estado != Estado.EsperandoAR) return;

        // El marcador esta firme: se levanta la torre y empieza el juego.
        if (builder != null && !builder.IsBuilt) builder.BuildTower();

        estado = Estado.Jugando;
        HabilitarManipulacion();
        RefrescarTurno();
    }

    // ===============================================================
    // JUGADAS
    // ===============================================================

    /// <summary>El jugador agarro un bloque: desde ya responde por la torre.</summary>
    private void BloqueAgarrado()
    {
        if (rutinaResponsable != null)
        {
            StopCoroutine(rutinaResponsable);
            rutinaResponsable = null;
        }

        responsable = turno;
    }

    /// <summary>
    /// Reglas 2, 4 y 7: se coloco el bloque arriba, el turno pasa al siguiente
    /// jugador (y del 3 vuelve al 1).
    /// </summary>
    private void BloqueColocado()
    {
        if (estado != Estado.Jugando) return;

        int quienMovio = turno;
        turno = (turno + 1) % NumJugadores;

        // El que acaba de apilar sigue siendo el responsable un rato: si la
        // torre se desarma por su mala colocacion, pierde el (regla 6).
        responsable = quienMovio;

        if (rutinaResponsable != null) StopCoroutine(rutinaResponsable);
        rutinaResponsable = StartCoroutine(TraspasarResponsabilidad());

        RefrescarTurno();
    }

    private IEnumerator TraspasarResponsabilidad()
    {
        yield return new WaitForSeconds(graciaColapso);

        responsable = turno;
        rutinaResponsable = null;
    }

    /// <summary>Regla 5: un movimiento invalido no cambia el turno.</summary>
    private void MovimientoInvalido(string motivo)
    {
        if (estado != Estado.Jugando) return;

        if (rutinaAviso != null) StopCoroutine(rutinaAviso);
        rutinaAviso = StartCoroutine(MostrarAviso(motivo));
    }

    private IEnumerator MostrarAviso(string motivo)
    {
        if (ui != null) ui.MostrarMensaje(motivo);

        yield return new WaitForSeconds(duracionAviso);

        rutinaAviso = null;
        if (estado == Estado.Jugando) RefrescarTurno();
    }

    /// <summary>Regla 6: el jugador que derriba la torre pierde.</summary>
    private void TorreDerribada()
    {
        if (estado == Estado.Fin) return;

        estado = Estado.Fin;
        DetenerRutinas();
        BloquearManipulacion();

        string perdedor = nombres[Mathf.Clamp(responsable, 0, NumJugadores - 1)];
        if (string.IsNullOrEmpty(perdedor)) perdedor = "Jugador " + (responsable + 1);

        if (ui != null) ui.MostrarFinJuego(perdedor);
    }

    // ===============================================================
    // HELPERS
    // ===============================================================

    private void RefrescarTurno()
    {
        string nombre = nombres[turno];
        if (string.IsNullOrEmpty(nombre)) nombre = "Jugador " + NumeroJugadorActual;

        if (ui != null)
        {
            ui.MostrarTurno(nombre, NumeroJugadorActual);

            if (estado == Estado.EsperandoAR)
                ui.MostrarMensaje("Apunta la camara al marcador para empezar.");
        }

        if (indicadores != null) indicadores.SetActivePlayer(NumeroJugadorActual);
    }

    private void HabilitarManipulacion()
    {
        if (interaction != null) interaction.SetInputEnabled(true);
    }

    private void BloquearManipulacion()
    {
        if (interaction != null) interaction.SetInputEnabled(false);
    }

    private void ReconstruirTorre()
    {
        if (builder == null) return;

        builder.ResetTower();
        if (interaction != null) interaction.ResetGameState();
    }

    private void LimpiarTorre()
    {
        if (builder != null) builder.ResetTower();
        if (interaction != null) interaction.ResetGameState();
    }

    private void DetenerRutinas()
    {
        if (rutinaResponsable != null) { StopCoroutine(rutinaResponsable); rutinaResponsable = null; }
        if (rutinaAviso != null) { StopCoroutine(rutinaAviso); rutinaAviso = null; }
    }
}
