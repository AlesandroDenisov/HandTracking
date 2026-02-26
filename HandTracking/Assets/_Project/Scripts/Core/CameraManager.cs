using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace HandTracking.Core
{
    /// <summary>
    /// Управление WebCamTexture и конвертация кадров
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFPS = 30;
        [SerializeField] private bool useFrontCamera = true;

        [Header("UI")]
        [SerializeField] private RawImage cameraDisplay;

        /// <summary>
        /// RawImage для отображения камеры (используется также как координатная привязка)
        /// </summary>
        public RawImage CameraDisplay => cameraDisplay;

        public WebCamTexture WebCamTexture { get; private set; }
        public Texture2D CurrentFrame { get; private set; }
        public bool IsPlaying => WebCamTexture != null && WebCamTexture.isPlaying;
        public int VideoRotationAngle => WebCamTexture != null ? WebCamTexture.videoRotationAngle : 0;
        public bool IsFrontCamera => useFrontCamera;

        public event Action<Texture2D> OnFrameReady;

        private bool isInitialized;

        public IEnumerator Initialize()
        {
            if (isInitialized) yield break;

            //Debug.Log("[CameraManager.Initialize] Initializing camera...");

            // Ждём доступные камеры
            yield return new WaitUntil(() => WebCamTexture.devices.Length > 0);

            // Выбираем камеру
            string cameraName = GetCameraName();
            if (string.IsNullOrEmpty(cameraName))
            {
                Debug.LogError("[CameraManager.Initialize] No camera found!");
                yield break;
            }

            //Debug.Log($"[CameraManager.Initialize] Using camera: {cameraName}");

            // Создаём WebCamTexture
            WebCamTexture = new WebCamTexture(cameraName, requestedWidth, requestedHeight, requestedFPS);
            WebCamTexture.Play();

            // Ждём первый кадр
            yield return new WaitUntil(() => WebCamTexture.didUpdateThisFrame);

            Debug.Log($"[CameraManager.Initialize] Camera started: {WebCamTexture.width}x{WebCamTexture.height} @ {WebCamTexture.requestedFPS}fps");

            // Настраиваем UI
            if (cameraDisplay != null)
            {
                cameraDisplay.texture = WebCamTexture;
                AdjustCameraDisplayRotation();
            }

            // Создаём текстуру для кадров
            CurrentFrame = new Texture2D(
                WebCamTexture.width,
                WebCamTexture.height,
                TextureFormat.RGBA32,
                false
            );

            isInitialized = true;
            Debug.Log($"[CameraManager.Initialize] Initialized successfully!");
        }

        private string GetCameraName()
        {
            foreach (var device in WebCamTexture.devices)
            {
                Debug.Log($"[CameraManager] Found camera: {device.name} (Front: {device.isFrontFacing})");

                if (useFrontCamera && device.isFrontFacing)
                    return device.name;
                if (!useFrontCamera && !device.isFrontFacing)
                    return device.name;
            }

            // Fallback - первая камера
            if (WebCamTexture.devices.Length > 0)
            {
                Debug.LogWarning($"[CameraManager.GetCameraName] Preferred camera not found, using: {WebCamTexture.devices[0].name}");
                return WebCamTexture.devices[0].name;
            }

            return null;
        }

        private void AdjustCameraDisplayRotation()
        {
            if (cameraDisplay == null) return;

            // Корректировка ориентации для Android
            var rectTransform = cameraDisplay.rectTransform;

#if UNITY_ANDROID && !UNITY_EDITOR
/*
            // Поворот и отражение для фронтальной камеры
            int rotation = -WebCamTexture.videoRotationAngle;
            rectTransform.localEulerAngles = new Vector3(0, 0, rotation);

            if (useFrontCamera)
            {
                // Зеркальное отражение для фронтальной камеры
                cameraDisplay.uvRect = new Rect(1, 0, -1, 1);
            }
*/
#endif
            //Debug.Log($"[CameraManager.AdjustCameraDisplayRotation] Camera rotation: {WebCamTexture.videoRotationAngle}°");
        }

        private void Update()
        {
            if (!isInitialized || !WebCamTexture.didUpdateThisFrame) return;

            // Копируем пиксели в Texture2D
            CurrentFrame.SetPixels32(WebCamTexture.GetPixels32());
            CurrentFrame.Apply();

            OnFrameReady?.Invoke(CurrentFrame);
        }

        private void OnDestroy()
        {
            if (WebCamTexture != null)
            {
                WebCamTexture.Stop();
                Destroy(WebCamTexture);
            }

            if (CurrentFrame != null)
            {
                Destroy(CurrentFrame);
            }

            //Debug.Log("[CameraManager.OnDestroy] Destroyed");
        }
    }
}
