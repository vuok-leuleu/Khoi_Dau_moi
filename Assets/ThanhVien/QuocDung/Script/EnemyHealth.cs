using UnityEngine;
using UnityEngine.UI;

/*
 * EnemyHealth.cs
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Người tối ưu: VŨ
 * Tính năng: Quản lý máu Enemy, tự vẽ thanh máu bằng code và phát sự kiện khi chết cho RaidManager.
 */

public class EnemyHealth : MonoBehaviour, IDamageable
{
    // 🌟 SỰ KIỆN TĨNH: Báo cho RoKFirstRaidManager biết khi con quái này chết
    public static event System.Action<EnemyHealth> OnEnemyDied;

    [Header("Penta Dev - Cấu hình máu (HP)")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject bloodVFXPrefab;
    [SerializeField] private Transform visualChild;
    [SerializeField] private Transform bloodTransform;

    [Header("Penta Dev - Cấu hình UI Máu tự vẽ (Runtime UI)")]
    [Tooltip("Độ cao của thanh máu trên đầu Enemy")]
    [SerializeField] private float hpBarHeightOffset = 2.2f; 
    [Tooltip("Kích thước thanh máu (Rộng, Cao)")]
    [SerializeField] private Vector2 hpBarSize = new Vector2(1.2f, 0.15f); 
    [SerializeField] private Color hpBarColor = Color.red;
    [SerializeField] private bool hideHpBarWhenFull = true;

    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }

    // 🌟 KHAI BÁO BIẾN TRẠNG THÁI CHẾT (Tránh lỗi không tìm thấy biến)
    private bool isDead = false;
    public bool IsDead => isDead || CurrentHealth <= 0f;

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

        if (visualChild == null && transform.childCount > 0)
        {
            visualChild = transform.GetChild(0);
        }

        if (bloodTransform == null)
        {
            bloodTransform = FindChildRecursive(transform, "blood");
        }

        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            camTransform = Camera.main.transform;
        }

        CreateRuntimeHPBar();
    }

    private void CreateRuntimeHPBar()
    {
        GameObject canvasObj = new GameObject("Runtime_EnemyHPBar_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, hpBarHeightOffset, 0);

        hpCanvas = canvasObj.AddComponent<Canvas>();
        hpCanvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();

        GameObject bgObj = new GameObject("HP_Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        
        hpBgImage = bgObj.AddComponent<Image>();
        hpBgImage.sprite = whiteSprite;
        hpBgImage.color = new Color(0f, 0f, 0f, 0.6f); 

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = hpBarSize;

        GameObject fillObj = new GameObject("HP_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);

        hpFillImage = fillObj.AddComponent<Image>();
        hpFillImage.sprite = whiteSprite;
        hpFillImage.color = hpBarColor;
        
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        if (hideHpBarWhenFull)
        {
            hpCanvas.gameObject.SetActive(false);
        }
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower().Contains(targetName.ToLower()))
            {
                return child;
            }
            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead)
            return;

        CurrentHealth -= amount;
        // Debug.Log($"{name} took {amount} damage at {hitPoint}. Current HP: {CurrentHealth}");

        // Enable combat on EnemyAI when damaged
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.EnableCombat();
        }

        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = CurrentHealth / MaxHealth;
        }

        if (hpCanvas != null)
        {
            bool shouldShow = CurrentHealth < MaxHealth && CurrentHealth > 0;
            hpCanvas.gameObject.SetActive(shouldShow);
        }

        if (bloodVFXPrefab != null)
        {
            Quaternion vfxRotation = visualChild != null ? visualChild.rotation : transform.rotation;
            Transform parentTransform = visualChild != null ? visualChild : transform;
            Vector3 spawnPosition = bloodTransform != null ? bloodTransform.position : hitPoint;

            GameObject vfx = Instantiate(bloodVFXPrefab, spawnPosition, vfxRotation, parentTransform);
            vfx.transform.SetParent(null, true);
            Destroy(vfx, 1f);
        }

        if (CurrentHealth <= 0f)
        {
            OnDeath();
        }
    }

    public void OnDeath()
    {
        if (isDead) return;
        isDead = true;

        // ✅ Báo cho Tutorial Manager biết quái đã bị hạ
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnEnemyKilled();
        }

        OnEnemyDied?.Invoke(this);

        // Some enemies, such as Dragon, need to remain in the scene long
        // enough for a death animation to be visible. The health system stays
        // generic by asking for an optional handler instead of knowing enemy types.
        IDeathAnimationHandler deathAnimationHandler = GetComponent<IDeathAnimationHandler>();
        float destroyDelay = deathAnimationHandler != null
            ? deathAnimationHandler.PlayDeathAnimation()
            : 0f;

        if (destroyDelay > 0f)
        {
            StartCoroutine(DestroyAfterDeathAnimation(destroyDelay));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator DestroyAfterDeathAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    private void LateUpdate()
    {
        if (hpCanvas != null && hpCanvas.gameObject.activeSelf && camTransform != null)
        {
            hpCanvas.transform.LookAt(hpCanvas.transform.position + camTransform.rotation * Vector3.forward, camTransform.rotation * Vector3.up);
        }
    }

    private void OnDestroy()
    {
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
}
