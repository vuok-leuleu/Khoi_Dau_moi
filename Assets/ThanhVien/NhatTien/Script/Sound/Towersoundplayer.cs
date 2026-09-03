using UnityEngine;

[RequireComponent(typeof(AttackTowerAI))]
[RequireComponent(typeof(AudioSource))]
public class TowerSoundPlayer : MonoBehaviour
{
    [Header("🏹 Âm thanh Cung (Archer) – 3 slot = 3 cấp độ")]
    public AudioClip[] archerFireClips = new AudioClip[3];

    [Header("💣 Âm thanh Pháo (Cannon) – 3 slot = 3 cấp độ")]
    public AudioClip[] cannonFireClips = new AudioClip[3];

    [Header("⚙️ Cấu hình Âm thanh")]
    [Range(0f, 1f)]
    public float fireVolume = 0.9f;
    [Range(1f, 50f)]
    public float maxAudioDistance = 30f;

    [Header("🔍 Nhận biết Đạn")]
    [Tooltip("Tag gán cho prefab Arrow và CanonBall")]
    public string projectileTag = "Projectile";
    public LayerMask projectileLayer;
    [Range(0.3f, 5f)]
    public float muzzleCheckRadius = 2.5f;

    [Header("⏱️ Cooldown tối thiểu (giây)")]
    [Range(0.05f, 5f)]
    public float minSoundInterval = 0.3f;

    // ── Private ──────────────────────────────────────────
    private AttackTowerAI       attackAI;
    private UpgradeableBuilding upgradeBuilding;
    private AudioSource         audioSource;

    private int   lastProjectileCount = 0;
    private float lastSoundTime       = -99f;
    private Transform cachedFirePoint;

    // ─────────────────────────────────────────────────────
    private void Start()
    {
        attackAI        = GetComponent<AttackTowerAI>();
        upgradeBuilding = GetComponent<UpgradeableBuilding>(); // nullable – OK
        audioSource     = GetComponent<AudioSource>();

        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        audioSource.minDistance  = 3f;
        audioSource.maxDistance  = maxAudioDistance;
        audioSource.dopplerLevel = 0f;

        // FIX 1: Bật auto sync một lần thay vì gọi SyncTransforms() mỗi frame
        Physics.autoSyncTransforms = true;

        ValidateClipArrays();
    }

    private void Update()
    {
        if (attackAI == null) return;

        RefreshFirePoint();
        if (cachedFirePoint == null) return;

        int currentCount = CountProjectilesNearMuzzle();

        // FIX 4: Chỉ trigger nếu đã qua cooldown VÀ có đạn mới
        if (currentCount > lastProjectileCount)
        {
            float timeSinceLast = Time.time - lastSoundTime;
            if (timeSinceLast >= minSoundInterval)
            {
                PlayFireSound();
                lastSoundTime = Time.time;
                // Reset ngay để tránh trigger lại frame kế nếu count vẫn cao
                lastProjectileCount = currentCount;
                return;
            }
        }

        lastProjectileCount = currentCount;
    }

    // ─────────────────────────────────────────────────────
    private int CountProjectilesNearMuzzle()
    {
        // FIX 1: Bỏ Physics.SyncTransforms() ở đây vì đã bật autoSyncTransforms
        Vector3 checkPos = cachedFirePoint.position;
        int count = 0;

        if (projectileLayer.value != 0)
        {
            Collider[] hits = Physics.OverlapSphere(
                checkPos, muzzleCheckRadius,
                projectileLayer,
                QueryTriggerInteraction.Collide);
            return hits.Length;
        }

        if (!string.IsNullOrEmpty(projectileTag))
        {
            Collider[] allHits = Physics.OverlapSphere(
                checkPos, muzzleCheckRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (var col in allHits)
            {
                if (col == null) continue;
                if (col.CompareTag(projectileTag)) count++;
            }
        }

        return count;
    }

    // ─────────────────────────────────────────────────────
    private void PlayFireSound()
    {
        int level = upgradeBuilding != null ? upgradeBuilding.CurrentLevel : 0;

        AudioClip[] clips = (attackAI.towerType == AttackTowerType.CrossbowTower)
            ? archerFireClips
            : cannonFireClips;

        AudioClip clip = GetClipForLevel(clips, level);
        if (clip == null) return;

        audioSource.PlayOneShot(clip, fireVolume);
    }

    private AudioClip GetClipForLevel(AudioClip[] clips, int level)
    {
        if (clips == null || clips.Length == 0) return null;

        int idx = Mathf.Clamp(level, 0, clips.Length - 1);
        if (clips[idx] != null) return clips[idx];

        for (int i = idx - 1; i >= 0; i--)
            if (clips[i] != null) return clips[i];
        for (int i = idx + 1; i < clips.Length; i++)
            if (clips[i] != null) return clips[i];

        return null;
    }

    // ─────────────────────────────────────────────────────
    private void RefreshFirePoint()
    {
        if (attackAI.firePoint == null || attackAI.firePoint == cachedFirePoint) return;

        // FIX 2: Cập nhật cache TRƯỚC, rồi mới đếm để dùng đúng vị trí mới
        cachedFirePoint = attackAI.firePoint;
        lastProjectileCount = CountProjectilesNearMuzzle();
    }

    // ─────────────────────────────────────────────────────
    private void ValidateClipArrays()
    {
        archerFireClips = EnsureLength(archerFireClips, 3);
        cannonFireClips = EnsureLength(cannonFireClips, 3);
    }

    private AudioClip[] EnsureLength(AudioClip[] arr, int length)
    {
        if (arr != null && arr.Length >= length) return arr;
        AudioClip[] newArr = new AudioClip[length];
        if (arr != null) System.Array.Copy(arr, newArr, Mathf.Min(arr.Length, length));
        return newArr;
    }

    // FIX 5: Dùng cached attackAI thay vì GetComponent trong Gizmo
    private void OnDrawGizmosSelected()
    {
        var ai = attackAI != null ? attackAI : GetComponent<AttackTowerAI>();
        if (ai == null || ai.firePoint == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawSphere(ai.firePoint.position, muzzleCheckRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(ai.firePoint.position, muzzleCheckRadius);
    }
}
