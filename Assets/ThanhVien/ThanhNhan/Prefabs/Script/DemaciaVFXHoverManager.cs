using UnityEngine;
using UnityEngine.EventSystems;

public class DemaciaVFXHoverManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask buildingLayer;
    [SerializeField] private GameObject vfxPrefab;      // Kéo Prefab VFX có sẵn vào đây
    [SerializeField] private Vector3 heightOffset = Vector3.up * 0.5f; // Bù độ cao nếu gốc pivot của công trình nằm dưới đất

    private GameObject vfxInstance;
    private ParticleSystem[] vfxParticles;
    private Collider currentTarget;

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

    private void ShowVFX(Vector3 position)
    {
        if (vfxInstance == null) return;

        vfxInstance.transform.position = position;

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

    private void ClearHover()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            if (vfxInstance != null && vfxInstance.activeSelf)
            {
                // Dừng phát hạt mới để hạt cũ tan tự nhiên (hoặc dùng SetActive(false) nếu muốn tắt ngay)
                vfxInstance.SetActive(false);
            }
        }
    }
}