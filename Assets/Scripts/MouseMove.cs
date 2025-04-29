using UnityEngine;
using UnityEngine.InputSystem;

public class MouseMove : MonoBehaviour
{
    [SerializeField] private float sensitivity = 500f;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Update()
    {
        // ���콺 ������(delta) �� �б�
        Vector2 delta = Mouse.current.delta.ReadValue();

        // �ΰ����������� ����
        rotationY += delta.x * sensitivity * Time.deltaTime;
        rotationX += delta.y * sensitivity * Time.deltaTime;

        // ���� ȸ�� ����
        rotationX = Mathf.Clamp(rotationX, -30f, 35f);

        // ���� ���Ϸ� ȸ�� ���� (X���� ��/�Ʒ� ����)
        transform.eulerAngles = new Vector3(-rotationX, rotationY, 0f);
    }
}
