using UnityEngine;

public class Rice : MonoBehaviour
{
    [Header("Rice Settings")]
    public int maxHealth = 2;

    [Header("Drop Settings")]
    public ObjectPool ricePool;
    public int dropAmount = 2;

    [Header("Respawn Settings")]
    [Tooltip("Thời gian (giây) lúa hồi sinh sau khi gặt. 0 = không hồi sinh.")]
    public float respawnDelay = 45f;

    private int  currentHealth;
    private bool isOccupied = false;
    private Coroutine respawnRoutine;

    void OnEnable()
    {
        currentHealth = maxHealth;
        isOccupied    = false;
        WorkerFindRice.Registry.Add(this);
    }

    public int GetCurrentHealth() => currentHealth;
    
    public void SetCurrentHealth(int health)
    {
        currentHealth = health;
        if (currentHealth <= 0)
        {
            SetVisible(false);
            WorkerFindRice.Registry.Remove(this);
        }
    }

    void OnDisable()
    {
        WorkerFindRice.Registry.Remove(this);
    }

    public bool TryClaim()
    {
        if (isOccupied || currentHealth <= 0) return false;
        isOccupied = true;
        return true;
    }

    public void Release() => isOccupied = false;

    public RicePickup[] TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"[Rice] '{name}' bị gặt. HP còn lại: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0) return HarvestRice();
        return null;
    }

    RicePickup[] HarvestRice()
    {
        RicePickup[] drops = DropRice();
        isOccupied = false;

        if (respawnDelay <= 0f)
        {
            gameObject.SetActive(false);
        }
        else
        {
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        return drops;
    }

    System.Collections.IEnumerator RespawnRoutine()
    {
        // Ẩn lúa + xóa khỏi Registry (không SetActive(false) để coroutine vẫn chạy)
        SetVisible(false);
        WorkerFindRice.Registry.Remove(this);

        yield return new WaitForSeconds(respawnDelay);

        // Hồi sinh
        currentHealth = maxHealth;
        isOccupied    = false;
        SetVisible(true);
        WorkerFindRice.Registry.Add(this);
        respawnRoutine = null;
    }

    void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>())  c.enabled = visible;
    }

    RicePickup[] DropRice()
    {
        if (ricePool == null)
        {
            Debug.LogWarning($"[Rice] '{name}' không có ricePool — không rơi lúa.");
            return null;
        }

        RicePickup[] drops = new RicePickup[dropAmount];
        for (int i = 0; i < dropAmount; i++)
        {
            GameObject obj = ricePool.GetObject();
            Vector3 dropPos = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.3f, Random.Range(-0.5f, 0.5f));
            obj.transform.position = dropPos;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic     = false;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(Vector3.up * 2f + Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
            }
            drops[i] = obj.GetComponent<RicePickup>();
        }
        Debug.Log($"[Rice] '{name}' rơi {dropAmount} lúa.");
        return drops;
    }
}