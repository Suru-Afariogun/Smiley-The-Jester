using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records the player's path so MoodDrops can replay jumps and movement exactly.
/// </summary>
public class PlayerPathRecorder : MonoBehaviour
{
    public static PlayerPathRecorder Instance { get; private set; }

    [SerializeField] float recordInterval = 0.05f;
    [SerializeField] int maxPathPoints = 600;

    readonly List<Vector2> path = new();
    float nextRecordTime;

    public IReadOnlyList<Vector2> Path => path;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Time.time < nextRecordTime)
            return;

        nextRecordTime = Time.time + recordInterval;
        path.Add(transform.position);

        while (path.Count > maxPathPoints)
            path.RemoveAt(0);
    }

    public int FindClosestPathIndex(Vector2 worldPosition)
    {
        if (path.Count == 0)
            return 0;

        int closest = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < path.Count; i++)
        {
            float distance = Vector2.Distance(path[i], worldPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = i;
            }
        }

        return closest;
    }
}
