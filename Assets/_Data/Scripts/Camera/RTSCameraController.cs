using UnityEngine;
using UnityEngine.EventSystems;

public class RTSCameraController : MonoBehaviour
{
    /// <summary>True khi người chơi đang giữ và kéo chuột trái để pan camera.</summary>
    public static bool IsMouseDragging { get; private set; }

    /// <summary>
    /// Giữ trạng thái của lần nhả chuột vừa rồi để script click khác bỏ qua
    /// một thao tác kéo thay vì coi đó là click chọn thành/công trình.
    /// </summary>
    public static bool WasMouseDragThisPress { get; private set; }

    [Header("Tốc độ di chuyển & Zoom")]
    public float moveSpeed = 30f;
    public float zoomSpeed = 500f; // Tốc độ cuộn chuột

    [Header("Kéo bản đồ bằng chuột trái")]
    [SerializeField] private bool enableLeftMouseDrag = true;
    [SerializeField, Min(0.001f)] private float dragUnitsPerPixel = 0.06f;
    [SerializeField, Min(1f)] private float dragStartThresholdPixels = 8f;

    [Header("Giới hạn mặt đất (Khóa trục X, Z)")]
    public float minX = -150f;
    public float maxX = 50f;
    public float minZ = -100f;
    public float maxZ = 100f;

    [Header("Giới hạn độ cao (Khóa trục Y - Zoom)")]
    public float minY = 5f;  // Zoom in sát đất nhất (không để bằng 0 vì sẽ chui xuống đất)
    public float maxY = 40f; // Zoom out cao nhất để nhìn bao quát

    private bool isMouseDragCandidate;
    private Vector3 mouseDownPosition;
    private Vector3 previousMousePosition;

    void Update()
    {
        HandleLeftMouseDrag();
        MoveCamera();
        ZoomCamera();
        ClampPosition();
    }

    private void OnDisable()
    {
        isMouseDragCandidate = false;
        IsMouseDragging = false;
        WasMouseDragThisPress = false;
    }

    private void HandleLeftMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            WasMouseDragThisPress = false;
            IsMouseDragging = false;

            if (!CanBeginMouseDrag()) return;

            isMouseDragCandidate = true;
            mouseDownPosition = Input.mousePosition;
            previousMousePosition = mouseDownPosition;
        }

        if (isMouseDragCandidate && Input.GetMouseButton(0))
        {
            Vector3 currentMousePosition = Input.mousePosition;
            if (!IsMouseDragging)
            {
                float movedDistanceSqr = (currentMousePosition - mouseDownPosition).sqrMagnitude;
                float thresholdSqr = dragStartThresholdPixels * dragStartThresholdPixels;
                IsMouseDragging = movedDistanceSqr >= thresholdSqr;
            }

            if (IsMouseDragging)
            {
                Vector3 mouseDelta = currentMousePosition - previousMousePosition;
                PanByMouseDelta(mouseDelta);
            }

            previousMousePosition = currentMousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            WasMouseDragThisPress = IsMouseDragging;
            isMouseDragCandidate = false;
            IsMouseDragging = false;
        }
    }

    private bool CanBeginMouseDrag()
    {
        if (!enableLeftMouseDrag) return false;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

        // Không cướp thao tác click để đặt công trình hoặc chọn điểm điều quân.
        if (MoveModeController.IsMoveModeActive) return false;
        if (BuildingSystem.Ins != null && (BuildingSystem.Ins.IsPlacing || BuildingSystem.Ins.IsMovingMode)) return false;

        return true;
    }

    private void PanByMouseDelta(Vector3 mouseDelta)
    {
        if (mouseDelta.sqrMagnitude <= 0f) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Kiểu "nắm bản đồ": kéo chuột sang phải thì bản đồ đi theo chuột,
        // nên camera đi về bên trái.
        float speedMultiplier = (SettingManager.Ins != null) ? SettingManager.Ins.mouseSpeed / 5f : 1f;
        Vector3 movement = (-right * mouseDelta.x - forward * mouseDelta.y) * dragUnitsPerPixel * speedMultiplier;
        transform.position += movement;
    }

    private void MoveCamera()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h == 0 && v == 0) return;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // Nhân thêm mouseSpeed từ SettingManager cho di chuyển WASD
        float speedMultiplier = (SettingManager.Ins != null) ? SettingManager.Ins.mouseSpeed / 5f : 1f;

        Vector3 moveDirection = (forward * v + right * h).normalized;
        transform.position += moveDirection * moveSpeed * speedMultiplier * Time.deltaTime;
    }

    private void ZoomCamera()
    {
        // Lấy tín hiệu cuộn con lăn chuột (Mouse ScrollWheel)
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            // Nhân thêm mouseSpeed từ SettingManager (chỉ ảnh hưởng scroll/zoom, không đụng WASD)
            float scrollMultiplier = (SettingManager.Ins != null) ? SettingManager.Ins.mouseSpeed / 5f : 1f;

            // Di chuyển camera tới/lui theo đúng hướng nó đang nhìn (transform.forward)
            Vector3 zoomDirection = transform.forward * scroll * zoomSpeed * scrollMultiplier * Time.deltaTime;
            transform.position += zoomDirection;
        }
    }

    private void ClampPosition()
    {
        Vector3 pos = transform.position;

        // Ép vị trí X và Z không được vượt quá giới hạn biên bản đồ
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        // Ép vị trí Y không được vượt quá giới hạn độ cao khi Zoom
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }
}
