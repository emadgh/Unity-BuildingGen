using System.Collections.Generic;
using UnityEngine;

namespace Emadgh.BuildingGen.Editor
{
    internal struct FacadeBayReservation
    {
        public int firstFloor;
        public int lastFloor;
        public float minX;
        public float maxX;
        public FacadeBayKind kind;
    }

    /// <summary>
    /// One-dimensional reservation map for a facade strip. Reservations may span multiple floors.
    /// Intervals are stored with neighbor clearance already applied, so all facade features compete
    /// through the same overlap test.
    /// </summary>
    internal sealed class FacadeBayReservationMap
    {
        private readonly List<FacadeBayReservation> reservations = new List<FacadeBayReservation>();

        public bool TryReserve(
            int firstFloor,
            int floorSpan,
            float centerX,
            float width,
            float neighborClearance,
            FacadeBayKind kind)
        {
            int lastFloor = firstFloor + Mathf.Max(1, floorSpan) - 1;
            float half = width * 0.5f;
            float padding = Mathf.Max(0f, neighborClearance);
            float minX = centerX - half - padding;
            float maxX = centerX + half + padding;

            for (int i = 0; i < reservations.Count; i++)
            {
                FacadeBayReservation other = reservations[i];
                bool floorOverlap = firstFloor <= other.lastFloor && lastFloor >= other.firstFloor;
                if (!floorOverlap)
                    continue;

                bool horizontalOverlap = minX < other.maxX && maxX > other.minX;
                if (horizontalOverlap)
                    return false;
            }

            reservations.Add(new FacadeBayReservation
            {
                firstFloor = firstFloor,
                lastFloor = lastFloor,
                minX = minX,
                maxX = maxX,
                kind = kind
            });
            return true;
        }

        public int CountKind(FacadeBayKind kind)
        {
            int count = 0;
            for (int i = 0; i < reservations.Count; i++)
            {
                if (reservations[i].kind == kind)
                    count++;
            }
            return count;
        }
    }
}
