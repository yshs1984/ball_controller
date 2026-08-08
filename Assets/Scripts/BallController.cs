using UnityEngine;

// WASD(または矢印キー)の入力でボールを転がす
[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    [SerializeField] private float moveForce = 10f;

    private Rigidbody rb;
    private float inputHorizontal;
    private float inputVertical;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 入力の読み取りはUpdateで行う
        inputHorizontal = Input.GetAxis("Horizontal");
        inputVertical = Input.GetAxis("Vertical");
    }

    private void FixedUpdate()
    {
        // 物理演算への反映はFixedUpdateで行う(フレームレート依存を避けるため)
        Vector3 force = new Vector3(inputHorizontal, 0f, inputVertical) * moveForce;
        rb.AddForce(force);
    }
}
