﻿using DynamicAsteroids.AsteroidEntities;
using Invalid.DynamicRoids;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Input;
using VRageMath;
using ProtoBuf;
using Sandbox.Game.Entities;

namespace DynamicAsteroids
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class MainSession : MySessionComponentBase
    {
        public static MainSession I;
        public Random Rand;
        private int seed;
        public AsteroidSpawner _spawner = new AsteroidSpawner();
        private int _saveStateTimer;
        private int _networkMessageTimer;

        public override void LoadData()
        {
            I = this;
            Log.Init();
            AsteroidSettings.LoadSettings(); // Load settings from the config file

            try
            {
                Log.Info("Loading data in MainSession");
                seed = AsteroidSettings.Seed;
                Rand = new Random(seed);

                if (MyAPIGateway.Session.IsServer)
                {
                    _spawner.Init(seed);
                    if (AsteroidSettings.EnablePersistence)
                    {
                        _spawner.LoadAsteroidState();
                    }
                }

                MyAPIGateway.Multiplayer.RegisterMessageHandler(32000, OnMessageReceived);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, typeof(MainSession));
            }
        }

        protected override void UnloadData()
        {
            try
            {
                Log.Info("Unloading data in MainSession");
                if (MyAPIGateway.Session.IsServer)
                {
                    if (AsteroidSettings.EnablePersistence)
                    {
                        _spawner.SaveAsteroidState();
                    }
                    _spawner.Close();
                }

                AsteroidSettings.SaveSettings(); // Save settings to the config file

                MyAPIGateway.Multiplayer.UnregisterMessageHandler(32000, OnMessageReceived);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, typeof(MainSession));
            }

            Log.Close();
            I = null;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (MyAPIGateway.Session.IsServer)
                {
                    _spawner.UpdateTick();
                    if (_saveStateTimer > 0)
                    {
                        _saveStateTimer--;
                    }
                    else
                    {
                        _spawner.SaveAsteroidState();
                        _saveStateTimer = AsteroidSettings.SaveStateInterval;
                    }

                    if (_networkMessageTimer > 0)
                    {
                        _networkMessageTimer--;
                    }
                    else
                    {
                        Log.Info($"Server: Sending network messages, asteroid count: {_spawner._asteroids.Count}");
                        _spawner.SendNetworkMessages();
                        _networkMessageTimer = AsteroidSettings.NetworkMessageInterval;
                    }
                }

                if (MyAPIGateway.Session?.Player?.Character != null && _spawner._asteroids != null)
                {
                    Vector3D characterPosition = MyAPIGateway.Session.Player.Character.PositionComp.GetPosition();
                    AsteroidEntity nearestAsteroid = FindNearestAsteroid(characterPosition);
                    if (nearestAsteroid != null)
                    {
                        Vector3D angularVelocity = nearestAsteroid.Physics.AngularVelocity;
                        string rotationString = $"({angularVelocity.X:F2}, {angularVelocity.Y:F2}, {angularVelocity.Z:F2})";
                        string message = $"Nearest Asteroid: {nearestAsteroid.EntityId} ({nearestAsteroid.Type})\nRotation: {rotationString}";
                        if (AsteroidSettings.EnableLogging) MyAPIGateway.Utilities.ShowNotification(message, 1000 / 60);
                    }
                }

                if (AsteroidSettings.EnableMiddleMouseAsteroidSpawn && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.MiddleButton))
                {
                    if (MyAPIGateway.Session != null)
                    {
                        var position = MyAPIGateway.Session.Player?.GetPosition() ?? Vector3D.Zero;
                        var velocity = MyAPIGateway.Session.Player?.Character?.Physics?.LinearVelocity ?? Vector3D.Zero;
                        AsteroidType type = DetermineAsteroidType();
                        AsteroidEntity.CreateAsteroid(position, Rand.Next(50), velocity, type);
                        Log.Info($"Asteroid created at {position} with velocity {velocity}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, typeof(MainSession));
            }
        }

        private void OnMessageReceived(byte[] message)
        {
            try
            {
                if (message == null || message.Length == 0)
                {
                    Log.Info("Received empty or null message, skipping processing.");
                    return;
                }

                Log.Info($"Client: Received message of {message.Length} bytes");
                var asteroidMessage = MyAPIGateway.Utilities.SerializeFromBinary<AsteroidNetworkMessage>(message);

                if (asteroidMessage.IsRemoval)
                {
                    var asteroid = MyEntities.GetEntityById(asteroidMessage.EntityId) as AsteroidEntity;
                    if (asteroid != null)
                    {
                        Log.Info($"Client: Removing asteroid with ID {asteroidMessage.EntityId}");
                        MyEntities.Remove(asteroid);
                        asteroid.Close();
                        Log.Info($"Client: Removed asteroid with ID {asteroidMessage.EntityId}");
                    }
                    else
                    {
                        Log.Info($"Client: Failed to find asteroid with ID {asteroidMessage.EntityId} for removal");
                    }
                }
                else
                {
                    var existingAsteroid = MyEntities.GetEntityById(asteroidMessage.EntityId) as AsteroidEntity;
                    if (existingAsteroid != null)
                    {
                        Log.Info($"Client: Asteroid with ID {asteroidMessage.EntityId} already exists, skipping creation");
                    }
                    else
                    {
                        Log.Info($"Client: Creating asteroid with provided details");
                        var asteroid = AsteroidEntity.CreateAsteroid(
                            asteroidMessage.GetPosition(),
                            asteroidMessage.Size,
                            asteroidMessage.GetVelocity(),
                            asteroidMessage.GetType(),
                            asteroidMessage.GetRotation(),
                            asteroidMessage.EntityId);

                        if (asteroid != null)
                        {
                            asteroid.Physics.AngularVelocity = asteroidMessage.GetAngularVelocity();
                            MyEntities.Add(asteroid);
                            Log.Info($"Client: Created asteroid with ID {asteroid.EntityId}");
                        }
                        else
                        {
                            Log.Info($"Client: Failed to create asteroid with ID {asteroidMessage.EntityId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, typeof(MainSession), "Error processing received message: ");
            }
        }

        private AsteroidEntity FindNearestAsteroid(Vector3D characterPosition)
        {
            if (_spawner._asteroids == null) return null;

            AsteroidEntity nearestAsteroid = null;
            double minDistance = double.MaxValue;
            foreach (var asteroid in _spawner._asteroids)
            {
                double distance = Vector3D.DistanceSquared(characterPosition, asteroid.PositionComp.GetPosition());
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestAsteroid = asteroid;
                }
            }
            return nearestAsteroid;
        }

        private AsteroidType DetermineAsteroidType()
        {
            int randValue = Rand.Next(0, 2);
            return (AsteroidType)randValue;
        }
    }
}
