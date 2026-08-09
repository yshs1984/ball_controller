using UnityEngine;

// ステージ進行とクリアタイム計測を管理する
public class GameManager : MonoBehaviour
{
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private UIManager uiManager;

    private int currentStage = 1;
    private float stageStartTime;

    private void Start()
    {
        StartStage();
    }

    private void StartStage()
    {
        stageStartTime = Time.time;
        Debug.Log($"Stage {currentStage} Start");

        if (uiManager != null)
        {
            uiManager.ShowStageAnnouncement(currentStage);
        }
    }

    // GoalTriggerから呼ばれる
    public void OnGoalReached()
    {
        float clearTime = Time.time - stageStartTime;
        Debug.Log($"Stage {currentStage} Clear! Time: {clearTime:F2}s");

        if (uiManager != null)
        {
            uiManager.ShowClear();
        }

        currentStage++;

        if (mazeGenerator != null)
        {
            mazeGenerator.GenerateNewStage(currentStage);
        }

        StartStage();
    }

    // FallDetectorから呼ばれる。迷路は再生成せず、同じ迷路でボールの位置だけ戻す
    public void OnBallFell()
    {
        Debug.Log($"Fall! Retry Stage {currentStage}");

        if (mazeGenerator != null)
        {
            mazeGenerator.ResetBall();
        }
    }
}
