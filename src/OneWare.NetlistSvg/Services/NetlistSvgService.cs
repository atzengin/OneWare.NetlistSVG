using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Esprima;
using Esprima.Ast;
using Jint;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Color = Avalonia.Media.Color;

namespace OneWare.NetlistSvg.Services;

public class NetlistSvgService
{
    private readonly ILogger _logger;
    private readonly IApplicationStateService _applicationStateService;
    private readonly IDockService _dockService;

    private Prepared<Script>? _script1;
    private Prepared<Script>? _script2;
    
    public NetlistSvgService(ILogger logger, IApplicationStateService active, IDockService dockService)
    {
        _logger = logger;
        _applicationStateService = active;
        _dockService = dockService;

        _ = LoadScriptsAsync();
    }

    private async Task LoadScriptsAsync()
    {
        var script1 = GetAvaloniaAsset("avares://OneWare.NetlistSvg/Assets/elk.bundled.js");
        var script2 = GetAvaloniaAsset("avares://OneWare.NetlistSvg/Assets/netlistsvg.bundle.js");

        var result = await Task.Run(() =>
        {
            var c1 = Engine.PrepareScript(script1);
            var c2 = Engine.PrepareScript(script2);
            
            return (c1, c2);
        });

        _script1 = result.c1;
        _script2 = result.c2;
    }

    public async Task ShowSchemeAsync(IProjectFile jsonFile)
    {
        var svgStr = await CreateFromJsonAsync(jsonFile);
        
        if (!string.IsNullOrEmpty(svgStr))
        {
           
            var newFileName = Path.GetFileNameWithoutExtension(jsonFile.FullPath) + ".svg";
            var newFile = jsonFile.TopFolder!.AddFile(newFileName, true);
            await File.WriteAllTextAsync(newFile.FullPath, svgStr);
            await _dockService.OpenFileAsync(newFile);
        }
    }

    private string _output = string.Empty;
    
    public async Task<string?> CreateFromJsonAsync(IProjectFile jsonFile)
    {
        if (_script1 is null || _script2 is null)
        {
            _logger.Error("Scripts not loaded");
            return null;
        }

        _output = string.Empty;

        var cancel = new CancellationTokenSource();
        var state = _applicationStateService.AddState("Rendering Scheme...", AppState.Loading,  () => cancel.Cancel());

        var theme = Application.Current!.ActualThemeVariant;
        var skin = LoadSkin(theme);
        var backgroundHex = Application.Current!.FindResource(theme, "ThemeControlLowColor") is Color background ? 
            $"#{background.R:X2}{background.G:X2}{background.B:X2}" : "#FFFFFF";
        
        try
        {
            await Task.Run(() =>
            {
                using var engine = new Engine(new Options()
                {
                    Strict = false
                });
                
                var console = new
                {
                    log = new Action<object>(x =>
                    {
                        _output += x;
                    }),
                    warn = new Action<object>(x => _logger.Warning(x.ToString() ?? "")),
                    error = new Action<object>(x => _logger.Error(x.ToString() ?? ""))
                };
                engine.SetValue("console", console);
                engine.Execute("const window = {};");
                engine.Execute("window.Math = Math");
                engine.Execute("window.Array = Array");
                engine.SetValue("window.Array", engine.GetValue("Array"));
                engine.SetValue("window.Date", new Func<string, object>((input) => DateTime.Parse(input)));
                engine.Execute("const exports = {}");
                engine.SetValue("setTimeout", new Action<Action, int>((action, delay) =>
                {
                    if (delay <= 0)
                    {
                        action();
                        return;
                    }
                    var timer = new System.Timers.Timer(delay);
                    timer.Elapsed += (sender, args) =>
                    {
                        action();
                        timer.Stop();
                    };
                    timer.Start();
                }));
                
                engine.Execute(_script1.Value);
                engine.Execute(_script2.Value);
            
                engine.Execute("var netlistsvg = window.netlistsvg");
                
                var json = File.ReadAllText(jsonFile.FullPath);
                
                engine.SetValue("OneWareJsonData", json);
                engine.Execute("var OneWareNetList = JSON.parse(OneWareJsonData)");
                
                engine.SetValue("OneWareSkin", skin);
                engine.Execute(
                    "netlistsvg.render(OneWareSkin, OneWareNetList, (err, result) => console.log(result));");

                if (theme != ThemeVariant.Light)
                {
                    _output = _output.Replace("fill: white; stroke: none", $"fill: {backgroundHex}; stroke: none");
                    _output = _output.Replace("fill:#000", $"fill:#FFF");
                }
            }, cancel.Token);
        }  
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return null;
        }

        _applicationStateService.RemoveState(state);
        return cancel.IsCancellationRequested ? null : _output;
    }

    private string GetAvaloniaAsset(string resource)
    {
        using var stream = AssetLoader.Open(new Uri(resource));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string LoadSkin(ThemeVariant themeVariant)
    {
        if (themeVariant == ThemeVariant.Light)
        {
            return GetAvaloniaAsset("avares://OneWare.NetlistSvg/Assets/theme_light.svg");
        }
        else
        {
            return GetAvaloniaAsset("avares://OneWare.NetlistSvg/Assets/theme_dark.svg");
        }
    }
}