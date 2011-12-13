﻿using System;
using System.Collections.Generic;
using IHI.Server.Rooms.RoomUnits;

namespace IHI.Server.Plugins.Cecer1.IHIPathfinder
{
    /// <summary>
    /// The logic of the pathfinder. This calculates all the paths.
    /// </summary>
    internal class Logic : IPathfinder
    {
        /// <summary>
        /// Stores the state of the tiles.
        /// </summary>
        private byte[,] _collisionMap;

        /// <summary>
        /// Stores the height of the tiles.
        /// </summary>
        private float[,] _height;

        #region IPathfinder Members

        /// <summary>
        /// Set the tile map.
        /// </summary>
        /// <param name="map">The state of the tiles.</param>
        /// <param name="height">The height of the tiles.</param>
        public void ApplyCollisionMap(byte[,] map, float[,] height)
        {
            // Is this replacing an existing tile map
            if (_collisionMap != null)
                // Yes, ensure thread safety.
                lock (_collisionMap)
                {
                    // Set the tile states.
                    _collisionMap = map;
                    // Set the tile heights
                    _height = height;
                }
            else
            {
                // No, don't worry about other threads.

                // Set the tile states.
                _collisionMap = map;
                // Set the tile heights
                _height = height;
            }
        }

        /// <summary>
        /// Get the next step on a path.
        /// </summary>
        /// <param name="startX">PointA X</param>
        /// <param name="startY">PointA Y</param>
        /// <param name="endX">PointB X</param>
        /// <param name="endY">PointB Y</param>
        /// <param name="maxDrop">The Maximum height to drop in a single step.</param>
        /// <param name="maxJump">The Maximum height to rise in a single step.</param>
        /// <returns></returns>
        public ICollection<byte[]> Path(byte startX, byte startY, byte endX, byte endY, float maxDrop, float maxJump)
        {
            Values values;
            lock (_collisionMap) // Thread Safety
            {
                values = new Values(_collisionMap, _height, maxDrop, maxJump);

                if (endX >= _collisionMap.GetLength(0) || // Is EndX outside the bounds of the collision map?
                    endY >= _collisionMap.GetLength(1) || // Is EndY outside the bounds of the collision map?
                    startX >= _collisionMap.GetLength(0) || // Is StartX outside the bounds of the collision map?
                    startY >= _collisionMap.GetLength(1) || // Is StartY outside the bounds of the collision map?
                    _collisionMap[endX, endY] == 0 || // Is the target blocked by the collision map?
                    (startX == endX && startY == endY)) // Is the start also the target?
                    
                    // If any of these are yes, no path can be made. Don't run the path finder.
                    return new byte[0][];

                #region Init

                /*
                 * G = Cost so far
                 * H = Estimated remaining cost
                 * F = G + H
                 */

                values.Count++;
                values.BinaryHeap[values.Count] = values.LastID;
                values.X[values.LastID] = startX;
                values.Y[values.LastID] = startY;
                values.H[values.LastID] = (ushort) GetH(startX, startY, endX, endY);
                values.Parent[values.LastID] = 0;
                values.G[values.LastID] = 0;
                values.F[values.LastID] = (ushort) (values.G[values.LastID] + values.H[values.LastID]);

                #endregion

                while (values.Count != 0)
                {
                    values.Location = values.BinaryHeap[1];

                    if (values.X[values.Location] == endX && values.Y[values.Location] == endY)
                        break;

                    Move(values);

                    // Add the surrounding tiles.
                    Add(-1, 0, endX, endY, values);
                    Add(0, -1, endX, endY, values);
                    Add(1, 0, endX, endY, values);
                    Add(0, 1, endX, endY, values);

                    Add(-1, -1, endX, endY, values);
                    Add(-1, 1, endX, endY, values);
                    Add(1, -1, endX, endY, values);
                    Add(1, 1, endX, endY, values);
                }
            }

            // If no new tiles can be checked then the path must be impossible.
            if (values.Count == 0)
                return new List<byte[]>();

            var path = new List<byte[]>();

            while (values.X[values.Parent[values.Location]] != startX ||
                   values.Y[values.Parent[values.Location]] != startY)
            {
                path.Add(new[] {values.X[values.Location], values.Y[values.Location]});
                values.Location = values.Parent[values.Location];
            }
            path.Add(new[] {values.X[values.Location], values.Y[values.Location]});
            path.Reverse();

            return path;
        }

        #endregion

        /// <summary>
        /// Estimate the cost from X,Y to EndX,EndY.
        /// </summary>
        /// <returns></returns>
        private static int GetH(int x, int y, int endX, int endY)
        {
            return (Math.Abs(x + endX) + Math.Abs(y + endY));
        }

        /// <summary>
        /// 
        /// </summary>
        private void Add(sbyte x, sbyte y, byte endX, byte endY, Values values)
        {
            var x2 = (byte) (values.X[values.Location] + x);
            var y2 = (byte) (values.Y[values.Location] + y);
            var parent = values.Location;

            if (x2 >= _collisionMap.GetLength(0) || y2 >= _collisionMap.GetLength(1))
                return;

            if (values.Tiles[x2, y2] == 2)
                return;
            if ((_collisionMap[x2, y2] == 0 || (_collisionMap[x2, y2] == 2 && (x2 != endX || y2 != endY))))
                return;

            var z = values.Z[x2, y2];
            var z2 = values.Z[values.X[parent], values.Y[parent]];
            if (z > z2 + values.MaxJump || z < z2 - values.MaxDrop)
                return;

            if (parent > 0)
            {
                if (values.X[parent] != x2 && values.Y[parent] != y2)
                {
                    if (_collisionMap[x2, values.Y[parent]] == 0 || _collisionMap[x2, values.Y[parent]] == 2)
                        return;
                    if (_collisionMap[values.X[parent], y2] == 0 || _collisionMap[values.X[parent], y2] == 2)
                        return;
                }
            }


            if (values.Tiles[x2, y2] == 1)
            {
                ushort i = 1;
                for (; i <= values.Count; i++)
                {
                    if (values.X[i] == x2 && values.Y[i] == y2)
                        break;
                }

                if (values.X[i] == endX || values.Y[i] == endY)
                {
                    if (10 + values.G[parent] < values.G[i])
                        values.Parent[i] = parent;
                }
                else if (14 + values.G[parent] < values.G[i])
                    values.Parent[i] = parent;
                return;
            }

            values.LastID++;
            values.Count++;
            values.BinaryHeap[values.Count] = values.LastID;
            values.X[values.LastID] = x2;
            values.Y[values.LastID] = y2;
            values.H[values.LastID] = (ushort) GetH(x2, y2, endX, endY);
            values.Parent[values.LastID] = parent;

            if (x2 == values.X[parent] || y2 == values.Y[parent])
                values.G[values.LastID] = (ushort) (10 + values.G[parent]);
            else
                values.G[values.LastID] = (ushort) (14 + values.G[parent]);
            values.F[values.LastID] = (ushort) (values.G[values.LastID] + values.H[values.LastID]);

            for (var c = values.Count; c != 1; c /= 2)
            {
                if (values.F[values.BinaryHeap[c]] > values.F[values.BinaryHeap[c/2]])
                    break;
                var temp = values.BinaryHeap[c / 2];
                values.BinaryHeap[c/2] = values.BinaryHeap[c];
                values.BinaryHeap[c] = temp;
            }
            values.Tiles[x2, y2] = 1;
        }

        private static void Move(Values values)
        {
            values.Tiles[values.X[values.BinaryHeap[1]], values.Y[values.BinaryHeap[1]]] = 2;


            values.BinaryHeap[1] = values.BinaryHeap[values.Count];
            values.Count--;

            ushort location = 1;
            while (true)
            {
                var high = location;
                if (2*high + 1 <= values.Count)
                {
                    if (values.F[values.BinaryHeap[high]] >= values.F[values.BinaryHeap[2*high]])
                        location = (ushort) (2*high);
                    if (values.F[values.BinaryHeap[location]] >= values.F[values.BinaryHeap[2*high + 1]])
                        location = (ushort) (2*high + 1);
                }
                else if (2*high <= values.Count)
                {
                    if (values.F[values.BinaryHeap[high]] >= values.F[values.BinaryHeap[2*high]])
                        location = (ushort) (2*high);
                }

                if (high == location)
                    break;
                var temp = values.BinaryHeap[high];
                values.BinaryHeap[high] = values.BinaryHeap[location];
                values.BinaryHeap[location] = temp;
            }
        }
    }
}