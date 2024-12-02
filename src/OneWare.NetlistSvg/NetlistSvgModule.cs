using CommunityToolkit.Mvvm.Input;
using OneWare.NetlistSvg.Services;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.OssCadSuiteIntegration.Yosys;
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
        var yosysService = containerProvider.Resolve<YosysService>();

        containerProvider.Resolve<IProjectExplorerService>().RegisterConstructContextMenu((x, l) =>
        {
            if (x is [IProjectFile { Extension: ".json" } json])
            {
                l.Add(new MenuItemViewModel("NetlistSvgFromJson")
                {
                    Header = "Convert to SVG",
                    Command = new AsyncRelayCommand(() => netListSvgService.ShowSchemeAsync(json))
                });
            }
            if (x is [IProjectFile { Extension: ".v" } verilog])
            {
                l.Add(new MenuItemViewModel("NetlistSvgFromVerilog")
                {
                    Header = "Convert to SVG",
                    Command = new AsyncRelayCommand(async () =>
                    {
                        await yosysService.CreateNetListJsonAsync(verilog);
                        var jsonFile = verilog.TopFolder!.AddFile(verilog.Name + ".json");
                        await netListSvgService.ShowSchemeAsync(jsonFile);
                    })
                });
            }
        });
    }
}