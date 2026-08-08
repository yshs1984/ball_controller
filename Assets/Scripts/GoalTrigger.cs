using UnityEngine;

// ゴールオブジェクトにアタッチする。Colliderの Is Trigger を有効にしておくこと
public class GoalTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Clear!");
        }
    }
}
