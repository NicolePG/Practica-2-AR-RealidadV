using UnityEngine;

public class PlayerIndicatorsManager : MonoBehaviour
{
    public PlayerIndicator player1;
    public PlayerIndicator player2;
    public PlayerIndicator player3;

    public void SetActivePlayer(int playerNumber)
    {
        player1.SetActive(playerNumber == 1);
        player2.SetActive(playerNumber == 2);
        player3.SetActive(playerNumber == 3);
    }
}