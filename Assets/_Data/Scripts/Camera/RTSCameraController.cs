using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Tốc độ di chuyển & Zoom")]
    public float moveSpeed = 30f;
    public float zoomSpeed = 500f; // Tốc độ cuộn chuột

    [Header("Giới hạn mặt đất (Khóa trục X, Z)")]
    public float minX = -150f;
    public float maxX = 50f;
    public float minZ = -100f;
    public float maxZ = 100f;

    [Header("Giới hạn độ cao (Khóa trục Y - Zoom)")]
    public float minY = 5f;  // Zoom in sát đất nhất (không để bằng 0 vì sẽ chui xuống đất)
    public float maxY = 40f; // Zoom out cao nhất để nhìn bao quát

    void Update()
    {
        MoveCamera();
        ZoomCamera();
        ClampPosition();
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