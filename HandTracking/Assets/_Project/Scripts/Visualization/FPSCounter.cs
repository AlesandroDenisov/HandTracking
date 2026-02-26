using TMPro;
using UnityEngine;

namespace HandTracking.Visualization
{
    /// <summary>
    /// Счётчик FPS с цветовой индикацией
    /// </summary>
    public class FPSCounter : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _fpsText;

        [Header("Settings")]
        [SerializeField] private float _updateInterval = 0.5f;
        [SerializeField] private int _targetFPS = 30;
        [SerializeField] private int _lowFPS = 20;

        [Header("Colors")]
        [SerializeField] private Color _goodColor = Color.green;      // > 30 FPS
        [SerializeField] private Color _warningColor = Color.yellow;  // 20-30 FPS
        [SerializeField] private Color _badColor = Color.red;         // < 20 FPS

        private float _accumulatedTime;
        private int _frameCount;
        private float _currentFPS;

        public float GetCurrentFPS() => _currentFPS;
        
        private void Update()
        {
            _accumulatedTime += Time.unscaledDeltaTime;
            _frameCount++;

            if (_accumulatedTime >= _updateInterval)
            {
                _currentFPS = _frameCount / _accumulatedTime;
                UpdateDisplay();

                _accumulatedTime = 0f;
                _frameCount = 0;
            }
        }

        private void UpdateDisplay()
        {
            if (_fpsText == null) return;

            _fpsText.text = $"FPS: {Mathf.RoundToInt(_currentFPS)}";

            // Устанавливаем цвет в зависимости от FPS
            if (_currentFPS >= _targetFPS)
            {
                _fpsText.color = _goodColor;
            }
            else if (_currentFPS >= _lowFPS)
            {
                _fpsText.color = _warningColor;
            }
            else
            {
                _fpsText.color = _badColor;
            }
        }

    }
}
