using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Stone : MonoBehaviour
{
    public static List<Stone> Registry = new List<Stone>();

    [Header("Stone Settings")]
    public int maxHealth = 4;

    [Header("Drop Settings")]
    public ObjectPool stonePool;
    public int dropAmount = 2;

    [Header("Respawn Settings")]
    [Tooltip("Thời gian (giây) đá hồi sinh sau khi vỡ. 0 = không hồi sinh.")]
    public float respawnDelay = 90f;

    protected int  currentHealth;
    protected bool isOccupied = false;
    protected Vector3 originalScale;
    private Coroutine respawnRoutine;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    public int GetCurrentHealth() => currentHealth;
    
    public void SetCurrentHealth(int health)
    {
        currentHealth = health;
        if (currentHealth <= 0)
        {
            SetVisible(false);
            Registry.Remove(this);
        }
    }

    void OnEnable()
    {
        currentHealth = maxHealth;
        isOccupied    = false;
        transform.localScale = originalScale;
        
        // Đã xóa Contains, add thẳng O(1)
        Registry.Add(this);
    }

    void OnDisable()
    {
        // FIX: Chặn lỗi văng game do Coroutine thao tác trên object đã bị disable
        StopAllCoroutines(); 
        Registry.Remove(this);
    }

    public bool TryClaim()
    {
        if (isOccupied || currentHealth <= 0) return false;
        isOccupied = true;
        return true;
    }

    public void Release() => isOccupied = false;

    /// <summary>
    /// Điểm mà worker sẽ đi tới để khai thác đá này. Mặc định = tâm object (hành vi cũ, không đổi).
    /// Class con (vd StoneInfinite) có thể override để trả về điểm sát bề mặt collider thay vì tâm mesh.
    /// </summary>
    public virtual Vector3 GetMinePoint(Vector3 fromPosition) => transform.position;

    public virtual StonePickup[] TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0) return DestroyStone();
        StartCoroutine(ChippingEffect());
        return null;
    }

    protected IEnumerator ChippingEffect()
    {
        float healthPercent = (float)currentHealth / maxHealth;
        Vector3 targetScale = originalScale * Mathf.Lerp(0.6f, 1f, healthPercent);
        transform.localScale = targetScale * 0.8f; 
        yield return new WaitForSeconds(0.1f);
        transform.localScale = targetScale;
    }

    protected virtual StonePickup[] DestroyStone()
    {
        StonePickup[] drops = DropStone();
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
        // Ẩn đá + xóa khỏi Registry (không SetActive(false) để coroutine vẫn chạy)
        SetVisible(false);
        Registry.Remove(this);

        yield return new WaitForSeconds(respawnDelay);

        // Hồi sinh: reset scale, HP, đăng ký lại
        transform.localScale = originalScale;
        currentHealth = maxHealth;
        isOccupied    = false;
        SetVisible(true);
        Registry.Add(this);
        respawnRoutine = null;
    }

    protected void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>())  c.enabled = visible;
    }

    protected StonePickup[] DropStone()
    {
        if (stonePool == null) return null;

        StonePickup[] drops = new StonePickup[dropAmount];
        for (int i = 0; i < dropAmount; i++)
        {
            GameObject obj = stonePool.GetObject();
            Vector3 dropPos = transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 0.5f, Random.Range(-0.8f, 0.8f));
            obj.transform.position = dropPos;

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic     = false;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(Vector3.up * 2.5f + Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
            }
            drops[i] = obj.GetComponent<StonePickup>();
        }
        return drops;
    }
}