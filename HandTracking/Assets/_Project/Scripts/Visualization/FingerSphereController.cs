using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using HandTracking.Core;

namespace HandTracking.Visualization
{
    /// <summary>
    /// 3D сфера, следующая за кончиком указательного пальца.
    /// Координаты берутся из CameraDisplay (так же, как HandOverlayRenderer),
    /// поэтому сфера всегда точно совпадает с точкой landmark 8.
    /// Сфера рендерится ПЕРЕД VideoQuad (ближе к камере), поэтому видна поверх видео.
    /// </summary>
    public class FingerSphereController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HandTrackingController _handController;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private UnityEngine.UI.RawImage _cameraDisplay;
        [SerializeField] private CameraManager _cameraManager;

        [Header("Sphere Settings")]
        [SerializeField] private float _sphereScale = 1f;
        [SerializeField] private Color _sphereColor = Color.red;
        [SerializeField] private bool _useEmission = true;

        [Header("Position Settings")]
        [Tooltip("Расстояние сферы от камеры. Должно быть МЕНЬШЕ чем Distance у VideoQuadDisplay!")]
        [SerializeField] private float _distanceFromCamera = 4.5f;

        [Header("Smoothing")]
        [SerializeField] private float _smoothSpeed = 10f;
        [SerializeField] private bool _useSmoothing = true;

        private GameObject _sphere;
        private MeshRenderer _sphereRenderer;
        private Vector3 _targetPosition;
        private bool _isVisible;

        private void Awake()
        {
            CreateSphere();

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // Автоматически находим CameraDisplay если не назначен вручную
            if (_cameraDisplay == null && _cameraManager != null && _cameraManager.CameraDisplay != null)
            {
                _cameraDisplay = _cameraManager.CameraDisplay;
                //Debug.Log("[FingerSphereController.Awake] CameraDisplay auto-resolved from CameraManager");
            }
        }

        private void CreateSphere()
        {
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.transform.SetParent(transform);
            _sphere.transform.localScale = Vector3.one * _sphereScale;
            _sphere.transform.localPosition = Vector3.zero;
            _sphere.name = "FingerSphere";

            // Настраиваем материал
            _sphereRenderer = _sphere.GetComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = _sphereColor;

            if (_useEmission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", _sphereColor * 0.5f);
            }

            _sphereRenderer.material = material;

            // Удаляем коллайдер
            var collider = _sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            SetVisible(false);
            Debug.Log("[FingerSphereController.CreateSphere] Sphere created");
        }

        private void Start()
        {
            if (_handController != null)
            {
                _handController.OnHandDetected += OnHandDetected;
                _handController.OnHandLost += OnHandLost;
            }
            else
            {
                Debug.LogError("[FingerSphereController.Start] HandTrackingController is null!");
            }
        }

        private void Update()
        {
            if (!_isVisible) return;

            if (_useSmoothing)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    _targetPosition,
                    Time.deltaTime * _smoothSpeed
                );
            }
            else
            {
                transform.position = _targetPosition;
            }
        }

        private void OnHandDetected(HandLandmarkerResult result)
        {
            var fingerTip = _handController.GetIndexFingerTipNormalized();

            if (fingerTip.HasValue)
            {
                _targetPosition = CalculateWorldPosition(fingerTip.Value);
                SetVisible(true);
                Debug.Log($"[FingerSphere] normalized={fingerTip.Value} worldPos={_targetPosition} cameraDisplay={(_cameraDisplay != null ? "OK" : "NULL")}");
            }
            else
            {
                SetVisible(false);
            }
        }

        private void OnHandLost()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Рассчитывает 3D позицию сферы, используя ту же логику что HandOverlayRenderer.
        /// X/Y из CameraDisplay RectTransform -> screen point -> ViewportPointToRay -> точка на _distanceFromCamera.
        /// Результат: сфера точно совпадает с overlay-точкой, и находится перед VideoQuad.
        /// </summary>
        private Vector3 CalculateWorldPosition(Vector2 normalizedPos)
        {
            if (_mainCamera == null) return Vector3.zero;

            if (_cameraDisplay != null)
            {
                var rectTransform = _cameraDisplay.rectTransform;
                var width  = rectTransform.rect.width;
                var height = rectTransform.rect.height;
                var pivot  = rectTransform.pivot;

                // Та же формула что в HandOverlayRenderer.OnHandDetected
                float x = normalizedPos.x * width;
                float y = normalizedPos.y * height;

                var localPos = new Vector3(
                    x - width  * pivot.x,
                    y - height * pivot.y,
                    0
                );

                // Для Screen Space Overlay Canvas, TransformPoint уже даёт экранные пиксели
                var screenPos = rectTransform.TransformPoint(localPos);
                var viewportPos = new Vector3(
                    screenPos.x / Screen.width,
                    screenPos.y / Screen.height,
                    0f
                );

                // Луч из камеры и точка на заданном расстоянии (перед VideoQuad)
                var ray = _mainCamera.ViewportPointToRay(viewportPos);
                return ray.GetPoint(_distanceFromCamera);
            }

            // Fallback: прямой viewport без CameraDisplay
            var fallbackRay = _mainCamera.ViewportPointToRay(
                new Vector3(normalizedPos.x, normalizedPos.y, 0f));
            return fallbackRay.GetPoint(_distanceFromCamera);
        }

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;

            _isVisible = visible;
            if (_sphereRenderer != null)
                _sphereRenderer.enabled = visible;
        }

        private void OnDestroy()
        {
            if (_handController != null)
            {
                _handController.OnHandDetected -= OnHandDetected;
                _handController.OnHandLost -= OnHandLost;
            }
        }
    }
}