using Microsoft.Extensions.Logging;
using SipLine.Plugin.Sdk;
using SipLine.Plugin.Sdk.Licensing;
using SipLine.Plugin.Recordings.Resources;

namespace SipLine.Plugin.Recordings;

/// <summary>
/// Plugin de gestion des enregistrements d'appels.
/// Permet de visualiser, lire, exporter et supprimer les enregistrements.
/// </summary>
public class RecordingsPlugin : ISipLinePlugin
{
    public string Id => "sipline.plugin.recordings";
    public string Name => "Enregistrements";
    public string Description => "Gestion des enregistrements d'appels";
    public Version Version => new(1, 0, 0);
    public string Author => "SipLine";
    public string? WebsiteUrl => null;

    // Plugin intégré - pas de licence requise
    public PluginLicenseType LicenseType => PluginLicenseType.Integrated;

    // Icône: FileAudio (Lucide)
    public string? IconPathData => "M17.5 22h.5a2 2 0 0 0 2-2V7l-5-5H6a2 2 0 0 0-2 2v3m0 12v-6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2zm0-2h8M14 2v5h5M2 17h.01M6 17h.01";

    public bool HasSettingsUI => true;

    private IPluginContext? _context;
    private RecordingsViewModel? _viewModel;
    private PluginSidebarTab? _tab;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _context.Logger.LogInformation("Plugin Enregistrements initialisé");

        // Enregistrer les ressources de traduction
        context.RegisterResource(Resources.Strings.ResourceManager);

        // Créer le ViewModel
        _viewModel = new RecordingsViewModel(context);

        // Créer l'onglet et le garder en mémoire pour les mises à jour
        _tab = new PluginSidebarTab
        {
            Id = "recordings-tab",
            Title = Resources.Strings.Title,
            Tooltip = Resources.Strings.Subtitle,
            IconPathData = IconPathData ?? "",
            Order = 200,
            ContentFactory = () => new RecordingsView { DataContext = _viewModel }
        };

        // Ajouter un onglet dans le menu principal
        _context.RegisterSidebarTab(_tab);

        // S'abonner au changement de langue pour mettre à jour le titre
        _context.OnLanguageChanged += (lang) =>
        {
            if (_tab != null)
            {
                // Mise à jour dynamique via INotifyPropertyChanged
                _tab.Title = Resources.Strings.Title;
                _tab.Tooltip = Resources.Strings.Subtitle;
            }
        };

        return Task.CompletedTask;
    }

    public object? GetSettingsUI()
    {
        // Retourne la vue WPF pour l'onglet Settings
        if (_viewModel == null) return null;
        return new RecordingsView { DataContext = _viewModel };
    }

    public Task ShutdownAsync()
    {
        _context?.UnregisterSidebarTab("recordings-tab");
        _context?.Logger.LogInformation("Plugin Enregistrements arrêté");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
    }
}
