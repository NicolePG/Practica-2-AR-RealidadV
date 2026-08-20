using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Capa de presentacion del juego: paneles, textos, audio y estilos.
///
/// El UIManager NO decide nada de la partida. Solo muestra lo que le piden y
/// avisa (via eventos) cuando el jugador pulsa un boton. Quien decide es el
/// <see cref="GameManager"/>.
///
/// Los botones del Canvas siguen apuntando a los mismos metodos publicos de
/// siempre (AbrirRegistroJugadores, IniciarPartida, JugarNuevamente,
/// VolverAlMenu, CerrarJuego), asi que el cableado del prefab no se rompe.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelInicio;
    public GameObject panelJugadores;
    public GameObject panelJuego;
    public GameObject panelFin;

    [Header("HUD de juego")]
    [Tooltip("Cartel con el turno actual, arriba de la pantalla.")]
    public GameObject panelTurno;

    [Tooltip("Muestra los 3 jugadores como fichas de color con su nombre " +
             "dentro del cartel de turno. Si se destilda vuelve el texto " +
             "'Turno de: ...' de siempre.")]
    public bool usarFichasDeJugador = true;

    [Tooltip("Si queda vacio se crea sola dentro del cartel de turno.")]
    public TurnHud fichasJugadores;

    [Tooltip("Cartel con el estado del seguimiento AR, abajo a la izquierda.")]
    public GameObject panelEstadoAR;

    public TMP_Text textoEstadoAR;

    [Header("Inputs")]
    public TMP_InputField inputJugador1;
    public TMP_InputField inputJugador2;
    public TMP_InputField inputJugador3;

    [Header("Textos")]
    public TMP_Text textoError;
    public TMP_Text textoJugadorActual;
    public TMP_Text textoInstruccion;
    public TMP_Text textoFin;
    public TMP_Text textoJugadorPerdedor;

    [Header("Botones")]
    public Button botonContinuar;
    public Button botonJugarNuevamente;
    public Button botonMenu;

    [Header("Audio")]
    public AudioSource musicaFondo;   // Musica de fondo (loop)
    public AudioSource efectosFX;     // Clicks de botones
    public AudioSource efectosJuego;  // Sonido de derrota
    public AudioClip clipClick;
    public AudioClip clipDerrota;

    [Header("Colores del HUD")]
    public Color colorEstadoOk = new Color(0.35f, 1f, 0.45f);
    public Color colorEstadoBuscando = new Color(1f, 0.82f, 0.1f);

    [Header("Movil")]
    [Tooltip("Encoge el HUD de juego al area segura de la pantalla, para que " +
             "el cartel de turno no quede debajo del notch ni el boton SALIR " +
             "bajo la camara frontal. No toca los menus, que siguen ocupando " +
             "la pantalla entera.")]
    public bool aplicarSafeArea = true;

    [Tooltip("Pone una barra oscura detras del texto de instruccion. Sin ella " +
             "el texto blanco desaparece sobre la hoja del marcador o sobre " +
             "los bloques claros.")]
    public bool fondoParaInstruccion = true;

    public Color colorFondoInstruccion = new Color(0f, 0f, 0f, 0.55f);

    [Header("Colores por jugador")]
    // Mismos valores que Player1/2/3Material, para que la fila del HUD y los
    // munequitos 3D de la escena muestren exactamente el mismo color.
    public Color colorJugador1 = new Color(0.1176471f, 0.5647059f, 1f);
    public Color colorJugador2 = new Color(0.8627452f, 0.07843138f, 0.2352941f);
    public Color colorJugador3 = new Color(0.6784314f, 1f, 0.1843137f);

    // ------------------------------------
    // EVENTOS PARA EL GameManager
    // ------------------------------------

    /// <summary>Los 3 nombres se validaron y el jugador pulso CONTINUAR.</summary>
    public event Action OnPartidaIniciada;

    /// <summary>Se pulso JUGAR DE NUEVO (vuelve al registro de jugadores).</summary>
    public event Action OnPartidaReiniciada;

    /// <summary>Se volvio al menu principal.</summary>
    public event Action OnVueltaAlMenu;

    // Nombres de los jugadores
    private string jugador1;
    private string jugador2;
    private string jugador3;

    public string Jugador1 => jugador1;
    public string Jugador2 => jugador2;
    public string Jugador3 => jugador3;

    public string NombreDe(int numeroJugador)
    {
        switch (numeroJugador)
        {
            case 1: return jugador1;
            case 2: return jugador2;
            case 3: return jugador3;
            default: return "";
        }
    }

    public Color ColorDe(int numeroJugador)
    {
        switch (numeroJugador)
        {
            case 1: return colorJugador1;
            case 2: return colorJugador2;
            case 3: return colorJugador3;
            default: return Color.white;
        }
    }

    private Rect ultimaSafeArea;

    private void Start()
    {
        AplicarFondoPurpura();
        AplicarEstiloBotones();
        CrearFondoInstruccion();
        AplicarSafeArea();
        IniciarMusica();
        MostrarInicio();
    }

    private void Update()
    {
        // La zona segura cambia al rotar el telefono o al aparecer la barra
        // de gestos, asi que se revisa (comparar dos Rect no cuesta nada).
        if (aplicarSafeArea && Screen.safeArea != ultimaSafeArea) AplicarSafeArea();
    }

    /// <summary>
    /// Ajusta el HUD de juego al area segura. Solo se toca panelJuego: los
    /// menus llevan fondo de color y conviene que lleguen a los bordes.
    /// </summary>
    private void AplicarSafeArea()
    {
        if (!aplicarSafeArea || panelJuego == null) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        RectTransform rt = panelJuego.transform as RectTransform;
        if (rt == null) return;

        Rect zona = Screen.safeArea;
        ultimaSafeArea = zona;

        Vector2 min = zona.position;
        Vector2 max = zona.position + zona.size;

        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ------------------------------------
    // GESTION DE AUDIO
    // ------------------------------------

    private void IniciarMusica()
    {
        if (musicaFondo != null && !musicaFondo.isPlaying)
        {
            musicaFondo.loop = true;
            musicaFondo.Play();
        }
    }

    public void ReproducirClick()
    {
        Sonar(efectosFX, clipClick);
    }

    public void ReproducirDerrota()
    {
        Sonar(efectosJuego, clipDerrota);
    }

    /// <summary>
    /// Si hay un clip suelto lo mezcla; si no, dispara el que ya trae la
    /// fuente. Los botones del Canvas llaman ademas a AudioSource.Play() por
    /// su cuenta: como Play() reinicia en vez de superponer, se oye un solo
    /// click aunque lleguen las dos llamadas en el mismo frame.
    /// </summary>
    private void Sonar(AudioSource fuente, AudioClip clip)
    {
        if (fuente == null) return;

        if (clip != null) fuente.PlayOneShot(clip);
        else if (fuente.clip != null) fuente.Play();
    }

    // ------------------------------------
    // COLORES Y ESTILOS POR CODIGO
    // ------------------------------------

    private void AplicarFondoPurpura()
    {
        Color purpura = new Color(0.45f, 0.15f, 0.65f, 0.85f);

        PintarFondo(panelInicio, purpura);
        PintarFondo(panelJugadores, purpura);
        PintarFondo(panelFin, purpura);

        // OJO: panelJuego NO se pinta. Durante la partida hace de HUD y tiene
        // que dejar ver la camara y la torre. Ademas se le saca el raycast
        // para que el fondo transparente no se coma los toques sobre la torre.
        if (panelJuego != null)
        {
            Image fondoJuego = panelJuego.GetComponent<Image>();
            if (fondoJuego != null)
            {
                fondoJuego.color = new Color(0f, 0f, 0f, 0f);
                fondoJuego.raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// Crea una barra oscura justo detras del texto de instruccion. Va como
    /// hermano anterior en la jerarquia, que es lo que decide el orden de
    /// dibujado en un Canvas: primero el fondo, encima el texto.
    /// </summary>
    private void CrearFondoInstruccion()
    {
        if (!fondoParaInstruccion || textoInstruccion == null) return;

        RectTransform texto = textoInstruccion.rectTransform;
        if (texto.parent == null) return;

        var go = new GameObject("FondoInstruccion", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(texto.parent, false);

        rt.anchorMin = texto.anchorMin;
        rt.anchorMax = texto.anchorMax;
        rt.pivot = texto.pivot;
        rt.anchoredPosition = texto.anchoredPosition;
        rt.sizeDelta = texto.sizeDelta;
        rt.localScale = Vector3.one;
        rt.SetSiblingIndex(texto.GetSiblingIndex());

        Image img = go.AddComponent<Image>();
        img.color = colorFondoInstruccion;
        img.raycastTarget = false;
    }

    private void PintarFondo(GameObject panel, Color color)
    {
        if (panel == null) return;

        Image img = panel.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    private void AplicarEstiloBotones()
    {
        Color amarilloNeon = new Color(1f, 0.82f, 0.1f);
        Color coralRojo = new Color(1f, 0.35f, 0.35f);
        Color cyanTurquesa = new Color(0f, 0.85f, 1f);

        if (panelInicio != null)
        {
            Button[] botonesInicio = panelInicio.GetComponentsInChildren<Button>(true);
            if (botonesInicio.Length > 0 && botonesInicio[0].GetComponent<Image>() != null)
                botonesInicio[0].GetComponent<Image>().color = amarilloNeon;

            if (botonesInicio.Length > 1 && botonesInicio[1].GetComponent<Image>() != null)
                botonesInicio[1].GetComponent<Image>().color = coralRojo;
        }

        if (botonContinuar != null && botonContinuar.GetComponent<Image>() != null)
            botonContinuar.GetComponent<Image>().color = amarilloNeon;

        if (botonJugarNuevamente != null && botonJugarNuevamente.GetComponent<Image>() != null)
            botonJugarNuevamente.GetComponent<Image>().color = amarilloNeon;

        if (botonMenu != null && botonMenu.GetComponent<Image>() != null)
            botonMenu.GetComponent<Image>().color = cyanTurquesa;
    }

    // ------------------------------------
    // PANTALLA DE INICIO
    // ------------------------------------

    public void MostrarInicio()
    {
        if (panelInicio != null) panelInicio.SetActive(true);
        if (panelJugadores != null) panelJugadores.SetActive(false);
        if (panelJuego != null) panelJuego.SetActive(false);
        if (panelFin != null) panelFin.SetActive(false);
    }

    public void AbrirRegistroJugadores()
    {
        ReproducirClick();

        if (panelInicio != null) panelInicio.SetActive(false);
        if (panelJugadores != null) panelJugadores.SetActive(true);
        if (panelJuego != null) panelJuego.SetActive(false);
        if (panelFin != null) panelFin.SetActive(false);

        if (textoError != null) textoError.text = "";
    }

    // ------------------------------------
    // REGISTRO DE JUGADORES
    // ------------------------------------

    public bool ValidarJugadores()
    {
        jugador1 = inputJugador1 != null ? inputJugador1.text.Trim() : "";
        jugador2 = inputJugador2 != null ? inputJugador2.text.Trim() : "";
        jugador3 = inputJugador3 != null ? inputJugador3.text.Trim() : "";

        if (string.IsNullOrEmpty(jugador1) ||
            string.IsNullOrEmpty(jugador2) ||
            string.IsNullOrEmpty(jugador3))
        {
            if (textoError != null)
                textoError.text = "Debes ingresar el nombre de los 3 jugadores.";
            return false;
        }

        if (jugador1 == jugador2 || jugador1 == jugador3 || jugador2 == jugador3)
        {
            if (textoError != null)
                textoError.text = "Los nombres tienen que ser distintos.";
            return false;
        }

        if (textoError != null) textoError.text = "";
        return true;
    }

    /// <summary>
    /// Llamado por el boton CONTINUAR. Valida y, si esta todo bien, pasa al
    /// HUD de juego y avisa al GameManager para que arranque la partida.
    /// </summary>
    public void IniciarPartida()
    {
        ReproducirClick();

        if (!ValidarJugadores())
            return;

        if (panelInicio != null) panelInicio.SetActive(false);
        if (panelJugadores != null) panelJugadores.SetActive(false);
        if (panelJuego != null) panelJuego.SetActive(true);
        if (panelFin != null) panelFin.SetActive(false);

        ConstruirFichas();

        OnPartidaIniciada?.Invoke();
    }

    // ------------------------------------
    // DURANTE EL JUEGO
    // ------------------------------------

    public void MostrarTurno(string nombreJugador)
    {
        MostrarTurno(nombreJugador, 0);
    }

    public void MostrarTurno(string nombreJugador, int numeroJugador)
    {
        if (fichasJugadores != null) fichasJugadores.MarcarActivo(numeroJugador);

        if (textoJugadorActual != null && textoJugadorActual.gameObject.activeSelf)
        {
            textoJugadorActual.text = numeroJugador > 0
                ? $"Turno de: {nombreJugador}  (Jugador {numeroJugador})"
                : "Turno de: " + nombreJugador;

            textoJugadorActual.color = ColorDe(numeroJugador);
        }

        MostrarMensaje("Retira un bloque y colocalo sobre la torre.");
    }

    /// <summary>
    /// Arma la fila de fichas dentro del cartel de turno con los nombres
    /// recien validados. Reemplaza al texto "Turno de: ...", que dice lo
    /// mismo pero sin mostrar a los otros dos jugadores.
    /// </summary>
    private void ConstruirFichas()
    {
        if (!usarFichasDeJugador || panelTurno == null) return;

        if (fichasJugadores == null)
        {
            GameObject go = new GameObject("FichasJugadores", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(panelTurno.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 8f);
            rt.offsetMax = new Vector2(-10f, -8f);
            rt.localScale = Vector3.one;

            fichasJugadores = go.AddComponent<TurnHud>();
        }

        if (textoJugadorActual != null) textoJugadorActual.gameObject.SetActive(false);

        fichasJugadores.Construir(
            new[] { jugador1, jugador2, jugador3 },
            new[] { colorJugador1, colorJugador2, colorJugador3 });
    }

    public void MostrarMensaje(string mensaje)
    {
        if (textoInstruccion != null) textoInstruccion.text = mensaje;
    }

    /// <summary>Estado del seguimiento AR (abajo a la izquierda).</summary>
    public void MostrarEstadoAR(string mensaje, bool estable)
    {
        if (panelEstadoAR != null && !panelEstadoAR.activeSelf)
            panelEstadoAR.SetActive(true);

        if (textoEstadoAR != null)
        {
            textoEstadoAR.text = mensaje;
            textoEstadoAR.color = estable ? colorEstadoOk : colorEstadoBuscando;
        }
    }

    public void MostrarHudTurno(bool visible)
    {
        if (panelTurno != null) panelTurno.SetActive(visible);
    }

    // ------------------------------------
    // FIN DEL JUEGO
    // ------------------------------------

    public void MostrarFinJuego(string jugadorPerdedor)
    {
        ReproducirDerrota();

        if (panelJuego != null) panelJuego.SetActive(false);
        if (panelFin != null) panelFin.SetActive(true);

        if (textoJugadorPerdedor != null)
            textoJugadorPerdedor.text = jugadorPerdedor + " perdio.";

        if (textoFin != null)
            textoFin.text = "TORRE CAIDA!\n" + jugadorPerdedor + " derribo la torre.";
    }

    // ------------------------------------
    // NUEVA PARTIDA
    // ------------------------------------

    public void JugarNuevamente()
    {
        ReproducirClick();

        if (panelFin != null) panelFin.SetActive(false);
        if (panelJuego != null) panelJuego.SetActive(false);
        if (panelJugadores != null) panelJugadores.SetActive(true);

        // Se conservan los nombres: normalmente se juega otra vez con los mismos.
        if (inputJugador1 != null) inputJugador1.text = jugador1;
        if (inputJugador2 != null) inputJugador2.text = jugador2;
        if (inputJugador3 != null) inputJugador3.text = jugador3;

        if (textoError != null) textoError.text = "";

        OnPartidaReiniciada?.Invoke();
    }

    public void VolverAlMenu()
    {
        ReproducirClick();
        MostrarInicio();
        OnVueltaAlMenu?.Invoke();
    }

    public void CerrarJuego()
    {
        ReproducirClick();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
