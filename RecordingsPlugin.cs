using Microsoft.Extensions.Logging;
using SipLine.Plugin.Sdk;
using SipLine.Plugin.Sdk.Licensing;
using SipLine.Plugin.Sdk.Enums;
using SipLine.Plugin.Recordings.Resources;

namespace SipLine.Plugin.Recordings;

public class RecordingsPlugin : ISipLinePlugin
{
    public string Id => ""sipline.plugin.recordings"";
    public string Name => ""Enregistrements"";
    public string Description => ""Gestion des enregistrements d'appels"";
    public Version Version => new(1, 0, 0);
    public string Author => ""SipLine"";
    public string? WebsiteUrl => null;

    public PluginLicenseType LicenseType => PluginLicenseType.Integrated;

    public PluginIcon? Icon => PluginIcon.Database;
    public string? IconPathData => null;

    public bool HasSettingsUI => true;

    private IPluginContext? _context;
    private RecordingsViewModel? _viewModel;
    private PluginSidebarTab? _tab;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _context.Logger.LogInformation(""Plugin Enregistrements initialisé"");

        _viewModel = new RecordingsViewModel(context);

        _tab = new PluginSidebarTab
        {
            Id = ""recordings-tab"",
            Title = context.Localization.GetString(""Title""),
            Tooltip = context.Localization.GetString(""Subtitle""),
            Icon = PluginIcon.Database,
            Order = 200,
            ContentFactory = () => new RecordingsView { DataContext = _viewModel }
        };

        _context.RegisterSidebarTab(_tab);

        _context.OnLanguageChanged += (lang) =>
        {
            if (_tab != null)
            {
                _tab.Title = context.Localization.GetString(""Title"");
                _tab.Tooltip = context.Localization.GetString(""Subtitle"");
            }
        };

        return Task.CompletedTask;
    }

    public object? GetSettingsUI()
    {
        if (_viewModel == null) return null;
        return new RecordingsView { DataContext = _viewModel };
    }

    public Task ShutdownAsync()
    {
        _context?.UnregisterSidebarTab(""recordings-tab"");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
    }
}
