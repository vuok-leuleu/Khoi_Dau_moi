using UnityEngine;
using UnityEngine.AI;

public class HPSoldier : MonoBehaviour, IDamageable
{
    [Header("Cấu hình máu (HP)")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject hitVFXPrefab;   // Hiệu ứng khi bị trúng đòn
    [SerializeField] private GameObject deathVFXPrefab; // Hiệu ứng khi tử trận (nếu có)
    [SerializeField] private float destroyDelay = 3.0f;  // Thời gian chờ để diễn xong hoạt cảnh chết trước khi hủy object

    [Header("Tên Trigger hoạt cảnh chết (nếu có Animator)")]
    [SerializeField] private string deathTriggerName = "Die";

    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }

    private Animator animator;
    private NavMeshAgent agent;
    private Collider[] colliders;
    private bool isDead = false;
    public bool IsDead => isDead;

    private void Awake()
    {
        MaxHealth = maxHealth;
        CurrentHealth = MaxHealth;

        // Lấy các component liên quan
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead) return;

        // Defence research reduces incoming damage, so it works for both old and
        // newly spawned soldiers without modifying their prefab health values.
        UnitController unit = GetComponent<UnitController>();
        if (unit == null) unit = GetComponentInParent<UnitController>();
        CurrentHealth -= amount / ResearchUpgradeEffects.GetDefenseMultiplier(
            unit != null ? unit.ResearchType : SoldierResearchType.Sword);
        // Debug.Log($"[HPSoldier] {gameObject.name} nhận {amount} sát thương tại {hitPoint}. HP còn lại: {CurrentHealth}/{MaxHealth}");

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
