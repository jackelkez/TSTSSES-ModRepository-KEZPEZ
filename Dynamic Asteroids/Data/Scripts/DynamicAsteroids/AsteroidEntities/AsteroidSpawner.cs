﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using SC.SUGMA;
using VRageMath;

namespace DynamicAsteroids.AsteroidEntities
{
    internal class AsteroidSpawner
    {
        private List<AsteroidEntity> _asteroids;

        public void Init()
        {
            Log.Info("Initializing AsteroidSpawner");
            _asteroids = new List<AsteroidEntity>(AsteroidSettings.MaxAsteroidCount);
        }

        public void Close()
        {
            Log.Info("Closing AsteroidSpawner");
            _asteroids.Clear();
        }

        public void UpdateTick()
        {
            try
            {
                Vector3D playerPosition = MyAPIGateway.Session?.Player?.GetPosition() ?? Vector3D.MaxValue;

                if (playerPosition == Vector3D.MaxValue || !AsteroidSettings.PlayerCanSeeRings(playerPosition))
                    return;

                foreach (var asteroid in _asteroids.ToArray())
                {
                    if (Vector3D.DistanceSquared(asteroid.PositionComp.GetPosition(), playerPosition) >
                        AsteroidSettings.AsteroidSpawnRadius * AsteroidSettings.AsteroidSpawnRadius * 1.1)
                    {
                        _asteroids.Remove(asteroid);
                        asteroid.Close();
                        continue;
                    }
                }

                int asteroidsSpawned = 0;
                while (_asteroids.Count < AsteroidSettings.MaxAsteroidCount && asteroidsSpawned < 10)
                {
                    Vector3D newPosition = playerPosition + RandVector() * AsteroidSettings.AsteroidSpawnRadius;
                    Vector3D newVelocity;
                    if (!AsteroidSettings.CanSpawnAsteroidAtPoint(newPosition, out newVelocity))
                        continue;
                    _asteroids.Add(AsteroidEntity.CreateAsteroid(newPosition, RandAsteroidSize, newVelocity));

                    asteroidsSpawned++;
                }

                // Show a notification with the number of active asteroids
                MyAPIGateway.Utilities.ShowNotification($"Active Asteroids: {_asteroids.Count}", 17);

                // Log the number of active asteroids for debugging purposes
                Log.Info($"Active Asteroids: {_asteroids.Count}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex, typeof(AsteroidSpawner));
            }
        }

        private Vector3D RandVector()
        {
            var theta = MainSession.I.Rand.NextDouble() * 2.0 * Math.PI;
            var phi = Math.Acos(2.0 * MainSession.I.Rand.NextDouble() - 1.0);
            var sinPhi = Math.Sin(phi);
            return Math.Pow(MainSession.I.Rand.NextDouble(), 1 / 3d) * new Vector3D(sinPhi * Math.Cos(theta), sinPhi * Math.Sin(theta), Math.Cos(phi));
        }

        private float RandAsteroidSize => (float)(MainSession.I.Rand.NextDouble() * MainSession.I.Rand.NextDouble() * MainSession.I.Rand.NextDouble() * 500) + 1.5f;
    }
}
