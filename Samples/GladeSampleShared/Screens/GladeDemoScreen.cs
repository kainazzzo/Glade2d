﻿using System;
using Glade2d.Screens;
using Glade2d.Services;
using Glade2d.Utility;
using GladeSampleShared.Entities;
using System.Collections.Generic;
using System.Numerics;
using Glade2d;
using Glade2d.Graphics;
using Meadow.Foundation;
using Meadow.Foundation.Graphics;

namespace GladeSampleShared.Screens
{
    public class GladeDemoScreen : Screen, IDisposable
    {
        private const int NumberOfClouds = 3;

        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly ILayer _skyLayer;
        private readonly ILayer _treeLayer;
        private readonly ILayer _mountainLayer;
        private readonly ILayer _groundLayer;
        private readonly List<Cloud> _clouds = new List<Cloud>();
        private readonly Color _backgroundColor = new Color(57, 120, 168);
        private readonly Vector2 _treeVelocity = new Vector2(-5, 0);
        private readonly Vector2 _groundVelocity = new Vector2(-10, 0);
        private readonly Vector2 _mountainVelocity = new Vector2(-2, 0);

        public GladeDemoScreen()
        {
            // set screen dimensions for easy reference
            _screenWidth = GameService.Instance.GameInstance.Renderer.Width;
            _screenHeight = GameService.Instance.GameInstance.Renderer.Height;

            // Set background color
            GameService.Instance.GameInstance.Renderer.BackgroundColor = _backgroundColor;

            _skyLayer = CreateSkyLayer();
            _treeLayer = CreateTreeLayer();
            _mountainLayer = CreateMountainLayer();
            _groundLayer = CreateGroundLayer();

            CreateSun();
            CreateInitialClouds();
        }

        public override void Activity()
        {
            DoClouds();

            // Shift layers by their velocity
            var timeSinceLastFrame = (float)GameService.Instance.Time.FrameDelta;
            _treeLayer.Shift(_treeVelocity * timeSinceLastFrame);
            _mountainLayer.Shift(_mountainVelocity * timeSinceLastFrame);
            _groundLayer.Shift(_groundVelocity * timeSinceLastFrame);

            base.Activity();
        }

        private ILayer CreateSkyLayer()
        {
            var skyChunk = new SkyChunk();

            // Sky doesn't move, so it can be the same width of the screen
            var layer = GameService.Instance.GameInstance.Renderer.CreateLayer(
                new Dimensions(_screenWidth, skyChunk.CurrentFrame.Height));
            
            layer.CameraOffset = new Point(0, 0);

            GameService.Instance.GameInstance.LayerManager.AddLayer(layer, -1);
            for (var x = 0; x < _screenWidth; x += skyChunk.CurrentFrame.Width)
            {
                layer.DrawTexture(skyChunk.CurrentFrame, new Point(x, 0));
            }

            return layer;
        }

        private ILayer CreateTreeLayer()
        {
            var tree = new Tree();
            var ground = new GroundChunk();

            // We want to make sure the layer is at least as wide as the screen, 
            // but also wide enough that it can tile with itself, so no seams show
            // when it scrolls. The trees will be staggered in two "depths", with
            // every other in front of the two surrounding to it.
            var layerWidth = _screenWidth +
                             (_screenWidth % (tree.CurrentFrame.Width / 2));

            var layer = GameService.Instance.GameInstance.Renderer.CreateLayer(new Dimensions(layerWidth, tree.CurrentFrame.Height));
            layer.CameraOffset = new Point(0, _screenHeight - tree.CurrentFrame.Height - ground.CurrentFrame.Height);
            layer.BackgroundColor = new Color(79, 84, 107);
            layer.Clear();

            GameService.Instance.GameInstance.LayerManager.AddLayer(layer, -1);

            // TODO: Clear layer to the main color of mountains, to pretend it has
            // transparency.

            // Draw background trees first
            for (var x = 0; x < layerWidth; x += tree.CurrentFrame.Width)
            {
                layer.DrawTexture(tree.CurrentFrame, new Point(x, 0));
            }

            // Now draw the foreground trees
            for (var x = tree.CurrentFrame.Width / 2; x < layerWidth; x += tree.CurrentFrame.Width)
            {
                layer.DrawTexture(tree.CurrentFrame, new Point(x, 0));
            }

            return layer;
        }

        private ILayer CreateMountainLayer()
        {
            var mountain = new MountainChunk();

            // We want to make sure the layer is at least as wide as the screen, 
            // but also wide enough that it can tile with itself, so no seams show
            // when it scrolls. 
            var layerWidth = _screenWidth + (_screenWidth % (mountain.CurrentFrame.Width));

            var layer = GameService.Instance.GameInstance.Renderer.CreateLayer(
                new Dimensions(layerWidth, mountain.CurrentFrame.Height));
            
            layer.BackgroundColor = _backgroundColor;
            layer.Clear();
            layer.CameraOffset = new Point(0, _screenHeight - 16 - mountain.CurrentFrame.Height);

            GameService.Instance.GameInstance.LayerManager.AddLayer(layer, -2);

            for (var x = 0; x < layerWidth; x += mountain.CurrentFrame.Width)
            {
                layer.DrawTexture(mountain.CurrentFrame, new Point(x, 0));
            }

            return layer;
        }

        private ILayer CreateGroundLayer()
        {
            var ground = new GroundChunk();

            var layer = GameService.Instance.GameInstance.Renderer.CreateLayer(
                new Dimensions(_screenWidth, ground.CurrentFrame.Height));
            
            layer.CameraOffset = new Point(0, _screenHeight - ground.CurrentFrame.Height);
            layer.DrawLayerWithTransparency = true;
            layer.Clear();

            GameService.Instance.GameInstance.LayerManager.AddLayer(layer, -1);

            for (var x = 0; x < _screenWidth; x += ground.CurrentFrame.Width)
            {
                layer.DrawTexture(ground.CurrentFrame, new Point(x, 0));
            }

            return layer;
        }

        /// <summary>
        /// Add a sun in the top left of the screen
        /// </summary>
        private void CreateSun()
        {
            var sun = new Sun(_screenWidth - 8 - Sun.ChunkWidth, 8);
            sun.Layer = 0;
            AddSprite(sun);
        }

        /// <summary>
        /// Add some starting clouds so they are on the screen at the begining
        /// </summary>
        private void CreateInitialClouds()
        {
            var rand = GameService.Instance.Random;
            int yOffsetMin = _screenHeight - 16 - MountainChunk.ChunkHeight;

            for (var i = 0; i < NumberOfClouds; i++)
            {
                var xPos = rand.Between(0, _screenWidth);
                var yPos = rand.Next(0, yOffsetMin);
                var c = new Cloud(xPos, yPos);
                c.VelocityX = rand.Between(-4f, -2f);
                c.Layer = 6;
                _clouds.Add(c);
                AddSprite(c);
            }
        }

        /// <summary>
        /// Dynamically create and destroy clouds as they drift to the left
        /// </summary>
        private void DoClouds()
        {
            var rand = GameService.Instance.Random;
            int yOffsetMin = _screenHeight - 16 - MountainChunk.ChunkHeight;

            for (var i = _clouds.Count - 1; i > -1; i--)
            {
                var cloud = _clouds[i];
                var cloudRightEdge = cloud.X + cloud.CurrentFrame.Width;

                if (cloudRightEdge < 0)
                {
                    cloud.Die();
                    _clouds.Remove(cloud);
                }
            }

            // This is intentionally only done once per frame instead of
            // a while loop like the other methods
            if (_clouds.Count < NumberOfClouds)
            {
                var xPos = rand.Between(_screenWidth, _screenWidth + 8);
                var yPos = rand.Next(0, yOffsetMin);
                var c = new Cloud(xPos, yPos);
                c.VelocityX = rand.Between(-4f, -2f);
                c.Layer = 6;
                _clouds.Add(c);
                AddSprite(c);
            }
        }

        /// <summary>
        /// Cleans up the screen's resources
        /// </summary>
        public void Dispose()
        {
            var layerManager = GameService.Instance.GameInstance.LayerManager;
            layerManager.RemoveLayer(_skyLayer);
            layerManager.RemoveLayer(_groundLayer);
            layerManager.RemoveLayer(_treeLayer);
            layerManager.RemoveLayer(_mountainLayer);
        }
    }
}