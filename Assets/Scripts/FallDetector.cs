using UnityEngine;

// ボールにアタッチする。床から落下したことを検知し、GameManagerへ通知する
public class FallDetector : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private float fallThreshold = -15f;

    private void Update()
    {
        if (transform.position.y < fallThreshold && gameManager != null)
        {
            gameManager.OnBallFell();
        }
    }
}
