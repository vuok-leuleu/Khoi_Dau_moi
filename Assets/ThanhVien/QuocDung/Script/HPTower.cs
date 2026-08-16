using UnityEngine;
using UnityEngine.UI;

public class HPTower : MonoBehaviour, IDamageable
{
    [Header("Penta Dev - Cấu hình máu (HP)")]
    [SerializeField] private float maxHealth = 200f;
    [SerializeField] private GameObject destroyVFXPrefab; // Hiệu ứng khi công trình bị phá hủy

    [Header("Penta Dev - Cấu hình UI Máu tự vẽ (Runtime UI)")]
    [SerializeField] private float hpBarHeightOffset = 4f;
    [SerializeField] private Vector2 hpBarSize = new Vector2(2f, 0.25f);
    [SerializeField] private Color hpBarColor = Color.red;
    [SerializeField] private bool hideHpBarWhenFull = true;

    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }

    private bool isDestroyed = false;
    public bool IsDestroyed => isDestroyed;

    public event System.Action OnDeathEvent;

    // Các thành phần UI được khởi tạo hoàn toàn bằng code lúc chạy game
    private Canvas hpCanvas;
    private Image hpFillImage;
    private Image hpBgImage;
    private Transform camTransform;
    private Sprite whiteSprite;

    private void Awake()
    {
        MaxHealth = maxHealth;
        CurrentHealth = MaxHealth;

        // Tạo sprite trắng 1x1 tại runtime để gán cho Image (bắt buộc đối với Image.Type.Filled)
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    private void Start()
    {
        // Gán camera chính để làm hệ thống xoay thanh máu (Billboarding)
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }

        // TỰ ĐỘNG KHỞI TẠO CỤM CANVAS MÁU BẰNG CODE
        CreateRuntimeHPBar();
    }

    /// <summary>
    /// Hàm xây dựng Canvas World Space và cấu hình thanh Image Filled nguyên bản bằng code
    /// </summary>
    private void CreateRuntimeHPBar()
    {
        // 1. Tạo GameObject làm Canvas gốc và đặt làm con của công trình
        GameObject canvasObj = new GameObject("Runtime_HPBar_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, hpBarHeightOffset, 0);

        hpCanvas = canvasObj.AddComponent<Canvas>();
        hpCanvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();

        // 2. Tạo GameObject làm nền đen cho thanh máu (Background)
        GameObject bgObj = new GameObject("HP_Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        
        hpBgImage = bgObj.AddComponent<Image>();
        hpBgImage.sprite = whiteSprite;
        hpBgImage.color = new Color(0f, 0f, 0f, 0.6f); // Màu đen mờ làm nền

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = hpBarSize;

        // 3. Tạo GameObject làm thanh máu chính co giãn (Fill Image)
        GameObject fillObj = new GameObject("HP_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);

        hpFillImage = fillObj.AddComponent<Image>();
        hpFillImage.sprite = whiteSprite;
        hpFillImage.color = hpBarColor;
        
        // Cấu hình chế độ Filled rút thanh máu theo chiều ngang từ trái sang phải
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        // Ép thanh Fill căng tràn theo kích thước của thanh nền Background
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        // Thiết lập trạng thái ẩn hiện ban đầu dựa theo cấu hình
        if (hideHpBarWhenFull)
        {
            hpCanvas.gameObject.SetActive(false);
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDestroyed) return;

        // Bỏ qua không nhận sát thương nếu công trình đang trong quá trình xây dựng ban đầu hoặc chưa xây xong
        UpgradeableBuilding building = GetComponent<UpgradeableBuilding>();
        if (building == null) building = GetComponentInParent<UpgradeableBuilding>();
        if (building != null && (building.IsInitialBuildNeeded || building.IsUpgrading))
        {
            return;
        }

        BuildingCtrl ctrl = GetComponent<BuildingCtrl>();
        if (ctrl == null) ctrl = GetComponentInParent<BuildingCtrl>();
        if (ctrl != null && !ctrl.IsBuilt)
        {
            return;
        }

        CurrentHealth -= amount;
        // Debug.Log($"[HPTower] {gameObject.name} nhận {amount} sát thương tại {hitPoint}. HP còn lại: {CurrentHealth}/{MaxHealth}");

        // CẬP NHẬT THANH MÁU ĐÃ VẼ BẰNG CODE
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = CurrentHealth / MaxHealth;
        }

        // Kiểm tra ẩn hiện UX: Chỉ hiện khi mất máu, đầy hoặc sập hẳn thì ẩn đi
        if (hpCanvas != null)
        {
            bool shouldShow = CurrentHealth < MaxHealth && CurrentHealth > 0;
            hpCanvas.gameObject.SetActive(shouldShow);
        }

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            OnDeath();
        }
    }

    public void OnDeath()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        OnDeathEvent?.Invoke();

        Debug.Log($"[HPTower] {gameObject.name} đã lọt vào trạng thái sụp đổ!");

        // Sinh hiệu ứng khói bụi phá hủy nhà nếu có gán prefab
        if (destroyVFXPrefab != null)
        {
            GameObject vfx = Instantiate(destroyVFXPrefab, transform.position, transform.rotation);
            Destroy(vfx, 3f);
        }

        // Tắt Canvas máu đi khi nhà sập
        if (hpCanvas != null) hpCanvas.gameObject.SetActive(false);

        // PHỐI HỢP LOGIC SẬP NHÀ VỚI UPGRADEABLEBUILDING
        UpgradeableBuilding building = GetComponent<UpgradeableBuilding>();
        if (building != null)
        {
            building.TriggerDestructionSequence();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        // Giữ thanh máu tự vẽ luôn nhìn thẳng về Camera (Billboarding) chống méo hình
        if (hpCanvas != null && hpCanvas.gameObject.activeSelf && camTransform != null)
        {
            hpCanvas.transform.LookAt(hpCanvas.transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
        }
    }

    /// <summary>
    /// Được gọi từ UpgradeableBuilding sau khi người chơi sửa nhà xong để tái tạo trạng thái ban đầu
    /// </summary>
    public void ResetHealth()
    {
        isDestroyed = false;
        CurrentHealth = MaxHealth;

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = 1f; // Đầy cây fill
        }

        if (hpCanvas != null)
        {
            // Nếu cấu hình ẩn khi đầy máu thì tắt canvas đi, ngược lại thì bật lên
            hpCanvas.gameObject.SetActive(!hideHpBarWhenFull);
        }
    }

    private void OnDestroy()
    {
        // Dọn dẹp bộ nhớ Runtime chống leak bộ nhớ do Sprite tự tạo ra
        if (whiteSprite != null)
        {
            if (Application.isPlaying)
            {
                Destroy(whiteSprite);
            }
            else
            {
                DestroyImmediate(whiteSprite);
            }
        }
    }

    // Thêm hàm này vào cuối file HPTower.cs[cite: 28]
    /// <summary>
    /// Đưa thanh máu về 0 và ẩn Canvas HP khi công trình ở trạng thái Tàn Tích ban đầu
    /// </summary>
    public void SetRuinedHealth()
    {
        CurrentHealth = 0f;
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = 0f;
        }
        if (hpCanvas != null)
        {
            hpCanvas.gameObject.SetActive(false);
        }
    }
}