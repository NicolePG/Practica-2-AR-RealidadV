using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelInicio;
    public GameObject panelJugadores;
    public GameObject panelJuego;
    public GameObject panelFin;

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

    // Nombres de los jugadores
    private string jugador1;
    private string jugador2;
    private string jugador3;

    public string Jugador1 => jugador1;
    public string Jugador2 => jugador2;
    public string Jugador3 => jugador3;

    private void Start()
    {
        MostrarInicio();
    }

    // ------------------------------------
    // PANTALLA DE INICIO
    // ------------------------------------

    public void MostrarInicio()
    {
        panelInicio.SetActive(true);
        panelJugadores.SetActive(false);
        panelJuego.SetActive(false);
        panelFin.SetActive(false);
    }

    public void AbrirRegistroJugadores()
{
    panelInicio.SetActive(false);
    panelJugadores.SetActive(true);
    panelJuego.SetActive(false);
    panelFin.SetActive(false);

    textoError.text = "";
}

    // ------------------------------------
    // REGISTRO DE JUGADORES
    // ------------------------------------

    public bool ValidarJugadores()
    {
        jugador1 = inputJugador1.text.Trim();
        jugador2 = inputJugador2.text.Trim();
        jugador3 = inputJugador3.text.Trim();

        if (string.IsNullOrEmpty(jugador1) ||
            string.IsNullOrEmpty(jugador2) ||
            string.IsNullOrEmpty(jugador3))
        {
            textoError.text = "Debes ingresar el nombre de los 3 jugadores.";
            return false;
        }

        return true;
    }

    public void IniciarPartida()
    {
        if (!ValidarJugadores())
            return;

        panelJugadores.SetActive(false);
        panelJuego.SetActive(true);
        panelFin.SetActive(false);

        MostrarTurno(jugador1);
    }

    // ------------------------------------
    // DURANTE EL JUEGO
    // ------------------------------------

    public void MostrarTurno(string nombreJugador)
    {
        textoJugadorActual.text = "Turno de: " + nombreJugador;
        textoInstruccion.text =
            "Retira un bloque y colócalo sobre la torre.";
    }

    public void MostrarMensaje(string mensaje)
    {
        textoInstruccion.text = mensaje;
    }

    // ------------------------------------
    // FIN DEL JUEGO
    // ------------------------------------

    public void MostrarFinJuego(string jugadorPerdedor)
    {
        panelJuego.SetActive(false);
        panelFin.SetActive(true);

        textoJugadorPerdedor.text =
            jugadorPerdedor + " perdió.";

        textoFin.text =
            "¡La torre ha caído!\n" +
            jugadorPerdedor + " derribó la torre.";
    }

    // ------------------------------------
    // NUEVA PARTIDA
    // ------------------------------------

    public void JugarNuevamente()
    {
        panelFin.SetActive(false);
        panelJugadores.SetActive(true);

        inputJugador1.text = "";
        inputJugador2.text = "";
        inputJugador3.text = "";

        textoError.text = "";
    }

    public void VolverAlMenu()
    {
        MostrarInicio();
    }
    public void CerrarJuego()
    {
        Application.Quit();
    }
}