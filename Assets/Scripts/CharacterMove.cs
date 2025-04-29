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
        // 1) 키보드 방향 입력 읽기 (WASD)
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;

        // 2) 카메라 방향 기준으로 월드 좌표 변환
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        moveDir = cameraTransform.TransformDirection(moveDir) * moveSpeed;

        // 3) 점프 & 중력
        if (controller.isGrounded)
        {
            yVelocity = 0f;
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                yVelocity = jumpSpeed;
        }
        yVelocity += gravity * Time.deltaTime;
        moveDir.y = yVelocity;

        // 4) 실제 이동 명령
        controller.Move(moveDir * Time.deltaTime);
    }
}
