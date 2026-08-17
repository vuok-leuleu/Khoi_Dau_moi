using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý hiệu ứng chuyển cảnh và focus camera hoàn toàn bằng đám mây 3D dưới Main Camera.
/// Tuyệt đối KHÔNG tạo bất kỳ UI Canvas nào bằng code.
/// Giữ đám mây 3D đóng kín khi chuyển scene sang SceneBattle và khi quay về scene chính rồi mở ra.
/// </summary>
public sealed class CloudSceneTransition : MonoBehaviour
{
    public const float DefaultTransitionDuration = 0.8f;
    private static CloudSceneTransition instance;

    private Coroutine transitionRoutine;
    private static bool shouldOpenCloudsOnSceneLoad = false;

    public static bool IsTransitioning => instance != null && instance.transitionRoutine != null;

    public static CloudSceneTransition Instance => GetOrCreate();

    public static CloudSceneTransition GetOrCreate()
    {
        if (instance != null) return instance;

        GameObject obj = new GameObject(nameof(CloudSceneTransition));
        instance = obj.AddComponent<CloudSceneTransition>();
        DontDestroyOnLoad(obj);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (shouldOpenCloudsOnSceneLoad)
        {
            shouldOpenCloudsOnSceneLoad = false;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(OpenCloudsAfterLoadRoutine(DefaultTransitionDuration * 0.5f));
        }
    }

    private IEnumerator OpenCloudsAfterLoadRoutine(float duration)
    {
        // Chờ 1-2 frame cho Camera và các GameObject đám mây ở scene mới sẵn sàng
        yield return null;
        yield return null;

        // Đảm bảo mây ở scene mới được đặt ở trạng thái che kín (closed)
        SetAllCloudsState(1f);

        // Mở mây ra 2 bên
        yield return AnimateCloudsToState(0f, duration);
        transitionRoutine = null;
    }

    /// <summary>
    /// Chuyển scene: Mây 3D từ 2 bên khép vào giữa che toàn màn hình -> Load scene mới -> Mây ở scene mới mở ra 2 bên
    /// </summary>
    public static void LoadSceneWithCloud(string sceneName, float duration = DefaultTransitionDuration)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[CloudSceneTransition] Tên scene bị trống!");
            return;
        }

        CloudSceneTransition transition = GetOrCreate();
        if (transition.transitionRoutine != null) transition.StopCoroutine(transition.transitionRoutine);
        transition.transitionRoutine = transition.StartCoroutine(transition.CloseAndLoadRoutine(sceneName, duration));
    }

    private IEnumerator CloseAndLoadRoutine(string sceneName, float duration)
    {
        float closeDuration = duration * 0.5f;

        // 1. Khép mây 3D vào giữa che kín toàn bộ màn hình
        yield return AnimateCloudsToState(1f, closeDuration);

        // Đánh dấu để scene tiếp theo sau khi load xong sẽ tự động mở mây ra
        shouldOpenCloudsOnSceneLoad = true;

        // 2. Load scene mới đằng sau màn mây
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp != null)
        {
            while (!asyncOp.isDone)
            {
                yield return null;
            }
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }

        // Sau khi load xong, OnSceneLoaded sẽ nhận sự kiện và thực thi OpenCloudsAfterLoadRoutine
    }

    /// <summary>
    /// Hiệu ứng Zoom Camera mượt kết hợp Đám mây 3D:
    /// Mây 3D khép vào giữa che kín -> Camera zoom mượt đến target -> Kích hoạt callback -> Mây 3D mở ra
    /// </summary>
    public static IEnumerator PlayCameraFocusWipeSmooth(Camera cam, Vector3 startPos, Vector3 targetPos, float totalDuration, Action onMiddleReached = null, Action onComplete = null)
    {
        CloudSceneTransition transition = GetOrCreate();
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        Transform cameraTransform = cam != null ? cam.transform : null;
        float duration = Mathf.Max(0.2f, totalDuration);
        float halfDuration = duration * 0.5f;

        // Pha 1: Mây 3D đóng vào giữa che kín màn hình, camera di chuyển mượt nửa chặng
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transition.SetAllCloudsState(smoothT);

            if (cameraTransform != null)
            {
                cameraTransform.position = Vector3.Lerp(startPos, targetPos, smoothT * 0.5f);
            }

            yield return null;
        }

        transition.SetAllCloudsState(1f);
        if (cameraTransform != null)
        {
            cameraTransform.position = targetPos;
        }

        onMiddleReached?.Invoke();

        // Pha 2: Mây 3D từ giữa mở ra 2 bên
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transition.SetAllCloudsState(1f - smoothT);

            yield return null;
        }

        transition.SetAllCloudsState(0f);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateCloudsToState(float targetState, float duration)
    {
        float startState = 1f - targetState;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float currentState = Mathf.Lerp(startState, targetState, smoothT);

            SetAllCloudsState(currentState);
            yield return null;
        }

        SetAllCloudsState(targetState);
    }

    /// <summary>
    /// Điều chỉnh trạng thái mây 3D dưới Camera hiện tại:
    /// progress = 0: Mở hoàn toàn ngoài màn hình (Open)
    /// progress = 1: Đóng hoàn toàn che kín màn hình (Closed)
    /// </summary>
    public void SetAllCloudsState(float progress)
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null) return;

        List<Transform> clouds = new List<Transform>();
        foreach (Transform child in cam.transform)
        {
            if (child.name.ToLower().Contains("cloud"))
            {
                clouds.Add(child);
            }
        }

        if (clouds.Count == 0) return;

        for (int i = 0; i < clouds.Count; i++)
        {
            Transform cloud = clouds[i];
            if (cloud == null) continue;

            // Scale to để đảm bảo che trọn cả phần trên và dưới
            if (cloud.localScale.x < 1.4f || cloud.localScale.y < 1.4f)
            {
                cloud.localScale = new Vector3(
                    Mathf.Max(cloud.localScale.x, 1.8f),
                    Mathf.Max(cloud.localScale.y, 1.8f),
                    Mathf.Max(cloud.localScale.z, 1.8f)
                );
            }

            Vector3 closedPos = GetClosedPositionForIndex(i);
            Vector3 openOffset = GetOpenOffsetForIndex(i);
            Vector3 openPos = closedPos + openOffset;

            Vector3 currentPos = Vector3.Lerp(openPos, closedPos, Mathf.Clamp01(progress));
            cloud.localPosition = currentPos;
        }
    }

    private Vector3 GetClosedPositionForIndex(int index)
    {
        switch (index % 3)
        {
            case 0: return new Vector3(0f, -1.3f, 3.2f);  // Mây che dưới
            case 1: return new Vector3(0f, 0.8f, 3.4f);   // Mây che trên
            case 2: return new Vector3(0f, -0.2f, 3.0f);  // Mây che giữa
            default: return new Vector3(0f, 0f, 3.2f);
        }
    }

    private Vector3 GetOpenOffsetForIndex(int index)
    {
        switch (index % 3)
        {
            case 0: return new Vector3(-20f, -2f, 0f); // Mây kéo sang trái
            case 1: return new Vector3(20f, 2f, 0f);   // Mây kéo sang phải
            case 2: return new Vector3(-18f, 4f, 0f);  // Mây kéo sang trái trên
            default: return new Vector3(-20f, 0f, 0f);
        }
    }
}
