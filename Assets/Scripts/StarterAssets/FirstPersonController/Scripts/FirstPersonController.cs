using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;



        [Header("Crouch Settings")]
        [Tooltip("Height of the character when crouching")]
        public float CrouchHeight = 1.0f; // 앉았을 때 캐릭터 컨트롤러 높이

        // 즉시 크라우치 전환, 부드러운 전환 제거

        // 원래 1초? 정도로 해서 앉기 구현하려 했는데 속도감 안나고 답답해서 즉시 앉기로 수정
        private float _defaultHeight;  // 일어서기 전 기본 높이
        private Vector3 _defaultCenter; // 일어서기 전 기본 콜라이더 중심
        private Vector3 _crouchCenter; // 앉았을 때 콜라이더 중심
		 
        private float _defaultCameraY; // 일어서기 전 카메라 Y 위치
        private float _crouchCameraY; // 앉았을 때 카메라 Y 위치

        private bool _isCrouching; // 현재 앉아있는지 여부


        // 슬라이딩 기초값 삽입
        [Header("Slide Settings")] 
        [Tooltip("Initial speed of the slide")] public float SlideInitialSpeed = 8.0f; // 슬라이드 시작 속도
        [Tooltip("Rate at which slide speed decays per second")] public float SlideDecayRate = 4.0f; // 초당 얼마나 속도를 줄일지

        private bool _isSliding; // 현재 슬라이드 중인지
        private float _currentSlideSpeed; // 프레임마다 갱신되는 슬라이드 속도



        // cinemachine
        private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;


            // crouch 초기 설정
            _defaultHeight = _controller.height;
            _defaultCenter = _controller.center;
            _crouchCenter = new Vector3(_defaultCenter.x, CrouchHeight / 2f, _defaultCenter.z);

            // 앉기를 누를 경우 카메라 위치를 y값의 절반으로 설정
            _defaultCameraY = CinemachineCameraTarget.transform.localPosition.y;
            _crouchCameraY = _defaultCameraY * 0.5f;
        
		}

        private void Update()
		{

			//앉기 컨트롤러
            HandleCrouch();

			//슬라이딩 컨트롤러
            HandleSlide();


            // 슬라이드 중에는 기본 이동 로직을 건너뜀
            if (_isSliding)
                return;

            JumpAndGravity();
			GroundedCheck();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

        private void HandleSlide()
        {

            // Input System 전용 처리 부분
            bool crouchHeld = Keyboard.current.leftCtrlKey.isPressed; // <-- 수정된 부분: Input.GetKey -> Input System API

            if (!_isSliding && _input.sprint && crouchHeld && Grounded)
            {

                // Shift+Ctrl 누르고 땅에 서 있을 때만 슬라이드 시작
                _isSliding = true;
                _currentSlideSpeed = SlideInitialSpeed;
            }
            if (_isSliding)
            {
                // 앉기 해제 시 슬라이드 종료 처리
                if (!Keyboard.current.leftCtrlKey.isPressed) { _isSliding = false; return; } // 슬라이드 중엔 기본 Move() 건너뜀
                if (_currentSlideSpeed > 0f)
                {
                    Vector3 dir = transform.forward;
                    // 1) 내리막일 때만 가속 추가
                    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.5f, GroundLayers))
                    {
                        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                        if (slopeAngle > 0f)
                        {
                            // 중력 성분을 이용한 가속량: g * sin(theta)

                            // float slopeAccel = (중력 크기, 15) * Sin(경사각 in Radian);
							//경사각이 0° (완전 평지) 이면 Sin(0) = 0 → slopeAccel = 0 → 가속 없음
							//경사각이 90° (완전 수직) 이면 Sin(90°)= 1 → slopeAccel = 15 → 중력 전체가 가속으로 작용
							//경사각이 30° 이면 Sin(30°)= 0.5 → slopeAccel = 15 * 0.5 = 7.5 → 내리막 가속 7.5m / s²
                            float slopeAccel = Mathf.Abs(Gravity) * Mathf.Sin(slopeAngle * Mathf.Deg2Rad);
                            _currentSlideSpeed += slopeAccel * Time.deltaTime;
                        }
                    }
                    // 2) 중력 계속 적용
                    _verticalVelocity += Gravity * Time.deltaTime;
                    // 3) 합쳐서 이동
                    Vector3 slideMove = dir * _currentSlideSpeed;
                    _controller.Move((slideMove + Vector3.up * _verticalVelocity) * Time.deltaTime);
                    // 4) 평지 혹은 여전히 슬라이드 중인 상태에서 속도 감쇠
                    _currentSlideSpeed = Mathf.Max(_currentSlideSpeed - SlideDecayRate * Time.deltaTime, 0f);
                }
            }
        }
        private void HandleCrouch()
        {
#if ENABLE_INPUT_SYSTEM
            bool crouch = Keyboard.current.leftCtrlKey.isPressed;
#else
    bool crouch = false;
#endif
            if (crouch && !_isCrouching) // Ctrl 눌렀을 경우
            {
                // 즉시 앉기
                _controller.height = CrouchHeight;
                _controller.center = _crouchCenter;
                Vector3 camPos = CinemachineCameraTarget.transform.localPosition;
                CinemachineCameraTarget.transform.localPosition = new Vector3(camPos.x, _crouchCameraY, camPos.z);
                _isCrouching = true;
            }
            else if (!crouch && _isCrouching) // Ctrl 안 눌렀을 경우
            {
                // 즉시 일어서기
                _controller.height = _defaultHeight;
                _controller.center = _defaultCenter;
                Vector3 camPos = CinemachineCameraTarget.transform.localPosition;
                CinemachineCameraTarget.transform.localPosition = new Vector3(camPos.x, _defaultCameraY, camPos.z);
                _isCrouching = false;
            }
        }


        private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}