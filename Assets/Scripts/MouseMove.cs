using UnityEngine;
using UnityEngine.InputSystem;

public class MouseMove : MonoBehaviour
{
    [SerializeField] private float sensitivity = 500f;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Update()
    {
        // 마우스 움직임(delta) 값 읽기
        Vector2 delta = Mouse.current.delta.ReadValue();

        // 민감도·프레임 보정
        rotationY += delta.x * sensitivity * Time.deltaTime;
        rotationX += delta.y * sensitivity * Time.deltaTime;

        // 상하 회전 제한
        rotationX = Mathf.Clamp(rotationX, -30f, 35f);

        // 실제 오일러 회전 적용 (X축은 위/아래 반전)
        transform.eulerAngles = new Vector3(-rotationX, rotationY, 0f);
    }
}
