using UnityEngine;
using UnityEngine.UI;
using HandTracking.Core;

namespace HandTracking.Visualization
{
    /// <summary>
    /// Отображает видео с камеры на 3D Quad перед Main Camera.
    /// Это позволяет рендерить 3D сферу поверх видео.
    /// RawImage (CameraDisplay) делается прозрачным - видео показывает этот Quad.
    /// </summary>
    public class VideoQuadDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private Camera _mainCamera;
        [Tooltip("RawImage с видео в Canvas — делаем прозрачным, т.к. видео теперь на Quad")]
        [SerializeField] private RawImage _cameraDisplayToHide;

        [Header("Settings")]
        [SerializeField] private float _distanceFromCamera = 5f;

        private GameObject _quad;
        private MeshRenderer _quadRenderer;
        private Material _quadMaterial;

        /// <summary>
        /// Transform Quad-а для позиционирования 3D объектов относительно видео
        /// </summary>
        public Transform QuadTransform => _quad != null ? _quad.transform : null;

        private void Start()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            // Автоматически находим CameraDisplay если не назначен вручную
            if (_cameraDisplayToHide == null && _cameraManager != null && _cameraManager.CameraDisplay != null)
            {
                _cameraDisplayToHide = _cameraManager.CameraDisplay;
                //Debug.Log("[VideoQuadDisplay.Start] CameraDisplay auto-resolved from CameraManager");
            }

            CreateQuad();

            // Подписываемся на событие готовности кадра для назначения текстуры
            if (_cameraManager != null)
                _cameraManager.OnFrameReady += OnFirstFrame;
        }

        private void CreateQuad()
        {
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "VideoQuad";
            _quad.transform.SetParent(transform);

            // Удаляем коллайдер
            var collider = _quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _quadRenderer = _quad.GetComponent<MeshRenderer>();

            // Пробуем Unlit/Texture (нужно добавить в Always Included Shaders в Project Settings → Graphics)
            // Fallback: Standard шейдер (всегда есть в билде)
            var unlitShader = Shader.Find("Unlit/Texture");
            if (unlitShader != null)
            {
                _quadMaterial = new Material(unlitShader);
                //Debug.Log("[VideoQuadDisplay.CreateQuad] Using Unlit/Texture shader");
            }
            else
            {
                Debug.LogWarning("[VideoQuadDisplay.CreateQuad] Unlit/Texture not found, using Standard. Add it to Always Included Shaders!");
                _quadMaterial = new Material(Shader.Find("Standard"));
                _quadMaterial.SetFloat("_Glossiness", 0f);
                _quadMaterial.SetFloat("_Metallic", 0f);
                _quadMaterial.EnableKeyword("_EMISSION");
            }
            _quadRenderer.material = _quadMaterial;
            
            UpdateQuadTransform();
        }

        private void OnFirstFrame(Texture2D frame)
        {
            if (_cameraManager.WebCamTexture != null && _quadMaterial != null)
            {
                _quadMaterial.mainTexture = _cameraManager.WebCamTexture;
                _quadMaterial.SetTexture("_EmissionMap", _cameraManager.WebCamTexture);
                _quadMaterial.SetColor("_EmissionColor", Color.white);
                Debug.Log($"[VideoQuadDisplay] WebCamTexture assigned: {_cameraManager.WebCamTexture.width}x{_cameraManager.WebCamTexture.height}, rotation: {_cameraManager.VideoRotationAngle}");
            }

            // Скрываем RawImage — теперь видео отображает 3D Quad.
            // RectTransform CameraDisplay остаётся для координатной привязки HandOverlayRenderer.
            if (_cameraDisplayToHide != null)
            {
                _cameraDisplayToHide.color = new Color(1f, 1f, 1f, 0f);
                Debug.Log("[VideoQuadDisplay] CameraDisplay hidden (alpha=0)");
            }

            UpdateQuadTransform();
            _cameraManager.OnFrameReady -= OnFirstFrame;
        }

        /// <summary>
        /// Позиционируем перед камерой
        /// </summary>
        private void UpdateQuadTransform()
        {
            if (_mainCamera == null || _quad == null) return;
            
            _quad.transform.position = _mainCamera.transform.position
                                       + _mainCamera.transform.forward * _distanceFromCamera;

            // Поворот Quad: только ориентация камеры (без Android-специфичных поворотов)
            _quad.transform.rotation = _mainCamera.transform.rotation;

            // Размеры RAW текстуры
            float rawW = 4f, rawH = 3f;
            if (_cameraManager != null && _cameraManager.WebCamTexture != null && _cameraManager.WebCamTexture.width > 0)
            {
                rawW = _cameraManager.WebCamTexture.width;
                rawH = _cameraManager.WebCamTexture.height;
            }

            float videoVisualAspect = rawW / rawH;

            // Размеры frustum камеры
            float frustumHeight = 2.0f * _distanceFromCamera * Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * _mainCamera.aspect;
            float screenAspect = _mainCamera.aspect;

            // Желаемые экранные размеры видео (в единицах frustum)
            float desiredScreenW, desiredScreenH;
            if (screenAspect > videoVisualAspect)
            {
                // Если экран шире видео, то заполняем по высоте, чёрные полосы по бокам
                desiredScreenH = frustumHeight;
                desiredScreenW = desiredScreenH * videoVisualAspect;
            }
            else
            {
                // Если Экран уже видео, то заполняем по ширине, чёрные полосы сверху/снизу
                desiredScreenW = frustumWidth;
                desiredScreenH = desiredScreenW / videoVisualAspect;
            }

            _quad.transform.localScale = new Vector3(desiredScreenW, desiredScreenH, 1f);

            Debug.Log($"[VideoQuadDisplay] raw={rawW}x{rawH} " +
                      $"visual={videoVisualAspect:F2} screen={screenAspect:F2} " +
                      $"quad=({desiredScreenW:F2}, {desiredScreenH:F2})");

            // Подстраиваем размер CameraDisplay под реальную область видео.
            // HandOverlayRenderer и FingerSphereController используют CameraDisplay для координат.
            if (_cameraDisplayToHide != null)
            {
                // Пиксельные размеры видео-области на экране
                float pixelW = desiredScreenW / frustumWidth  * Screen.width;
                float pixelH = desiredScreenH / frustumHeight * Screen.height;

                var rt = _cameraDisplayToHide.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(pixelW, pixelH);

                Debug.Log($"[VideoQuadDisplay] CameraDisplay resized to ({pixelW:F0}x{pixelH:F0})");
            }
        }

        private void OnDestroy()
        {
            if (_cameraManager != null)
                _cameraManager.OnFrameReady -= OnFirstFrame;

            if (_quadMaterial != null)
                Destroy(_quadMaterial);
        }
    }
}
