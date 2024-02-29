﻿using CommunityToolkit.Mvvm.Input;
using OneWare.NetlistSvg.Services;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.NetlistSvg;

public class NetlistSvgModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<NetlistSvgService>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var netListSvgService = containerProvider.Resolve<NetlistSvgService>();
        
        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu(x =>
        {
            if (x is [IProjectFile {Extension: ".json"} json])
            {
                return new[]
                {
                    new MenuItemViewModel("NetlistSvg")
                    {
                        Header = "Convert to SVG",
                        Command = new AsyncRelayCommand(() => netListSvgService.ShowSchemeAsync(json))
                    }
                };
            }
            return null;
        });
    }
}