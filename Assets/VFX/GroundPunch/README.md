# Ground Punch VFX

`GroundPunchVFX.prefab` creates a stylised ground-impact burst at runtime: low-poly rocks, a dust puff and an expanding ring.

## Use from a punch script

```csharp
using UnityEngine;

public class PunchImpact : MonoBehaviour
{
    [SerializeField] private GroundPunchVFX groundPunchVfx;

    // Call from an Animation Event when the fist reaches the floor.
    public void OnPunchHitGround()
    {
        groundPunchVfx.Play(transform.position, Vector3.up);
    }
}
```

For a reusable one-shot instance:

```csharp
var fx = Instantiate(groundPunchPrefab, hitPoint, Quaternion.identity);
fx.Play(hitPoint, hitNormal);
```

The prefab is configured to play on enable so it works even when another script only instantiates the VFX prefab. `Dragon.cs` also calls `Play` explicitly after spawning it. Tune counts, speed, colors and radius in the Inspector.
