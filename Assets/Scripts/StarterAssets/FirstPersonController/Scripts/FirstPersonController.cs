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


        // -----------------수정된 부분 시작 ----------------
        [Header("Crouch Settings")]
        [Tooltip("Height of the character when crouching")]
        public float CrouchHeight = 1.0f;

        // -----------------수정된 부분 시작 ----------------
        // 즉시 크라우치 전환, 부드러운 전환 제거
        private float _defaultHeight;
        private Vector3 _defaultCenter;
        private Vector3 _crouchCenter;

        private float _defaultCameraY;
        private float _crouchCameraY;

        private bool _isCrouching;
        // -----------------수정된 부분 끝 ----------------


        // ---------------- 슬라이딩 로직 추가 -------------------
        [Header("Slide Settings")]
        [Tooltip("Initial speed of the slide")] public float SlideInitialSpeed = 8.0f;
        [Tooltip("Rate at which slide speed decays per second")] public float SlideDecayRate = 4.0f;

        private bool _isSliding;
        private float _currentSlideSpeed;
        // ---------------- 슬라이딩 로직 추가 끝 -------------------



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


            // -----------------수정된 부분 시작 ----------------
            // crouch 초기 설정
            _defaultHeight = _controller.height;
            _defaultCenter = _controller.center;
            _crouchCenter = new Vector3(_defaultCenter.x, CrouchHeight / 2f, _defaultCenter.z);

            // camera 초기 설정
            _defaultCameraY = CinemachineCameraTarget.transform.localPosition.y;
            _crouchCameraY = _defaultCameraY * 0.5f;
            // ----------------수정된 부분 끝 ----------------------------
        
		}

        private void Update()
		{

            // -----------------수정된 부분 시작 ----------------
            HandleCrouch();
            // ----------------수정된 부분 끝 ----------------------------


            // ---------------- 슬라이딩 로직 추가 -------------------
            HandleSlide();
            // ---------------- 슬라이딩 로직 추가 끝 -------------------
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
        // ---------------- 슬라이딩 로직 수정 -------------------
        private void HandleSlide()
        {
            // Input System 전용 처리 부분
            bool crouchHeld = Keyboard.current.leftCtrlKey.isPressed; // <-- 수정된 부분: Input.GetKey -> Input System API

            if (!_isSliding && _input.sprint && crouchHeld && Grounded)
            {
                _isSliding = true;
                _currentSlideSpeed = SlideInitialSpeed;
            }
            if (_isSliding)
            {
                // 앉기 해제 시 슬라이드 종료 처리
                if (!Keyboard.current.leftCtrlKey.isPressed) { _isSliding = false; return; } // <-- 수정된 부분
                if (_currentSlideSpeed > 0f)
                {
                    Vector3 dir = transform.forward;

                    // ---------------- 슬라이딩 로직 수정 전 ----------------
                    // _controller.Move(dir * _currentSlideSpeed * Time.deltaTime);
                    // ---------------- 슬라이딩 로직 수정 후 ----------------
                    // 1) 계속 중력 적용
                    _verticalVelocity += Gravity * Time.deltaTime;
                    // 2) 앞 방향 슬라이드 벡터
                    Vector3 slideMove = dir * _currentSlideSpeed;
                    // 3) 중력까지 합쳐서 한 번에 Move
                    _controller.Move((slideMove + Vector3.up * _verticalVelocity) * Time.deltaTime);
                    // 4) 속도 감쇠
                    _currentSlideSpeed = Mathf.Max(_currentSlideSpeed - SlideDecayRate * Time.deltaTime, 0f);
                    // ---------------- 슬라이딩 로직 수정 끝 -----------------

                    _currentSlideSpeed = Mathf.Max(_currentSlideSpeed - SlideDecayRate * Time.deltaTime, 0f);
                }
            }
        }
        // ---------------- 슬라이딩 로직 수정 끝 -------------------

        // ---------------- 크라우치 로직 수정 -------------------
        private void HandleCrouch()
        {
#if ENABLE_INPUT_SYSTEM
            bool crouch = Keyboard.current.leftCtrlKey.isPressed;
#else
    bool crouch = false;
#endif
            if (crouch && !_isCrouching)
            {
                // 즉시 앉기
                _controller.height = CrouchHeight;
                _controller.center = _crouchCenter;
                Vector3 camPos = CinemachineCameraTarget.transform.localPosition;
                CinemachineCameraTarget.transform.localPosition = new Vector3(camPos.x, _crouchCameraY, camPos.z);
                _isCrouching = true;
            }
            else if (!crouch && _isCrouching)
            {
                // 즉시 일어서기
                _controller.height = _defaultHeight;
                _controller.center = _defaultCenter;
                Vector3 camPos = CinemachineCameraTarget.transform.localPosition;
                CinemachineCameraTarget.transform.localPosition = new Vector3(camPos.x, _defaultCameraY, camPos.z);
                _isCrouching = false;
            }
        }
        // ---------------- 크라우치 로직 수정 끝 -------------------


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