using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Transform mainCameraTransform;
    private Camera mainCamera;

    [Header("Fixed Size Settings")]
    [Tooltip("Bật/tắt giữ kích thước cố định trên màn hình camera không phụ thuộc vào khoảng cách")]
    [SerializeField] private bool useFixedSize = true;

    [Tooltip("Hệ số kích thước cố định (tỉ lệ hiển thị trên màn hình)")]
    [SerializeField] private float fixedSize = 0.1f;

    [Tooltip("Kích thước tỉ lệ nhỏ nhất cho phép")]
    [SerializeField] private float minScale = 0.001f;

    [Tooltip("Kích thước tỉ lệ lớn nhất cho phép")]
    [SerializeField] private float maxScale = 100f;

    [Header("Distance Visibility Settings")]
    [Tooltip("Bật/tắt tính năng ẩn khi Camera đến quá gần")]
    [SerializeField] private bool enableDistanceFade = true;

    [Tooltip("Khoảng cách tối thiểu từ Camera đến UI. Nếu lại gần hơn khoảng cách này, UI sẽ bị ẩn đi")]
    [SerializeField] private float minVisibleDistance = 18f;

    [Tooltip("Khoảng cách chuyển tiếp làm mờ dần mượt mà")]
    [SerializeField] private float fadeTransitionRange = 6f;

    private Vector3 initialScale;
    private CanvasGroup canvasGroup;

    void Start()
    {
        initialScale = transform.localScale;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        FindMainCamera();
    }

    void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            FindMainCamera();
            if (mainCameraTransform == null) return;
        }

        // 1. Bắt UI luôn quay mặt về hướng Camera hướng tới (Billboard)
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);

        float distance = Vector3.Distance(transform.position, mainCameraTransform.position);

        // 2. Ẩn khi đến quá gần Camera (chỉ hiện khi đứng ở khoảng cách nhất định)
        if (enableDistanceFade && canvasGroup != null)
        {
            if (distance < minVisibleDistance)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else if (distance < minVisibleDistance + fadeTransitionRange)
            {
                float t = (distance - minVisibleDistance) / Mathf.Max(0.01f, fadeTransitionRange);
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        // 3. Điều chỉnh kích thước cố định nhìn từ Camera
        if (useFixedSize)
        {
            AdjustFixedSize(distance);
        }
    }

    private void FindMainCamera()
    {
        if (Camera.main != null)
        {
            mainCamera = Camera.main;
            mainCameraTransform = mainCamera.transform;
        }
    }

    private void AdjustFixedSize(float distance)
    {
        if (mainCamera == null) return;

        float scaleFactor;
        if (mainCamera.orthographic)
        {
            scaleFactor = mainCamera.orthographicSize * fixedSize;
        }
        else
        {
            scaleFactor = distance * fixedSize;
        }

        scaleFactor = Mathf.Clamp(scaleFactor, minScale, maxScale);
        transform.localScale = initialScale * scaleFactor;
    }
}
