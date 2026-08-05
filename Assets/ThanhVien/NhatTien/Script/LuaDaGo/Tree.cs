using UnityEngine;

public class Tree : MonoBehaviour
{
    [Header("Tree Settings")]
    public int maxHealth = 3;

    [Header("Drop Settings")]
    public ObjectPool woodPool;
    public int dropAmount = 3;

    [Header("VFX")]
    public ParticleSystem chipVFX;

    [Header("Respawn Settings")]
    [Tooltip("Thời gian (giây) cây hồi sinh sau khi bị đốn. 0 = không hồi sinh.")]
    public float respawnDelay = 60f;

    private int currentHealth;
    private bool isOccupied = false;
    private bool isFalling  = false; // Chặn TakeDamage kép trong lúc animation đổ cây
    private TreeVisual treeVisual;
    private Coroutine respawnRoutine;

    void Awake()
    {
        currentHealth = maxHealth;
        treeVisual = GetComponentInChildren<TreeVisual>();
        if (treeVisual == null) Debug.LogWarning($"[Tree] '{name}' không có TreeVisual.");
    }

    public int GetCurrentHealth() => currentHealth;
    
    public void SetCurrentHealth(int health)
    {
        currentHealth = health;
        if (currentHealth <= 0)
        {
            // Cây đã bị khai thác hết trước khi save
            SetVisible(false);
            WorkerFindTree.Registry.Remove(this);
            // Có thể khởi động lại coroutine hồi sinh nếu muốn, 
            // nhưng tạm thời ta có thể set nó ẩn đi nếu hp <= 0.
        }
    }

    void OnEnable()
    {
        currentHealth = maxHealth;
        isOccupied    = false;
        isFalling     = false;
        WorkerFindTree.Registry.Add(this);
    }

    void OnDisable()
    {
        WorkerFindTree.Registry.Remove(this);
    }

    public bool TryClaim()
    {
        if (isOccupied || isFalling || currentHealth <= 0) return false;
        isOccupied = true;
        return true;
    }

    public void Release() => isOccupied = false;

    public void PlayChopHitVFX()
    {
        if (chipVFX == null) return;
        chipVFX.transform.position = transform.position + Vector3.up * 1f;
        chipVFX.Emit(8);
    }

    public WoodPickup[] TakeDamage(int damage)
    {
        if (isFalling) return null; // Đang đổ rồi, bỏ qua

        currentHealth -= damage;
        Debug.Log($"[Tree] '{name}' nhận {damage} damage. HP còn lại: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0) return DestroyTree();
        treeVisual?.PlayShake();
        return null;
    }

    WoodPickup[] DestroyTree()
    {
        isFalling  = true;  // Khóa, không nhận damage nữa
        isOccupied = false; // Nhả claim ngay để worker khác không chờ vô ích

        WoodPickup[] woods = DropWood();

        if (treeVisual != null)
        {
            treeVisual.PlayFall(onFallComplete: () =>
            {
                if (respawnDelay <= 0f)
                {
                    gameObject.SetActive(false);
                }
                else
                {
                    if (respawnRoutine != null) StopCoroutine(respawnRoutine);
                    respawnRoutine = StartCoroutine(RespawnRoutine());
                }
            });
        }
        else
        {
            if (respawnDelay <= 0f) gameObject.SetActive(false);
            else
            {
                if (respawnRoutine != null) StopCoroutine(respawnRoutine);
                respawnRoutine = StartCoroutine(RespawnRoutine());
            }
        }

        return woods;
    }

    System.Collections.IEnumerator RespawnRoutine()
    {
        // Ẩn cây + xóa khỏi Registry (không SetActive(false) để coroutine vẫn chạy)
        SetVisible(false);
        WorkerFindTree.Registry.Remove(this);

        yield return new WaitForSeconds(respawnDelay);

        // Hồi sinh: reset visual, hả thả, đăng ký lại
        if (treeVisual != null) treeVisual.ResetState();
        currentHealth = maxHealth;
        isFalling     = false;
        isOccupied    = false;
        SetVisible(true);
        WorkerFindTree.Registry.Add(this);
        respawnRoutine = null;
    }

    void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>())  c.enabled = visible;
    }

    WoodPickup[] DropWood()
    {
        if (woodPool == null)
        {
            Debug.LogWarning($"[Tree] '{name}' không có woodPool — không rơi gỗ.");
            return null;
        }

        WoodPickup[] woods = new WoodPickup[dropAmount];
        for (int i = 0; i < dropAmount; i++)
        {
            GameObject obj = woodPool.GetObject();
            Vector3 dropPos = transform.position + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f));
            obj.transform.position = dropPos;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic     = false;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(Vector3.up * 3f + Random.insideUnitSphere, ForceMode.Impulse);
            }
            woods[i] = obj.GetComponent<WoodPickup>();
        }
        Debug.Log($"[Tree] '{name}' rơi {dropAmount} gỗ.");
        return woods;
    }
}