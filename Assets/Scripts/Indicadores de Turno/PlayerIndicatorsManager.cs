using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enciende el indicador del jugador al que le toca y apaga los otros dos.
///
/// Si los tres campos quedan vacios en el Inspector, los busca solo en la
/// escena y los ordena por el nombre del objeto (Player1Marker, Player2Marker,
/// Player3Marker), asi el prefab de los munequitos se puede mover de sitio sin
/// tener que recablear nada.
/// </summary>
public class PlayerIndicatorsManager : MonoBehaviour
{
    public PlayerIndicator player1;
    public PlayerIndicator player2;
    public PlayerIndicator player3;

    private void Awake()
    {
        if (player1 == null || player2 == null || player3 == null)
            AutoDetectar();

        SetActivePlayer(0);   // arrancan todos apagados
    }

    private void AutoDetectar()
    {
        PlayerIndicator[] encontrados = FindObjectsByType<PlayerIndicator>(FindObjectsInactive.Include);

        List<PlayerIndicator> ordenados = new List<PlayerIndicator>(encontrados);
        ordenados.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (player1 == null && ordenados.Count > 0) player1 = ordenados[0];
        if (player2 == null && ordenados.Count > 1) player2 = ordenados[1];
        if (player3 == null && ordenados.Count > 2) player3 = ordenados[2];
    }

    /// <param name="playerNumber">1, 2 o 3. Cualquier otro valor apaga todos.</param>
    public void SetActivePlayer(int playerNumber)
    {
        if (player1 != null) player1.SetActive(playerNumber == 1);
        if (player2 != null) player2.SetActive(playerNumber == 2);
        if (player3 != null) player3.SetActive(playerNumber == 3);
    }
}
