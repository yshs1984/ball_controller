using UnityEngine;

// WASD(または矢印キー)、あるいは画面のタップ&ドラッグでボールを転がす
[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    [SerializeField] private float moveForce = 10f;
    [SerializeField] private float dragRadius = 100f;

    private Rigidbody rb;
    private float inputHorizontal;
    private float inputVertical;
    private Vector2 dragOrigin;
    private bool isDragging;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 入力の読み取りはUpdateで行う
        float keyboardHorizontal = Input.GetAxis("Horizontal");
        float keyboardVertical = Input.GetAxis("Vertical");

        Vector2 dragInput = ReadDragInput();

        // キーボードとドラッグを合算し、暴走しないよう-1〜1に収める
        inputHorizontal = Mathf.Clamp(keyboardHorizontal + dragInput.x, -1f, 1f);
        inputVertical = Mathf.Clamp(keyboardVertical + dragInput.y, -1f, 1f);
    }

    private Vector2 ReadDragInput()
    {
        // WebGLではブラウザのタッチがマウスイベントとしてエミュレートされるため、
        // GetMouseButton系だけでPCのマウスドラッグとスマホのタップ&ドラッグの両方をカバーできる
        if (Input.GetMouseButtonDown(0))
        {
            dragOrigin = Input.mousePosition;
            isDragging = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (!isDragging)
        {
            return Vector2.zero;
        }

        Vector2 delta = (Vector2)Input.mousePosition - dragOrigin;
        return Vector2.ClampMagnitude(delta / dragRadius, 1f);
    }

    private void FixedUpdate()
    {
        // 物理演算への反映はFixedUpdateで行う(フレームレート依存を避けるため)
        Vector3 force = new Vector3(inputHorizontal, 0f, inputVertical) * moveForce;
        rb.AddForce(force);
    }
}
