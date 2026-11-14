using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class CottageCollisionImporter : AssetPostprocessor
{
    private const string CottagePath = "Assets/Do_Shesti_Cottage.fbx";

    private void OnPostprocessModel(GameObject root)
    {
        if (!string.Equals(assetPath, CottagePath, StringComparison.Ordinal))
            return;

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var obj = filter.gameObject;
            var name = obj.name;
            // Decorative layers sit proud of the planes that should guide the player.
            if (name.EndsWith(" | skirting, wainscot, cornice", StringComparison.Ordinal) ||
                name.EndsWith(" | mitred skirting and cornice", StringComparison.Ordinal) ||
                name.EndsWith(" | oak threshold", StringComparison.Ordinal) ||
                name.EndsWith(" | leaf.001", StringComparison.Ordinal) ||
                name == "Stair | rear oak boards")
                continue;

            if (filter.sharedMesh == null)
                continue;

            var collision = obj.AddComponent<MeshCollider>();
            collision.sharedMesh = filter.sharedMesh;
            collision.convex = false;
        }
    }

    [MenuItem("Tools/Cottage/Verify Imported House")]
    private static void VerifyImportedHouse()
    {
        var root = SceneManager.GetActiveScene().GetRootGameObjects()
            .SingleOrDefault(candidate => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate) == CottagePath);
        if (root == null)
            throw new InvalidOperationException("The cottage FBX instance is missing from the active scene.");

        var filters = root.GetComponentsInChildren<MeshFilter>();
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        var collisions = root.GetComponentsInChildren<MeshCollider>();
        var thresholds = filters.Count(f => f.name.EndsWith(" | oak threshold", StringComparison.Ordinal));
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        if (bounds.size.x < 19.7f || bounds.size.z < 14.0f || thresholds != 6 || collisions.Length < 65)
            throw new InvalidOperationException($"Unexpected cottage: size {bounds.size}, {thresholds} thresholds, {collisions.Length} colliders.");

        var invalid = renderers.SelectMany(r => r.sharedMaterials).Distinct()
            .Where(m => m == null || !m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") == null ||
                        m.GetTexture("_BumpMap") == null || m.GetFloat("_BumpScale") != 1 ||
                        m.mainTexture.width > 2048 || m.mainTexture.height > 2048 ||
                        !Mathf.Approximately(m.mainTextureScale.x, m.mainTextureScale.y))
            .ToArray();
        if (invalid.Length != 0)
            throw new InvalidOperationException("Missing or invalid cottage materials: " +
                string.Join(", ", invalid.Select(m => m != null ? m.name : "<missing>")));

        Physics.SyncTransforms();
        var hall = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Floor | Vestibule and stair hall");
        float floorHeight = hall.bounds.max.y;
        var stair = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name.StartsWith("Straight stair |", StringComparison.Ordinal));
        var landing = root.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Floor | Gallery landing");
        float direction = Mathf.Sign(landing.bounds.center.z - stair.bounds.center.z);
        float front = stair.bounds.center.z - direction * stair.bounds.extents.z;
        float back = stair.bounds.center.z + direction * stair.bounds.extents.z;
        for (int i = 0; i < 18; i++)
        {
            var z = Mathf.Lerp(front, back, (i + 0.58f) / 18);
            var origin = new Vector3(stair.bounds.center.x, floorHeight + (i + 1) * (3.25f / 19f) + .08f, z);
            if (!Physics.Raycast(origin, Vector3.down, out var hit, .28f) ||
                hit.collider.gameObject != stair.gameObject)
                throw new InvalidOperationException($"Missing collision on stair step {i + 1}.");
        }

        var from = new Vector3(hall.bounds.center.x, floorHeight + .42f, hall.bounds.center.z);
        if (!Physics.Raycast(from, Vector3.down, out var floorHit, .65f) ||
            floorHit.collider.gameObject != hall.gameObject)
            throw new InvalidOperationException("Missing ground floor collision.");

        foreach (var renderer in renderers.Where(r => r.name.Contains(" | fixed sash ")))
        {
            var collider = renderer.GetComponent<MeshCollider>();
            if (collider == null || collider.sharedMesh == null)
                throw new InvalidOperationException($"Missing window collision: {renderer.name}");
        }

        var doorCount = collisions.Count(c => c.name.EndsWith(" | leaf", StringComparison.Ordinal));
        if (doorCount != 11)
            throw new InvalidOperationException($"Expected 11 collision-enabled door leaves; found {doorCount}.");
        foreach (var frame in renderers.Where(r => r.name.EndsWith(" | casing", StringComparison.Ordinal)))
        {
            var frameBounds = frame.bounds;
            bool alongX = frameBounds.size.x > frameBounds.size.z;
            var across = alongX ? Vector3.forward : Vector3.right;
            var along = alongX ? Vector3.right : Vector3.forward;
            var center = frameBounds.center;
            center.y = frameBounds.min.y + 1.1f;
            bool sealedEntrance = frame.name.Contains("fixed entrance");
            foreach (float offset in new[] { -.24f, 0, .24f })
            {
                var start = center + along * offset - across * .40f;
                bool blocked = Physics.Raycast(start, across, .80f);
                if (blocked != sealedEntrance)
                    throw new InvalidOperationException($"Unexpected doorway obstruction state at {frame.name}.");
            }
        }

        Debug.Log($"Cottage verified: {renderers.Length} renderers, {collisions.Length} colliders, " +
                  $"{thresholds} thresholds, 10 open passages, sealed entrance, " +
                  $"walkable collision on all 18 stair treads, size {bounds.size}.");
    }
}

