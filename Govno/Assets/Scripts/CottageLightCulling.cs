using System;
using System.Collections.Generic;
using UnityEngine;
using UntilSix.Monster;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class CottageLightCulling : MonoBehaviour
{
    [Serializable]
    private struct Opening
    {
        public CeilingLamp firstRoom;
        public CeilingLamp secondRoom;
        public BoxCollider doorway;
        public CottageDoor door;
        public Bounds passage;
    }

    private struct Connection
    {
        public ulong rooms;
        public CottageDoor door;
        public BoxCollider doorway;
        public Bounds bounds;
    }

    [SerializeField] private Camera viewCamera;
    [SerializeField] private LampLightZone[] zones;
    [SerializeField] private Opening[] openings;

    private readonly Plane[] frustum = new Plane[6];
    private CeilingLamp[] rooms;
    private ulong[] zoneRooms;
    private Connection[] connections;
    private bool[] open;
    private bool[] visible;
    private bool[] inLightRange;

    private void Awake()
    {
        if (viewCamera == null || zones == null || openings == null)
            return;

        var fixtures = new List<CeilingLamp>();
        foreach (var zone in zones)
            if (zone != null && zone.Fixture != null && !fixtures.Contains(zone.Fixture))
                fixtures.Add(zone.Fixture);

        if (fixtures.Count == 0 || fixtures.Count > 64)
            return;

        rooms = fixtures.ToArray();
        zoneRooms = new ulong[zones.Length];
        for (int i = 0; i < zones.Length; i++)
            if (zones[i] != null && zones[i].Fixture != null)
                zoneRooms[i] = 1UL << fixtures.IndexOf(zones[i].Fixture);

        connections = new Connection[openings.Length];
        open = new bool[openings.Length];
        visible = new bool[openings.Length];
        inLightRange = new bool[openings.Length];
        for (int i = 0; i < openings.Length; i++)
        {
            var opening = openings[i];
            int first = fixtures.IndexOf(opening.firstRoom);
            int second = fixtures.IndexOf(opening.secondRoom);
            if (first < 0 || second < 0 || first == second)
            {
                connections = null;
                return;
            }

            var bounds = opening.doorway != null ? opening.doorway.bounds : opening.passage;
            bounds.Expand(0.2f);
            connections[i] = new Connection
            {
                rooms = (1UL << first) | (1UL << second),
                door = opening.door,
                doorway = opening.doorway,
                bounds = bounds
            };
        }
    }

    private void LateUpdate()
    {
        if (connections == null)
            return;

        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            RestoreLights();
            return;
        }

        Vector3 eye = viewCamera.transform.position;
        ulong seenRooms = 0;
        for (int i = 0; i < zones.Length; i++)
            if (zoneRooms[i] != 0 && zones[i] != null && zones[i].ContainsPosition(eye))
                seenRooms |= zoneRooms[i];

        // The doorway can straddle a gap between the room volumes.
        GeometryUtility.CalculateFrustumPlanes(viewCamera, frustum);
        for (int i = 0; i < connections.Length; i++)
        {
            var connection = connections[i];
            if (connection.doorway != null &&
                (!connection.doorway.enabled || !connection.doorway.gameObject.activeInHierarchy))
            {
                RestoreLights();
                return;
            }

            open[i] = connection.door == null || !connection.door.isActiveAndEnabled ||
                      connection.door.IsLightPassageOpen;
            if (open[i] && connection.bounds.Contains(eye))
                seenRooms |= connection.rooms;
            visible[i] = open[i] && GeometryUtility.TestPlanesAABB(frustum, connection.bounds);
        }

        if (seenRooms == 0)
        {
            RestoreLights();
            return;
        }

        seenRooms = Traverse(seenRooms, visible);
        for (int i = 0; i < rooms.Length; i++)
        {
            var lamp = rooms[i];
            if (lamp == null)
                continue;

            ulong sourceRoom = 1UL << i;
            var light = lamp.RenderLight;
            bool keep = (seenRooms & sourceRoom) != 0 || light == null || light.type != LightType.Point;
            if (!keep && lamp.IsWorking)
                keep = CouldLightVisibleRoom(light, sourceRoom, seenRooms);

            lamp.SetRenderingVisible(keep);
        }
    }

    private bool CouldLightVisibleRoom(Light light, ulong sourceRoom, ulong seenRooms)
    {
        // A light may reach a visible wall via an open door outside the camera frustum.
        Vector3 origin = light.transform.position;
        float rangeSquared = light.range * light.range;
        for (int i = 0; i < connections.Length; i++)
            inLightRange[i] = open[i] && connections[i].bounds.SqrDistance(origin) <= rangeSquared;

        return (Traverse(sourceRoom, inLightRange) & seenRooms) != 0;
    }

    private ulong Traverse(ulong reached, bool[] accessible)
    {
        ulong previous;
        do
        {
            previous = reached;
            for (int i = 0; i < connections.Length; i++)
                if (accessible[i] && (connections[i].rooms & reached) != 0)
                    reached |= connections[i].rooms;
        } while (previous != reached);

        return reached;
    }

    private void OnDisable() => RestoreLights();

    private void RestoreLights()
    {
        if (rooms == null)
            return;

        foreach (var lamp in rooms)
            if (lamp != null)
                lamp.SetRenderingVisible(true);
    }
}

