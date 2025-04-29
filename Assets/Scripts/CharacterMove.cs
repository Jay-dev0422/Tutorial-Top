using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    private CharacterController controller;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float jumpSpeed = 10f;
    [SerializeField] private float gravity = -20f;

    private float yVelocity = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1) Ű���� ���� �Է� �б� (WASD)
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        // 2) ī�޶� ���� �������� ���� ��ǥ ��ȯ
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        moveDir = cameraTransform.TransformDirection(moveDir) * moveSpeed;

        // 3) ���� & �߷�
        if (controller.isGrounded)
        {
            yVelocity = 0f;
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                yVelocity = jumpSpeed;
        }
        yVelocity += gravity * Time.deltaTime;
        moveDir.y = yVelocity;

        // 4) ���� �̵� ���
        controller.Move(moveDir * Time.deltaTime);
    }
}
