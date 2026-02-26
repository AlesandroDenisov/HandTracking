using UnityEngine;
using UnityEngine.Android;
using System;
using System.Collections;

namespace HandTracking.Core
{
    /// <summary>
    /// Управление разрешениями Android (камера)
    /// </summary>
    public class PermissionManager : MonoBehaviour
    {
        public event Action OnPermissionGranted;
        public event Action OnPermissionDenied;

        public bool HasCameraPermission =>
#if UNITY_ANDROID && !UNITY_EDITOR
            Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
            true;
#endif

        public void RequestCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!HasCameraPermission)
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += OnPermissionGrantedCallback;
                callbacks.PermissionDenied += OnPermissionDeniedCallback;
                callbacks.PermissionDeniedAndDontAskAgain += OnPermissionDeniedCallback;

                Permission.RequestUserPermission(Permission.Camera, callbacks);
            }
            else
            {
                OnPermissionGranted?.Invoke();
            }
#else
            OnPermissionGranted?.Invoke();
#endif
        }

        private void OnPermissionGrantedCallback(string permission)
        {
            Debug.Log($"[PermissionManager.OnPermissionGrantedCallback] Camera permission granted: {permission}");
            OnPermissionGranted?.Invoke();
        }

        private void OnPermissionDeniedCallback(string permission)
        {
            OnPermissionDenied?.Invoke();
            Debug.LogError($"[PermissionManager.OnPermissionDeniedCallback] Camera permission denied: {permission}");
        }
    }
}
