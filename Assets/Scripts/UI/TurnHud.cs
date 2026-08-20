using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila de jugadores en la parte de arriba de la pantalla.
///
/// Cada jugador es un munequito 2D con la misma silueta que el marcador 3D
/// que lo representa alrededor de la torre (cabeza redonda, cuerpo de
/// capsula y aro en la base) y con su nombre debajo. El del turno se agranda,
/// toma su color pleno y late; los otros quedan grises.
///
/// La silueta se dibuja por codigo en una textura: los sprites integrados de
/// Unity (UI/Skin/Knob.psd y companiaa) no siempre entran en el build, y
/// cuando faltan la ficha sale como un cuadrado.
///
/// Se arma sola: el <see cref="UIManager"/> la crea dentro del cartel de
/// turno cuando ya conoce los tres nombres.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TurnHud : MonoBehaviour
{
    [Header("Tamanos (en unidades de canvas)")]
    [Tooltip("Separacion entre los centros de dos munequitos.")]
    public float separacion = 170f;
    public float altoFigura = 108f;
    public float altoFiguraActiva = 136f;
    public float alturaNombre = 44f;
    public int fuenteNombre = 30;

    [Header("Colores")]
    public Color colorApagado = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color nombreApagado = new Color(1f, 1f, 1f, 0.45f);
    public Color nombreActivo = Color.white;

    [Header("Latido del jugador en turno")]
    public bool latir = true;
    [Range(0f, 0.2f)] public float amplitudLatido = 0.06f;
    [Range(0.2f, 4f)] public float velocidadLatido = 1.6f;

    // La silueta es igual para todos: se dibuja una vez y se tinta por jugador.
    private static Sprite siluetaCache;

    private const int TexAncho = 128;
    private const int TexAlto = 160;

    private readonly List<RectTransform> figuras = new List<RectTransform>();
    private readonly List<Image> imagenes = new List<Image>();
    private readonly List<TMP_Text> etiquetas = new List<TMP_Text>();
    private readonly List<Color> colores = new List<Color>();

    private int activo = -1;   // indice 0..n-1, -1 = ninguno

    /// <summary>Rehace la fila. Se puede llamar en cada partida nueva.</summary>
    public void Construir(IList<string> nombres, IList<Color> coloresJugador)
    {
        Limpiar();

        int total = Mathf.Min(nombres.Count, coloresJugador.Count);
        if (total == 0) return;

        Sprite silueta = Silueta();
        float anchoFigura = altoFigura * TexAncho / (float)TexAlto;

        for (int i = 0; i < total; i++)
        {
            colores.Add(coloresJugador[i]);

            // --- columna centrada: los tres quedan juntos en el medio ---
            RectTransform slot = NuevoHijo("Jugador" + (i + 1), transform as RectTransform);
            slot.anchorMin = new Vector2(0.5f, 0f);
            slot.anchorMax = new Vector2(0.5f, 1f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2((i - (total - 1) * 0.5f) * separacion, 0f);
            slot.sizeDelta = new Vector2(separacion, 0f);

            // --- munequito ---
            RectTransform figura = NuevoHijo("Munequito", slot);
            figura.anchorMin = new Vector2(0.5f, 1f);
            figura.anchorMax = new Vector2(0.5f, 1f);
            figura.pivot = new Vector2(0.5f, 1f);
            figura.anchoredPosition = new Vector2(0f, -6f);
            figura.sizeDelta = new Vector2(anchoFigura, altoFigura);

            Image img = figura.gameObject.AddComponent<Image>();
            img.sprite = silueta;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // --- nombre debajo ---
            RectTransform texto = NuevoHijo("Nombre", slot);
            texto.anchorMin = new Vector2(0f, 0f);
            texto.anchorMax = new Vector2(1f, 0f);
            texto.pivot = new Vector2(0.5f, 0f);
            texto.anchoredPosition = new Vector2(0f, 2f);
            texto.sizeDelta = new Vector2(-6f, alturaNombre);

            TextMeshProUGUI tmp = texto.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = string.IsNullOrEmpty(nombres[i]) ? "Jugador " + (i + 1) : nombres[i];
            tmp.fontSize = fuenteNombre;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            figuras.Add(figura);
            imagenes.Add(img);
            etiquetas.Add(tmp);
        }

        MarcarActivo(0);
    }

    /// <param name="numeroJugador">1..n. Cualquier otro valor apaga todos.</param>
    public void MarcarActivo(int numeroJugador)
    {
        activo = numeroJugador - 1;

        for (int i = 0; i < imagenes.Count; i++)
        {
            bool esActivo = i == activo;

            imagenes[i].color = esActivo ? colores[i] : colorApagado;
            etiquetas[i].color = esActivo ? nombreActivo : nombreApagado;
            etiquetas[i].fontStyle = esActivo ? FontStyles.Bold : FontStyles.Normal;

            float alto = esActivo ? altoFiguraActiva : altoFigura;
            figuras[i].sizeDelta = new Vector2(alto * TexAncho / (float)TexAlto, alto);
            figuras[i].localScale = Vector3.one;
        }
    }

    private void Update()
    {
        if (!latir || activo < 0 || activo >= figuras.Count) return;

        float escala = 1f + Mathf.Sin(Time.time * velocidadLatido * Mathf.PI) * amplitudLatido;
        figuras[activo].localScale = new Vector3(escala, escala, 1f);
    }

    private void Limpiar()
    {
        foreach (RectTransform figura in figuras)
        {
            if (figura != null && figura.parent != null) Destroy(figura.parent.gameObject);
        }

        figuras.Clear();
        imagenes.Clear();
        etiquetas.Clear();
        colores.Clear();
        activo = -1;
    }

    // ===============================================================
    // SILUETA DEL MUNEQUITO
    //
    // Mismas tres partes que el prefab 3D: aro en la base, cuerpo de
    // capsula y cabeza esferica. Se dibuja en blanco y se tinta con el
    // color de cada jugador, que es el mismo del marcador de la escena.
    // ===============================================================
    private static Sprite Silueta()
    {
        if (siluetaCache != null) return siluetaCache;

        var textura = new Texture2D(TexAncho, TexAlto, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "SiluetaJugador"
        };

        var pixeles = new Color32[TexAncho * TexAlto];

        const float cx = TexAncho * 0.5f;

        for (int y = 0; y < TexAlto; y++)
        {
            for (int x = 0; x < TexAncho; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;   // la fila 0 de una Texture2D es la de abajo

                // Distancia con signo a cada parte (negativa = adentro).
                // Proporciones tomadas del prefab 3D: la cabeza es 1.5 veces
                // mas ancha que la capsula del cuerpo, y el aro de la base
                // mide lo mismo que la cabeza. Sin eso la silueta sale como
                // un pilar redondeado en vez de un munequito.
                float aro = DistanciaElipse(px, py, cx, 20f, 42f, 12f);
                float cuerpo = DistanciaCapsula(px, py, cx, 34f, cx, 96f, 17f);
                float cabeza = Vector2.Distance(new Vector2(px, py), new Vector2(cx, 122f)) - 26f;

                float d = Mathf.Min(aro, Mathf.Min(cuerpo, cabeza));

                // borde suave de ~1.5 px para que no quede aserrado
                float alfa = Mathf.Clamp01(0.5f - d / 1.5f);

                pixeles[y * TexAncho + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
            }
        }

        textura.SetPixels32(pixeles);
        textura.Apply(false, true);

        siluetaCache = Sprite.Create(textura,
            new Rect(0f, 0f, TexAncho, TexAlto),
            new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect);
        siluetaCache.name = "SiluetaJugador";

        return siluetaCache;
    }

    private static float DistanciaCapsula(float px, float py,
                                          float ax, float ay, float bx, float by, float r)
    {
        Vector2 p = new Vector2(px - ax, py - ay);
        Vector2 ab = new Vector2(bx - ax, by - ay);

        float t = Mathf.Clamp01(Vector2.Dot(p, ab) / Vector2.Dot(ab, ab));
        return (p - ab * t).magnitude - r;
    }

    private static float DistanciaElipse(float px, float py, float cx, float cy,
                                         float rx, float ry)
    {
        // Aproximacion suficiente para una mascara: se escala a circulo y se
        // devuelve la distancia multiplicada por el radio menor.
        float nx = (px - cx) / rx;
        float ny = (py - cy) / ry;

        return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
    }

    private static RectTransform NuevoHijo(string nombre, RectTransform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(padre, false);
        rt.localScale = Vector3.one;
        return rt;
    }
}
