﻿using Glade2d;
using Glade2d.Graphics;
using Glade2d.Graphics.SelfRenderer;
using Glade2d.Input;
using Glade2d.Profiling;
using Glade2d.Services;
using GladePlatformer.Shared;
using Meadow;
using Meadow.Devices;
using Meadow.Foundation.Graphics;


namespace GladePlatformer.ProjectLab;

public class MeadowApp : App<F7FeatherV2>
{
    private IGraphicsDisplay _display = default!;
    private IProjectLabHardware _projectLab = default!;
    
    public override Task Initialize()
    {
        _projectLab = Meadow.Devices.ProjectLab.Create();
        _display = _projectLab.Display!;
        
        return base.Initialize();
    }
    
    public override Task Run()
    {
        LogService.Log.Trace("Initializing Glade game engine...");
        var textureManager = new TextureManager(MeadowOS.FileSystem.UserFileSystemRoot);
        var layerManager = new LayerManager();
        var profiler = new Profiler();
        var renderer = new GladeSelfRenderer(_display, textureManager, layerManager, profiler, 2);
        
        var glade = new Game();
        glade.Initialize(renderer, textureManager, layerManager, profiler);
        InitializeInput(glade.InputManager);

        GladePlatformerGame.Run(glade);

        return base.Run();
    }

    private void InitializeInput(InputManager inputManager)
    {
        inputManager.RegisterPushButton(_projectLab.UpButton!, nameof(GameInputs.Jump));
        inputManager.RegisterPushButton(_projectLab.LeftButton!, nameof(GameInputs.Left));
        inputManager.RegisterPushButton(_projectLab.RightButton!, nameof(GameInputs.Right));
    }
}
