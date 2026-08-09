using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 画面上に「ステージX」「ステージクリア!」を一定時間だけ表示する
public class UIManager : MonoBehaviour
{
    [SerializeField] private Text stageText;
    [SerializeField] private Text clearText;
    [SerializeField] private float stageAnnounceDuration = 3f;
    [SerializeField] private float clearDisplayDuration = 2f;

    private Coroutine stageCoroutine;
    private Coroutine clearCoroutine;

    private void Awake()
    {
        SetActiveSafe(stageText, false);
        SetActiveSafe(clearText, false);
    }

    // ステージ開始時にGameManagerから呼ばれる
    public void ShowStageAnnouncement(int stage)
    {
        if (stageText == null)
        {
            return;
        }

        // クリア表示と入れ替わりで出るようにする
        if (clearCoroutine != null)
        {
            StopCoroutine(clearCoroutine);
            SetActiveSafe(clearText, false);
        }

        if (stageCoroutine != null)
        {
            StopCoroutine(stageCoroutine);
        }
        stageCoroutine = StartCoroutine(ShowTemporarily(stageText, $"ステージ {stage}", stageAnnounceDuration));
    }

    // ゴール到達時にGameManagerから呼ばれる
    public void ShowClear()
    {
        if (clearText == null)
        {
            return;
        }

        if (clearCoroutine != null)
        {
            StopCoroutine(clearCoroutine);
        }
        clearCoroutine = StartCoroutine(ShowTemporarily(clearText, "ステージクリア!", clearDisplayDuration));
    }

    private IEnumerator ShowTemporarily(Text text, string message, float duration)
    {
        text.text = message;
        text.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        text.gameObject.SetActive(false);
    }

    private void SetActiveSafe(Text text, bool isActive)
    {
        if (text != null)
        {
            text.gameObject.SetActive(isActive);
        }
    }
}
