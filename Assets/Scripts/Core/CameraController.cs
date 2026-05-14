using UnityEngine;

/// <summary>
/// Камера для 2D метроидвании.
/// — Плавное следование через SmoothDamp
/// — В режиме Free стенда — следует за стендом, затем плавно возвращается к игроку
/// — Look-ahead: забегает вперёд по направлению движения
/// — Face offset: сдвиг в сторону взгляда
/// — Vertical peek: зажатый W/S сдвигает обзор
/// — Bounds: не выходит за границы уровня
/// </summary>
public class CameraController : MonoBehaviour
{
    // ──────────────────────────────────────────
    // Ссылки
    // ──────────────────────────────────────────

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private StandController  standController;

    // ──────────────────────────────────────────
    // Следование
    // ──────────────────────────────────────────

    [Header("Follow")]
    [SerializeField] private Vector2 baseOffset    = new Vector2(0f, 1f);
    [SerializeField] private float   smoothTimeX   = 0.18f;
    [SerializeField] private float   smoothTimeY   = 0.22f;

    // ──────────────────────────────────────────
    // Переключение на стенд
    // ──────────────────────────────────────────

    [Header("Stand Follow")]
    [Tooltip("Скорость перехода камеры к средней точке и обратно")]
    [SerializeField] private float standFollowSpeed = 3f;

    [Tooltip("Задержка перед возвратом к игроку после отпускания ПКМ")]
    [SerializeField] private float returnDelay = 0.5f;

    [Tooltip("Вес стенда при вычислении средней точки (0 = только игрок, 1 = только стенд)")]
    [SerializeField] [Range(0f, 1f)] private float standWeight = 0.4f;

    // ──────────────────────────────────────────
    // Look-ahead
    // ──────────────────────────────────────────

    [Header("Look-Ahead")]
    [SerializeField] private float lookAheadDist  = 2f;
    [SerializeField] private float lookAheadSpeed = 4f;

    // ──────────────────────────────────────────
    // Face offset
    // ──────────────────────────────────────────

    [Header("Face Offset")]
    [SerializeField] private float faceOffsetDist  = 1.2f;
    [SerializeField] private float faceOffsetSpeed = 2.5f;

    // ──────────────────────────────────────────
    // Vertical peek
    // ──────────────────────────────────────────

    [Header("Vertical Peek")]
    [SerializeField] private float peekDistance = 2.5f;
    [SerializeField] private float peekSpeed    = 2f;
    [SerializeField] private float peekDelay    = 0.4f;

    // ──────────────────────────────────────────
    // Bounds
    // ──────────────────────────────────────────

    [Header("Bounds")]
    [SerializeField] private bool  useBounds  = false;
    [SerializeField] private float boundsMinX = -50f;
    [SerializeField] private float boundsMaxX =  50f;
    [SerializeField] private float boundsMinY = -20f;
    [SerializeField] private float boundsMaxY =  20f;

    // ──────────────────────────────────────────
    // Приватные
    // ──────────────────────────────────────────

    private Vector2 _vel;               // SmoothDamp velocity

    // Плавная точка слежения (интерполируется между игроком и стендом)
    private Vector2 _lookTarget;

    private float _standBlend;          // 0 = игрок, 1 = стенд
    private float _returnTimer;         // таймер задержки возврата

    private float _lookAhead;
    private float _faceOffset;
    private float _peek;
    private float _peekTimer;
    private int   _peekDir;

    private Camera _cam;
    private float  _halfW, _halfH;

    // ──────────────────────────────────────────
    // Unity
    // ──────────────────────────────────────────

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null && _cam.orthographic)
        {
            _halfH = _cam.orthographicSize;
            _halfW = _halfH * _cam.aspect;
        }
    }

    private void Start()
    {
        if (playerController != null)
        {
            _lookTarget = (Vector2)playerController.transform.position + baseOffset;
            transform.position = new Vector3(_lookTarget.x, _lookTarget.y, transform.position.z);
        }
    }

    private void LateUpdate()
    {
        if (playerController == null) return;

        UpdateStandBlend();
        UpdateLookAhead();
        UpdateFaceOffset();
        UpdatePeek();

        // Базовая точка слежения:
        // когда стенд движется — камера смещается к средней точке (вес standWeight),
        // так оба объекта остаются в кадре
        Vector2 playerPos = (Vector2)playerController.transform.position;
        Vector2 standPos  = standController != null
            ? standController.Position
            : playerPos;

        // Средняя точка с весом — игрок всегда виден
        Vector2 midPoint = Vector2.Lerp(playerPos, standPos, standWeight);

        // _standBlend плавно включает/выключает слежение за средней точкой
        _lookTarget = Vector2.Lerp(playerPos, midPoint, _standBlend);

        // Итоговая желаемая позиция
        float desiredX = _lookTarget.x + baseOffset.x + _lookAhead + _faceOffset;
        float desiredY = _lookTarget.y + baseOffset.y + _peek;

        // SmoothDamp
        float newX = Mathf.SmoothDamp(transform.position.x, desiredX, ref _vel.x, smoothTimeX);
        float newY = Mathf.SmoothDamp(transform.position.y, desiredY, ref _vel.y, smoothTimeY);

        transform.position = new Vector3(newX, newY, transform.position.z);

        ApplyBounds();
    }

    // ──────────────────────────────────────────
    // Переключение цели: игрок ↔ стенд
    // ──────────────────────────────────────────

    private void UpdateStandBlend()
    {
        // Следуем за стендом только пока он активно движется (ПКМ зажат)
        bool standMoving = standController != null && standController.IsMovingToTarget;

        if (standMoving)
        {
            _returnTimer = returnDelay;
            _standBlend  = Mathf.MoveTowards(_standBlend, 1f, standFollowSpeed * Time.deltaTime);
        }
        else
        {
            if (_returnTimer > 0f)
                _returnTimer -= Time.deltaTime;
            else
                _standBlend = Mathf.MoveTowards(_standBlend, 0f, standFollowSpeed * Time.deltaTime);
        }
    }

    // ──────────────────────────────────────────
    // Look-ahead по скорости (только для игрока)
    // ──────────────────────────────────────────

    private void UpdateLookAhead()
    {
        // При следовании за стендом look-ahead убираем
        float blendedDist = Mathf.Lerp(lookAheadDist, 0f, _standBlend);

        float velX = playerController.Velocity.x;
        float target = Mathf.Abs(velX) > 0.1f
            ? Mathf.Sign(velX) * blendedDist
            : 0f;

        _lookAhead = Mathf.Lerp(_lookAhead, target, lookAheadSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────
    // Face offset по направлению взгляда
    // ──────────────────────────────────────────

    private void UpdateFaceOffset()
    {
        float blendedDist = Mathf.Lerp(faceOffsetDist, 0f, _standBlend);
        float target = playerController.FacingRight ? blendedDist : -blendedDist;
        _faceOffset = Mathf.Lerp(_faceOffset, target, faceOffsetSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────
    // Vertical peek — зажатый W / S
    // ──────────────────────────────────────────

    private void UpdatePeek()
    {
        // Peek только когда камера на игроке
        if (_standBlend > 0.1f)
        {
            _peek = Mathf.Lerp(_peek, 0f, peekSpeed * Time.deltaTime);
            _peekTimer = 0f;
            return;
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        int wantedDir = 0;
        if (kb != null)
        {
            if (kb.wKey.isPressed)      wantedDir =  1;
            else if (kb.sKey.isPressed) wantedDir = -1;
        }

        if (wantedDir != 0 && wantedDir == _peekDir)
            _peekTimer += Time.deltaTime;
        else
        {
            _peekDir   = wantedDir;
            _peekTimer = 0f;
        }

        float targetPeek = _peekTimer >= peekDelay ? _peekDir * peekDistance : 0f;
        _peek = Mathf.Lerp(_peek, targetPeek, peekSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────
    // Bounds
    // ──────────────────────────────────────────

    private void ApplyBounds()
    {
        if (!useBounds) return;

        float x = Mathf.Clamp(transform.position.x, boundsMinX + _halfW, boundsMaxX - _halfW);
        float y = Mathf.Clamp(transform.position.y, boundsMinY + _halfH, boundsMaxY - _halfH);
        transform.position = new Vector3(x, y, transform.position.z);
    }

    // ──────────────────────────────────────────
    // Публичное API
    // ──────────────────────────────────────────

    /// <summary>Мгновенный прыжок камеры к цели (загрузка сцены, чекпоинт)</summary>
    public void SnapToTarget()
    {
        if (playerController == null) return;
        Vector2 pos = (Vector2)playerController.transform.position + baseOffset;
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        _vel        = Vector2.zero;
        _lookAhead  = 0f;
        _faceOffset = 0f;
        _peek       = 0f;
        _standBlend = 0f;
    }

    // ──────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (useBounds)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Vector3 c = new Vector3((boundsMinX + boundsMaxX) * 0.5f,
                                    (boundsMinY + boundsMaxY) * 0.5f, 0f);
            Vector3 s = new Vector3(boundsMaxX - boundsMinX,
                                    boundsMaxY - boundsMinY, 0f);
            Gizmos.DrawWireCube(c, s);
        }
    }
}
