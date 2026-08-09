using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class HPSoldier : MonoBehaviour, IDamageable
{
    // 🌟 GLOBAL TOGGLE: Bật/Tắt thanh máu cho tất cả lính
    public static bool GlobalShowHPBar = true;

    [Header("Cấu hình máu (HP)")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject hitVFXPrefab;   // Hiệu ứng khi bị trúng đòn
    [SerializeField] private GameObject deathVFXPrefab; // Hiệu ứng khi tử trận (nếu có)
    [SerializeField] private float destroyDelay = 3.0f;  // Thời gian chờ để diễn xong hoạt cảnh chết trước khi hủy object

    [Header("Cấu hình UI Thanh Máu (Runtime UI)")]
    [Tooltip("Bật/Tắt hiển thị thanh máu cho lính này")]
    [SerializeField] private bool showHpBar = true;
    [Tooltip("Độ cao của thanh máu trên đầu lính")]
    [SerializeField] private float hpBarHeightOffset = 2.0f;
    [Tooltip("Kích thước thanh máu (Rộng, Cao)")]
    [SerializeField] private Vector2 hpBarSize = new Vector2(1.2f, 0.15f);
    [Tooltip("Màu sắc của thanh máu")]
    [SerializeField] private Color hpBarColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [Tooltip("Màu nền của thanh máu")]
    [SerializeField] private Color hpBgColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("Tự động ẩn thanh máu khi HP đầy 100%")]
    [SerializeField] private bool hideHpBarWhenFull = false;

    [Header("Tên Trigger hoạt cảnh chết (nếu có Animator)")]
    [SerializeField] private string deathTriggerName = "Die";

    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }

    private Animator animator;
    private NavMeshAgent agent;
    private Collider[] colliders;
    private bool isDead = false;
    public bool IsDead => isDead;

    // Các thành phần UI thanh máu khởi tạo động
    private Canvas hpCanvas;
    private Image hpFillImage;
    private Image hpBgImage;
    private Transform camTransform;
    private Sprite whiteSprite;

    private void Awake()
    {
        MaxHealth = maxHealth;
        CurrentHealth = MaxHealth;

        // Lấy các component liên quan
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }

        CreateRuntimeHPBar();
    }

    private void LateUpdate()
    {
        if (isDead) return;

        // Phím tắt H: Bật / Tắt thanh máu cho toàn bộ lính
        if (Input.GetKeyDown(KeyCode.H))
        {
            ToggleGlobalHPBar();
        }

        UpdateHPBarUI();
    }

    private void CreateRuntimeHPBar()
    {
        if (hpCanvas != null) return;

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        GameObject canvasObj = new GameObject("Runtime_SoldierHPBar_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, hpBarHeightOffset, 0);

        hpCanvas = canvasObj.AddComponent<Canvas>();
        hpCanvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();

        GameObject bgObj = new GameObject("HP_Background");
        bgObj.transform.SetParent(canvasObj.transform, false);

        hpBgImage = bgObj.AddComponent<Image>();
        hpBgImage.sprite = whiteSprite;
        hpBgImage.color = hpBgColor;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = hpBarSize;

        GameObject fillObj = new GameObject("HP_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);

        hpFillImage = fillObj.AddComponent<Image>();
        hpFillImage.sprite = whiteSprite;
        hpFillImage.color = hpBarColor;
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillOrigin(hpFillImage);

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        UpdateHPBarUI();
    }

    private void hpFillOrigin(Image img)
    {
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    public void UpdateHPBarUI()
    {
        if (hpCanvas == null) return;

        bool shouldShow = showHpBar && GlobalShowHPBar && !isDead;

        if (hideHpBarWhenFull && CurrentHealth >= MaxHealth)
        {
            shouldShow = false;
        }

        if (hpCanvas.gameObject.activeSelf != shouldShow)
        {
            hpCanvas.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        // Xoay thanh máu hướng về Camera
        if (camTransform == null && Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }

        if (camTransform != null)
        {
            hpCanvas.transform.rotation = camTransform.rotation;
        }

        // Cập nhật phần trăm máu
        if (hpFillImage != null && MaxHealth > 0f)
        {
            hpFillImage.fillAmount = Mathf.Clamp01(CurrentHealth / MaxHealth);
        }
    }

    /// <summary>
    /// Bật / Tắt thanh máu cho cá thể lính này
    /// </summary>
    public void SetHPBarVisible(bool visible)
    {
        showHpBar = visible;
        UpdateHPBarUI();
    }

    /// <summary>
    /// Đảo trạng thái Ẩn/Hiện thanh máu cho cá thể lính này
    /// </summary>
    public void ToggleHPBar()
    {
        SetHPBarVisible(!showHpBar);
    }

    /// <summary>
    /// Bật / Tắt thanh máu cho TOÀN BỘ lính trong game
    /// </summary>
    public static void SetGlobalHPBarVisibility(bool visible)
    {
        GlobalShowHPBar = visible;
        HPSoldier[] allSoldiers = FindObjectsByType<HPSoldier>(FindObjectsSortMode.None);
        foreach (var s in allSoldiers)
        {
            if (s != null) s.UpdateHPBarUI();
        }
        Debug.Log($"[HPSoldier] Đã {(visible ? "HIỆN" : "ẨN")} thanh máu cho toàn bộ lính.");
    }

    /// <summary>
    /// Đảo trạng thái Ẩn/Hiện thanh máu cho TOÀN BỘ lính trong game
    /// </summary>
    public static void ToggleGlobalHPBar()
    {
        SetGlobalHPBarVisibility(!GlobalShowHPBar);
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead) return;

        CurrentHealth -= amount;

        UpdateHPBarUI();

        // Tạo hiệu ứng trúng đòn tại điểm va chạm
        if (hitVFXPrefab != null)
        {
            GameObject hitVfx = Instantiate(hitVFXPrefab, hitPoint, Quaternion.identity);
            Destroy(hitVfx, 1f);
        }

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            OnDeath();
        }
    }

    public void OnDeath()
    {
        if (isDead) return;
        isDead = true;

        if (hpCanvas != null)
        {
            hpCanvas.gameObject.SetActive(false);
        }

        Debug.Log($"[HPSoldier] {gameObject.name} đã tử trận!");

        // 1. Tắt di chuyển của NavMeshAgent để lính dừng lại ngay lập tức
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 2. Tắt các collider để kẻ địch không va chạm hoặc tiếp tục nhắm mục tiêu vào xác lính
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        // 3. Kích hoạt hoạt cảnh chết
        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
        {
            if (HasAnimatorParameter(animator, deathTriggerName, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(deathTriggerName);
            }
        }

        // 4. Tạo hiệu ứng chết (nếu có)
        if (deathVFXPrefab != null)
        {
            GameObject deathVfx = Instantiate(deathVFXPrefab, transform.position, transform.rotation);
            Destroy(deathVfx, destroyDelay);
        }

        // 5. Hủy đối tượng sau một khoảng thời gian trễ để chạy xong hoạt cảnh
        Destroy(gameObject, destroyDelay);
    }

    private bool HasAnimatorParameter(Animator anim, string paramName, AnimatorControllerParameterType type)
    {
        if (anim == null) return false;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName && param.type == type)
                return true;
        }
        return false;
    }
}
