using UnityEngine;

public class CollectibleSpawner : MonoBehaviour
{
    [Header("Spawn Configurations")]
    public GameObject collectiblePrefab;
    [SerializeField] private GameObject[] _spawns = new GameObject[3];
    public int spawnCount = 3;

    [Header("Spawn Region")]
    [SerializeField] private BoxCollider spawnArea;

    [Header("Spacing")]
    [Tooltip("Minimum distance allowed between any two collectibles.")]
    [SerializeField] private float minDistanceBetweenSpawns = 2f;
    [Tooltip("How many random positions to try before giving up on one spawn.")]
    [SerializeField] private int maxPlacementAttempts = 30;

    // Track which slots are currently occupied (in use)
    private bool[] _slotActive;

    private void Awake()
    {
        _slotActive = new bool[_spawns.Length];
    }

    private void Start()
    {
        SpawnCollectibles();
    }

    private void Update()
    {
        // Fallback: if any tracked collectible was destroyed or deactivated
        // (i.e. picked up), respawn it in the same slot.
        for (int i = 0; i < _spawns.Length; i++)
        {
            if (_slotActive[i] && _spawns[i] == null)
            {
                _slotActive[i] = false;
                RespawnInSlot(i);
            }
        }

        // Keep spawnCount in sync with active slots
        int count = 0;
        for (int i = 0; i < _slotActive.Length; i++)
            if (_slotActive[i]) count++;
        spawnCount = count;
    }

    /// <summary>
    /// Spawns the initial batch of collectibles.
    /// </summary>
    public void SpawnCollectibles()
    {
        if (collectiblePrefab == null || spawnArea == null)
        {
            Debug.LogWarning("Please assign both the Prefab and Spawn Area!");
            return;
        }

        // Clear existing spawns if you call this again at runtime
        for (int i = 0; i < _spawns.Length; i++)
        {
            if (_spawns[i] != null) Destroy(_spawns[i]);
            _slotActive[i] = false;
        }

        int amount = Mathf.Min(spawnCount, _spawns.Length);

        for (int i = 0; i < amount; i++)
        {
            Vector3 pos;
            if (TryFindFreePosition(out pos))
            {
                _spawns[i] = Instantiate(collectiblePrefab, pos, Quaternion.identity);
                _slotActive[i] = true;
            }
            else
            {
                Debug.LogWarning($"Could not find a free position for collectible #{i}. " +
                                 $"Consider reducing minDistance or spawnCount.");
            }
        }
    }

    /// <summary>
    /// Respawns a single collectible into a specific slot.
    /// </summary>
    private void RespawnInSlot(int slotIndex)
    {
        if (collectiblePrefab == null || spawnArea == null) return;

        Vector3 pos;
        if (TryFindFreePosition(out pos))
        {
            _spawns[slotIndex] = Instantiate(collectiblePrefab, pos, Quaternion.identity);
            _slotActive[slotIndex] = true;
        }
        else
        {
            Debug.LogWarning($"Could not find a free position to respawn slot {slotIndex}.");
        }
    }

    /// <summary>
    /// Tries to find a position inside the spawn area that is far enough from
    /// all currently active collectibles. Falls back to a best-effort position
    /// after maxPlacementAttempts so spawns never silently fail.
    /// </summary>
    private bool TryFindFreePosition(out Vector3 result)
    {
        Bounds bounds = spawnArea.bounds;

        Vector3 bestCandidate = Vector3.zero;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            float nearest = GetNearestActiveDistance(candidate);

            // Accept the first candidate that satisfies the spacing rule
            if (nearest >= minDistanceBetweenSpawns)
            {
                result = candidate;
                return true;
            }

            // Otherwise remember the best candidate seen so far
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                bestCandidate = candidate;
            }
        }

        // Fallback: best we could do. You can return false here instead if you
        // prefer to skip spawning rather than allow a close spawn.
        result = bestCandidate;
        return true;
    }

    /// <summary>
    /// Returns the distance from the given point to the nearest active collectible.
    /// Returns float.MaxValue if there are no active collectibles.
    /// </summary>
    private float GetNearestActiveDistance(Vector3 point)
    {
        float nearest = float.MaxValue;

        for (int i = 0; i < _spawns.Length; i++)
        {
            if (!_slotActive[i] || _spawns[i] == null) continue;

            float d = Vector3.Distance(point, _spawns[i].transform.position);
            if (d < nearest) nearest = d;
        }

        return nearest;
    }

    // ---------- Optional: Editor visualization ----------
    private void OnDrawGizmosSelected()
    {
        if (spawnArea == null) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawCube(spawnArea.bounds.center, spawnArea.bounds.size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(spawnArea.bounds.center, spawnArea.bounds.size);
    }
}
