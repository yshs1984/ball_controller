using UnityEngine;

// ゴールオブジェクトにアタッチする。Colliderの Is Trigger を有効にしておくこと
public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Clear!");

            if (gameManager != null)
            {
                gameManager.OnGoalReached();
            }
        }
    }
}
