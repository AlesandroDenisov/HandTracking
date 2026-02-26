using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mediapipe.Tasks.Vision.HandLandmarker;
using HandTracking.Core;

namespace HandTracking.Visualization
{
    /// <summary>
    /// Рендеринг точек и линий руки
    /// </summary>
    public class HandOverlayRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HandTrackingController _handController;
        [SerializeField] private RectTransform _overlayContainer;
        [SerializeField] private RawImage _cameraDisplay;

        [Header("Point Settings")]
        [SerializeField] private float _pointRadius = 10f;
        [SerializeField] private Color _pointColor = Color.green;

        [Header("Line Settings")]
        [SerializeField] private float _lineWidth = 3f;
        [SerializeField] private Color _lineColor = Color.cyan;

        // 21 точка руки
        private readonly List<GameObject> _landmarkPoints = new List<GameObject>();
        private readonly List<Image> _connectionLines = new List<Image>();

        // Connections между точками (индексы из MediaPipe)
        private static readonly int[][] HAND_CONNECTIONS = new int[][]
        {
            // Thumb
            new[] {0, 1}, new[] {1, 2}, new[] {2, 3}, new[] {3, 4},
            // Index finger
            new[] {0, 5}, new[] {5, 6}, new[] {6, 7}, new[] {7, 8},
            // Middle finger
            new[] {0, 9}, new[] {9, 10}, new[] {10, 11}, new[] {11, 12},
            // Ring finger
            new[] {0, 13}, new[] {13, 14}, new[] {14, 15}, new[] {15, 16},
            // Pinky
            new[] {0, 17}, new[] {17, 18}, new[] {18, 19}, new[] {19, 20},
            // Palm
            new[] {5, 9}, new[] {9, 13}, new[] {13, 17}
        };

        private void Awake()
        {
            if (_overlayContainer == null)
            {
                Debug.LogError("[HandOverlayRenderer.Awake] OverlayContainer is not assigned!");
                return;
            }

            if (_cameraDisplay == null)
            {
                Debug.LogError("[HandOverlayRenderer.Awake] CameraDisplay is not assigned!");
                return;
            }

            InitializeLandmarkPoints();
            InitializeConnectionLines();
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
                Debug.LogError("[HandOverlayRenderer.Start] HandTrackingController is null!");
            }
        }

        private void InitializeLandmarkPoints()
        {
            // Создаём 21 точку
            for (int i = 0; i < 21; i++)
            {
                var point = new GameObject($"Landmark_{i}");
                point.transform.SetParent(_overlayContainer);

                // Создаём Image компонент для точки
                var image = point.AddComponent<Image>();
                image.color = _pointColor;

                var rectTransform = point.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(_pointRadius, _pointRadius);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;

                point.SetActive(false);
                _landmarkPoints.Add(point);
            }

            //Debug.Log($"[HandOverlayRenderer.InitializeLandmarkPoints] Created {_landmarkPoints.Count} landmark points");
        }

        private void InitializeConnectionLines()
        {
            // Создаём UI линии для каждого соединения
            for (int i = 0; i < HAND_CONNECTIONS.Length; i++)
            {
                var lineObj = new GameObject($"Connection_{i}");
                lineObj.transform.SetParent(_overlayContainer);

                // Создаём Image для линии
                var image = lineObj.AddComponent<Image>();
                image.color = _lineColor;

                var rectTransform = lineObj.GetComponent<RectTransform>();
                rectTransform.pivot = new Vector2(0, 0.5f); // Pivot в левом центре
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.zero;
                rectTransform.sizeDelta = new Vector2(100, _lineWidth); // Ширина будет динамической

                lineObj.SetActive(false);
                _connectionLines.Add(image);
            }

            //Debug.Log($"[HandOverlayRenderer.InitializeConnectionLines] Created {_connectionLines.Count} connection lines");
        }

        private void OnHandDetected(HandLandmarkerResult result)
        {
            if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            {
                OnHandLost();
                return;
            }

            var handLandmarks = result.handLandmarks[0];      // NormalizedLandmarks struct
            var landmarks = handLandmarks.landmarks;          // List<NormalizedLandmark>

            if (landmarks == null || landmarks.Count < 21)
            {
                OnHandLost();
                return;
            }

            // Получаем размер CameraDisplay
            var rectTransform = _cameraDisplay.rectTransform;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;
            var pivot = rectTransform.pivot;

            // Обновляем точки используя прямое преобразование координат
            for (int i = 0; i < 21 && i < landmarks.Count; i++)
            {
                var landmark = landmarks[i];

                // Прямое преобразование: координаты MediaPipe в пиксели
                // MediaPipe: X: 0=left, 1=right; Y: 0=top, 1=bottom
                float x = landmark.x * width;
                float y = landmark.y * height;

                // Создаём позицию в local space CameraDisplay с учётом pivot
                var localPos = new Vector3(
                    x - width * pivot.x,
                    y - height * pivot.y,
                    0
                );

                // Преобразуем координаты из пространства CameraDisplay в пространство OverlayContainer
                var worldPos = rectTransform.TransformPoint(localPos);
                var overlayLocalPos = _overlayContainer.InverseTransformPoint(worldPos);

                _landmarkPoints[i].transform.localPosition = overlayLocalPos;
                _landmarkPoints[i].SetActive(true);
            }
            
            UpdateConnectionLines();
        }

        private void OnHandLost()
        {
            // Скрываем все точки
            foreach (var point in _landmarkPoints)
            {
                if (point != null)
                    point.SetActive(false);
            }

            // Скрываем все линии
            foreach (var line in _connectionLines)
            {
                if (line != null)
                    line.gameObject.SetActive(false);
            }
        }

        private void UpdateConnectionLines()
        {
            for (int i = 0; i < HAND_CONNECTIONS.Length; i++)
            {
                var connection = HAND_CONNECTIONS[i];
                int startIdx = connection[0];
                int endIdx = connection[1];

                if (startIdx >= _landmarkPoints.Count || endIdx >= _landmarkPoints.Count)
                {
                    _connectionLines[i].gameObject.SetActive(false);
                    continue;
                }

                var startPoint = _landmarkPoints[startIdx];
                var endPoint = _landmarkPoints[endIdx];

                if (!startPoint.activeSelf || !endPoint.activeSelf)
                {
                    _connectionLines[i].gameObject.SetActive(false);
                    continue;
                }

                DrawLineBetweenPoints(
                    _connectionLines[i].rectTransform,
                    startPoint.transform.localPosition,
                    endPoint.transform.localPosition
                );

                _connectionLines[i].gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Рисует UI линию между двумя точками
        /// </summary>
        private void DrawLineBetweenPoints(RectTransform lineRect, Vector3 start, Vector3 end)
        {
            // Вычисляем направление и расстояние
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // Устанавливаем позицию (начало линии)
            lineRect.localPosition = start;

            // Устанавливаем размер (длина линии)
            lineRect.sizeDelta = new Vector2(distance, _lineWidth);

            // Вычисляем угол поворота
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            lineRect.localRotation = Quaternion.Euler(0, 0, angle);
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