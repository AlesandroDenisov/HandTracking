using UnityEngine;
using System.Collections;
using HandTracking.Core;
using TMPro;

namespace HandTracking
{
    /// <summary>
    /// Главный контроллер приложения (точка входа)
    /// </summary>
    public class AppController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PermissionManager _permissionManager;
        [SerializeField] private CameraManager _cameraManager;
        [SerializeField] private HandTrackingController _handTrackingController;

        [Header("UI")]
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private GameObject _errorPanel;
        [SerializeField] private TextMeshProUGUI _statusText;

        private void Start()
        {
            StartCoroutine(InitializeApp());
        }

        private IEnumerator InitializeApp()
        {
            ShowLoading(true);
            UpdateStatus("Requesting camera permission...");

            // Шаг 1: Запрос разрешений
            Debug.Log("[AppController.InitializeApp] Requesting camera permission...");

            bool permissionGranted = false;
            _permissionManager.OnPermissionGranted += () => permissionGranted = true;
            _permissionManager.OnPermissionDenied += () => ShowError("Camera permission denied");
            _permissionManager.RequestCameraPermission();

            yield return new WaitUntil(() => 
                permissionGranted || (_errorPanel != null && _errorPanel.activeSelf));

            if (!permissionGranted)
            {
                ShowLoading(false);
                yield break;
            }

            // Шаг 2: Инициализация камеры
            UpdateStatus("Initializing camera...");
            Debug.Log("[AppController.InitializeApp] Initializing camera...");
            yield return _cameraManager.Initialize();

            if (!_cameraManager.IsPlaying)
            {
                ShowError("Failed to initialize camera");
                ShowLoading(false);
                yield break;
            }

            // Шаг 3: Инициализация MediaPipe Hand Tracking
            UpdateStatus("Initializing MediaPipe...");
            Debug.Log("[AppController.InitializeApp] Initializing MediaPipe Hand Tracker...");

            if (_handTrackingController != null)
            {
                yield return _handTrackingController.Initialize();
            }
            else
            {
                Debug.LogError("[AppController.InitializeApp] HandTrackingController is null!");
            }

            // Завершение
            UpdateStatus("Ready!");
            ShowLoading(false);
            Debug.Log("[AppController.InitializeApp] App initialized successfully!");
        }

        private void ShowLoading(bool show)
        {
            if (_loadingPanel != null)
            {
                _loadingPanel.SetActive(show);
            }
        }

        private void UpdateStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
            Debug.Log($"[AppController.UpdateStatus] Status: {message}");
        }

        private void ShowError(string message)
        {
            Debug.LogError($"[AppController.ShowError] Error: {message}");
            if (_errorPanel != null)
            {
                _errorPanel.SetActive(true);
            }
            UpdateStatus($"Error: {message}");
        }
    }
}
