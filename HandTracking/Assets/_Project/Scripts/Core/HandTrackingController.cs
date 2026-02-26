using System;
using System.Collections;
using HandTracking.Utils;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using UnityEngine;

namespace HandTracking.Core
{
    /// <summary>
    /// Основной контроллер распознавания руки через MediaPipe
    /// </summary>
    public class HandTrackingController : MonoBehaviour
    {
        private const string MODEL_HAND_LANDMARKER = "hand_landmarker.bytes";
        private const int LANDMARKS_COUNT = 21;
        private const string UNKNOWN = "Unknown";

        [Header("References")]
        [SerializeField] private CameraManager cameraManager;

        [Header("MediaPipe Settings")]
        [SerializeField] private int numHands = 1;
        [SerializeField] [Range(0f, 1f)] private float minDetectionConfidence = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float minPresenceConfidence = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float minTrackingConfidence = 0.5f;

        public event Action<HandLandmarkerResult> OnHandDetected;
        public event Action OnHandLost;

        public HandLandmarkerResult? LastResult { get; private set; }
        public bool IsHandDetected => LastResult.HasValue &&
                                       LastResult.Value.handLandmarks != null &&
                                       LastResult.Value.handLandmarks.Count > 0;

        private HandLandmarker handLandmarker;
        private bool isInitialized;
        private long timestampMs;

        public IEnumerator Initialize()
        {
            if (isInitialized) yield break;

            Debug.Log("[HandTrackingController.Initialize] Starting initialization...");

            // ВАЖНО: Инициализируем MainThreadDispatcher в основном потоке
            // до того как MediaPipe начнёт отправлять callbacks
            _ = UnityMainThreadDispatcher.Instance;

            // Инициализация AssetLoader для загрузки модели из StreamingAssets
            yield return InitializeAssetLoader();

            // Создание BaseOptions
            var baseOptions = new Mediapipe.Tasks.Core.BaseOptions(
                Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                modelAssetPath: MODEL_HAND_LANDMARKER
            );

            // Создание HandLandmarkerOptions через конструктор
            var options = new HandLandmarkerOptions(
                baseOptions,
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                numHands: numHands,
                minHandDetectionConfidence: minDetectionConfidence,
                minHandPresenceConfidence: minPresenceConfidence,
                minTrackingConfidence: minTrackingConfidence,
                resultCallback: OnHandLandmarkerResult
            );

            try
            {
                handLandmarker = HandLandmarker.CreateFromOptions(options);
                Debug.Log("[HandTrackingController.Initialize] HandLandmarker created successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HandTrackingController.Initialize] Failed to create HandLandmarker: {e.Message}");
                yield break;
            }
            
            cameraManager.OnFrameReady += ProcessFrame;

            isInitialized = true;
            Debug.Log("[HandTrackingController.Initialize] Initialization complete!");
        }

        private IEnumerator InitializeAssetLoader()
        {
            Debug.Log($"[HandTrackingController.InitializeAssetLoader] Loading model: {MODEL_HAND_LANDMARKER}");

            // Используем StreamingAssetsResourceManager для Android
            var resourceManager = new StreamingAssetsResourceManager();

            // Provide the resource manager to AssetLoader
            Mediapipe.Unity.Sample.AssetLoader.Provide(resourceManager);
            
            yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(MODEL_HAND_LANDMARKER);

            Debug.Log("[HandTrackingController.InitializeAssetLoader] Model loaded successfully");
        }

        private void ProcessFrame(Texture2D frame)
        {
            if (!isInitialized || handLandmarker == null || frame == null) return;

            timestampMs = (long)(Time.realtimeSinceStartup * 1000);

            try
            {
                // Создаём Image из Texture2D
                var image = new Mediapipe.Image(frame);

                // Асинхронная детекция
                handLandmarker.DetectAsync(image, timestampMs);

                // Освобождаем ресурсы
                ((IDisposable)image).Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[HandTrackingController.ProcessFrame] Error processing frame: {e.Message}");
            }
        }

        private void OnHandLandmarkerResult(HandLandmarkerResult result, Image image, long timestamp)
        {
            // Callback вызывается из другого потока!
            // Используем MainThreadDispatcher для выполнения в главном потоке Unity
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                LastResult = result;

                if (result.handLandmarks != null && result.handLandmarks.Count > 0)
                {
                    OnHandDetected?.Invoke(result);
                }
                else
                {
                    LastResult = null;
                    OnHandLost?.Invoke();
                }
            });
        }

        /// <summary>
        /// Получить позицию кончика указательного пальца в нормализованных координатах (0-1)
        /// </summary>
        public Vector2? GetIndexFingerTipNormalized()
        {
            if (!IsHandDetected) return null;

            var handLandmarks = LastResult.Value.handLandmarks[0];
            var landmarks = handLandmarks.landmarks;
            if (landmarks == null || landmarks.Count < LANDMARKS_COUNT) return null;

            // Index 8 = INDEX_FINGER_TIP
            var tip = landmarks[8];
            return new Vector2(tip.x, tip.y);
        }

        /// <summary>
        /// Получить 3D позицию кончика указательного пальца в мировых координатах
        /// </summary>
        public Vector3? GetIndexFingerTipWorld()
        {
            if (!IsHandDetected || LastResult.Value.handWorldLandmarks == null) return null;

            var handWorldLandmarks = LastResult.Value.handWorldLandmarks[0];
            var worldLandmarks = handWorldLandmarks.landmarks;
            if (worldLandmarks == null || worldLandmarks.Count < LANDMARKS_COUNT) return null;

            var tip = worldLandmarks[8];
            // MediaPipe: X вправо, Y вниз, Z от камеры
            // Unity: X вправо, Y вверх, Z от камеры
            return new Vector3(tip.x, -tip.y, -tip.z);
        }

        /// <summary>
        /// Получить все 21 точку landmarks в нормализованных координатах
        /// </summary>
        public Vector2[] GetAllLandmarksNormalized()
        {
            if (!IsHandDetected) return null;

            var handLandmarks = LastResult.Value.handLandmarks[0];
            var landmarks = handLandmarks.landmarks;
            if (landmarks == null || landmarks.Count < LANDMARKS_COUNT) return null;

            var result = new Vector2[LANDMARKS_COUNT];
            for (int i = 0; i < LANDMARKS_COUNT; i++)
            {
                result[i] = new Vector2(landmarks[i].x, landmarks[i].y);
            }
            return result;
        }

        /// <summary>
        /// Получить handedness (левая/правая рука)
        /// </summary>
        public string GetHandedness()
        {
            if (!IsHandDetected || LastResult.Value.handedness == null || LastResult.Value.handedness.Count == 0)
                return UNKNOWN;

            var handedness = LastResult.Value.handedness[0];  // Classifications struct
            var categories = handedness.categories;            // List<Category>
            if (categories == null || categories.Count == 0)
                return UNKNOWN;

            return categories[0].categoryName; // "Left" или "Right"
        }

        private void OnDestroy()
        {
            if (cameraManager != null)
            {
                cameraManager.OnFrameReady -= ProcessFrame;
            }

            if (handLandmarker != null)
            {
                // HandLandmarker управляется автоматически
                handLandmarker = null;
            }

            //Debug.Log("[HandTrackingController.OnDestroy] Cleaned up resources");
        }
    }
}
