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

    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
        FindMainCamera();
    }

    void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            FindMainCamera();
            if (mainCameraTransform == null) return;
        }

        // Bắt UI luôn quay mặt về hướng Camera hướng tới (Billboard)
        transform.LookAt(transform.position + mainCameraTransform.rotation * Vector3.forward,
                         mainCameraTransform.rotation * Vector3.up);

        // Điều chỉnh kích thước cố định nhìn từ Camera
        if (useFixedSize)
        {
            AdjustFixedSize();
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

    private void AdjustFixedSize()
    {
        if (mainCamera == null) return;

        float scaleFactor;
        if (mainCamera.orthographic)
        {
            scaleFactor = mainCamera.orthographicSize * fixedSize;
        }
        else
        {
            // Tính khoảng cách từ UI đến Camera và nhân với hệ số fixedSize
            float distance = Vector3.Distance(transform.position, mainCameraTransform.position);
            scaleFactor = distance * fixedSize;
        }

        // Giới hạn trong khoảng min/max scale
        scaleFactor = Mathf.Clamp(scaleFactor, minScale, maxScale);
        transform.localScale = initialScale * scaleFactor;
    }
}
