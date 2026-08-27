using UnityEngine;
using UnityEngine.EventSystems;

public class DemaciaVFXHoverManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask buildingLayer;
    [SerializeField] private GameObject vfxPrefab;      // Kéo Prefab VFX có sẵn vào đây
    [SerializeField] private Vector3 heightOffset = Vector3.up * 0.5f; // Bù độ cao nếu gốc pivot của công trình nằm dưới đất
    [SerializeField] private Vector3 vfxRotation = Vector3.zero;

    private GameObject vfxInstance;
    private ParticleSystem[] vfxParticles;
    private Collider currentTarget;
    private Transform selectedSettlementTarget;

    void Awake()
    {
        if (mainCam == null) mainCam = Camera.main;

        // Tạo 1 bản thể VFX duy nhất trên Scene để tái sử dụng
        if (vfxPrefab != null)
        {
            vfxInstance = Instantiate(vfxPrefab, Vector3.zero, Quaternion.identity);
            vfxParticles = vfxInstance.GetComponentsInChildren<ParticleSystem>();
            vfxInstance.SetActive(false);
        }
    }

    void Update()
    {
        // Focus của thành được chọn có ưu tiên hơn hover chuột. VFX sẽ bám
        // đúng vị trí thành trong lúc bảng thành đang mở.
        if (selectedSettlementTarget != null)
        {
            UpdateSettlementFocusVFX();
            return;
        }

        // Chặn hover khi trỏ chuột lên các bảng UI (Quest, Topbar...)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, buildingLayer))
        {
            Collider target = hit.collider;

            if (currentTarget != target)
            {
                currentTarget = target;
                ShowVFX(GetVFXPosition(currentTarget));
            }
        }
        else
        {
            ClearHover();
        }
    }

    /// <summary>
    /// Bật VFX tại thành đang chọn. Có thể gọi cả khi GameObject đang tắt.
    /// </summary>
    public void ShowSettlementFocus(Transform target)
    {
        if (target == null) return;

        // BuildTrainingUIManager có thể nhận hai event click từ collider thành.
        // Cùng target đang hiển thị thì giữ VFX hiện tại, không Clear/Play lại.
        if (selectedSettlementTarget == target && vfxInstance != null && vfxInstance.activeSelf)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        selectedSettlementTarget = target;
        currentTarget = null;
        ShowVFX(GetVFXPosition(target));
    }

    /// <summary>
    /// Bỏ VFX focus thành; hover chuột sẽ trở lại bình thường ở frame tiếp theo.
    /// </summary>
    public void ClearSettlementFocus()
    {
        selectedSettlementTarget = null;
        currentTarget = null;
        HideVFX();
    }

    private void ShowVFX(Vector3 position)
    {
        if (vfxInstance == null) return;

        vfxInstance.transform.SetPositionAndRotation(position, Quaternion.Euler(vfxRotation));

        if (!vfxInstance.activeSelf)
        {
            vfxInstance.SetActive(true);
        }
        else
        {
            // Reset lại phát hạt nếu chuyển nhanh giữa 2 nhà cạnh nhau
            foreach (var ps in vfxParticles)
            {
                ps.Clear();
                ps.Play();
            }
        }
    }

    private Vector3 GetVFXPosition(Collider target)
    {
        Bounds bounds = target.bounds;
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + heightOffset;
    }

    private Vector3 GetVFXPosition(Transform target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null ? GetVFXPosition(targetCollider) : target.position + heightOffset;
    }

    private void UpdateSettlementFocusVFX()
    {
        if (vfxInstance == null) return;

        Vector3 focusPosition = GetVFXPosition(selectedSettlementTarget);
        vfxInstance.transform.SetPositionAndRotation(focusPosition, Quaternion.Euler(vfxRotation));
        if (!vfxInstance.activeSelf)
        {
            vfxInstance.SetActive(true);
        }
    }

    private void ClearHover()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            HideVFX();
        }
    }

    private void HideVFX()
    {
        if (vfxInstance != null && vfxInstance.activeSelf)
        {
            // Dừng phát hạt mới để hạt cũ tan tự nhiên (hoặc dùng SetActive(false) nếu muốn tắt ngay)
            vfxInstance.SetActive(false);
        }
    }
}
