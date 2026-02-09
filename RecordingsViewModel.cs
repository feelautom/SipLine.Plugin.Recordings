using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SipLine.Plugin.Recordings.Models;
using SipLine.Plugin.Sdk;

namespace SipLine.Plugin.Recordings;

/// <summary>
/// ViewModel pour la gestion des enregistrements.
/// </summary>
public class RecordingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IPluginContext _context;
    private readonly string _recordingsPath;
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;
    private RecordingEntry? _currentlyPlaying;
    private string _searchQuery = "";
    private string _filterPeriod = "all";
    private bool _isLoading;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RecordingEntry> Recordings { get; } = new();
    public ObservableCollection<RecordingEntry> FilteredRecordings { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    public string FilterPeriod
    {
        get => _filterPeriod;
        set
        {
            if (_filterPeriod != value)
            {
                _filterPeriod = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public int TotalCount => Recordings.Count;
    public string TotalSize => FormatSize(Recordings.Sum(r => r.FileSize));

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand EmailCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand SetFilterCommand { get; }

    // Indexeur pour la localisation (Binding [Key])
    public string this[string key] => _context.GetLocalizedString(key);

    public RecordingsViewModel(IPluginContext context)
    {
        _context = context;
        _recordingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SipLine", "Recordings");

        // Rafraîchir les textes lors du changement de langue
        _context.OnLanguageChanged += (lang) =>
        {
            // Notifier que toutes les propriétés (Item[]) ont changé
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(System.Windows.Data.Binding.IndexerName));
        };

        // Initialiser les commandes
        RefreshCommand = new RelayCommand(LoadRecordings);
        PlayCommand = new RelayCommand<RecordingEntry>(Play);
        StopCommand = new RelayCommand(Stop);
        DeleteCommand = new RelayCommand<RecordingEntry>(Delete);
        ExportCommand = new RelayCommand<RecordingEntry>(Export);
        EmailCommand = new RelayCommand<RecordingEntry>(SendEmail);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        SetFilterCommand = new RelayCommand<string>(period => FilterPeriod = period ?? "all");

        // Charger les enregistrements
        LoadRecordings();
    }

    private void LoadRecordings()
    {
        IsLoading = true;
        Recordings.Clear();

        try
        {
            if (!Directory.Exists(_recordingsPath))
            {
                Directory.CreateDirectory(_recordingsPath);
                _context.Logger.LogInformation("Dossier d'enregistrements créé: {Path}", _recordingsPath);
            }

            // Recherche récursive dans tous les sous-dossiers
            var files = Directory.GetFiles(_recordingsPath, "*.mp3", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_recordingsPath, "*.wav", SearchOption.AllDirectories))
                .OrderByDescending(f => File.GetCreationTime(f));

            foreach (var file in files)
            {
                var entry = ParseRecordingFile(file);
                if (entry != null)
                {
                    Recordings.Add(entry);
                }
            }

            _context.Logger.LogInformation("Chargé {Count} enregistrements", Recordings.Count);
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur lors du chargement des enregistrements");
            _context.ShowSnackbar($"Erreur: {ex.Message}", SnackbarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
            ApplyFilters();
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalSize));
        }
    }

    private RecordingEntry? ParseRecordingFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Formats supportés:
            // - Nouveau: yyyy-MM-dd_HH-mm-ss_+33612345678.mp3
            // - Ancien: call_yyyy-MM-dd_HH-mm-ss.mp3 (numéro dans le dossier parent)
            var parts = fileName.Split('_');

            DateTime dateTime = fileInfo.CreationTime;
            string phoneNumber = "";

            // Format nouveau: yyyy-MM-dd_HH-mm-ss_phoneNumber
            if (parts.Length >= 3 && !parts[0].Equals("call", StringComparison.OrdinalIgnoreCase))
            {
                // Essayer de parser la date (parts[0] = date, parts[1] = heure)
                if (DateTime.TryParseExact(
                    $"{parts[0]}_{parts[1]}",
                    "yyyy-MM-dd_HH-mm-ss",
                    null,
                    System.Globalization.DateTimeStyles.None,
                    out var parsedDate))
                {
                    dateTime = parsedDate;
                }

                // Le numéro est le reste (parts[2] et suivants)
                phoneNumber = string.Join("_", parts.Skip(2));
            }
            // Format ancien: call_yyyy-MM-dd_HH-mm-ss (numéro dans le dossier parent)
            else if (parts.Length >= 3 && parts[0].Equals("call", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParseExact(
                    $"{parts[1]}_{parts[2]}",
                    "yyyy-MM-dd_HH-mm-ss",
                    null,
                    System.Globalization.DateTimeStyles.None,
                    out var parsedDate))
                {
                    dateTime = parsedDate;
                }

                // Récupérer le numéro depuis le dossier parent
                var parentDir = Path.GetFileName(Path.GetDirectoryName(filePath) ?? "");
                phoneNumber = parentDir;
            }

            // Obtenir la durée du fichier audio
            TimeSpan duration = TimeSpan.Zero;
            try
            {
                using var reader = new AudioFileReader(filePath);
                duration = reader.TotalTime;
            }
            catch
            {
                // Ignorer les erreurs de lecture de durée
            }

            return new RecordingEntry
            {
                FilePath = filePath,
                DateTime = dateTime,
                PhoneNumber = phoneNumber,
                Duration = duration,
                FileSize = fileInfo.Length,
                Direction = fileName.Contains("_in_") ? "incoming" : "outgoing"
            };
        }
        catch (Exception ex)
        {
            _context.Logger.LogWarning(ex, "Impossible de parser le fichier: {File}", filePath);
            return null;
        }
    }

    private void ApplyFilters()
    {
        FilteredRecordings.Clear();

        var filtered = Recordings.AsEnumerable();

        // Filtre par période
        var now = DateTime.Now;
        filtered = _filterPeriod switch
        {
            "today" => filtered.Where(r => r.DateTime.Date == now.Date),
            "week" => filtered.Where(r => r.DateTime >= now.AddDays(-7)),
            "month" => filtered.Where(r => r.DateTime >= now.AddMonths(-1)),
            _ => filtered
        };

        // Filtre par recherche
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var query = _searchQuery.ToLower();
            filtered = filtered.Where(r =>
                r.DisplayName.ToLower().Contains(query) ||
                r.PhoneNumber.ToLower().Contains(query));
        }

        foreach (var item in filtered)
        {
            FilteredRecordings.Add(item);
        }
    }

    private void Play(RecordingEntry? entry)
    {
        if (entry == null) return;

        _context.Logger.LogDebug("Play demandé pour: {File}", entry.FilePath);

        // Vérifier que le fichier existe
        if (!File.Exists(entry.FilePath))
        {
            _context.Logger.LogWarning("Fichier introuvable: {File}", entry.FilePath);
            _context.ShowSnackbar("Fichier introuvable", SnackbarSeverity.Error);
            return;
        }

        // Arrêter la lecture en cours
        Stop();

        try
        {
            _audioReader = new AudioFileReader(entry.FilePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.PlaybackStopped += (s, e) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    entry.IsPlaying = false;
                    _currentlyPlaying = null;
                });
            };

            _waveOut.Play();
            entry.IsPlaying = true;
            _currentlyPlaying = entry;

            _context.Logger.LogInformation("Lecture démarrée: {File}", entry.FilePath);
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur de lecture: {File}", entry.FilePath);
            _context.ShowSnackbar($"Impossible de lire: {ex.Message}", SnackbarSeverity.Error);

            // Nettoyer en cas d'erreur
            _audioReader?.Dispose();
            _audioReader = null;
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }

    private void Stop()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _audioReader?.Dispose();
        _audioReader = null;

        if (_currentlyPlaying != null)
        {
            _currentlyPlaying.IsPlaying = false;
            _currentlyPlaying = null;
        }
    }

    private void Delete(RecordingEntry? entry)
    {
        if (entry == null) return;

        try
        {
            // Arrêter si en cours de lecture
            if (entry.IsPlaying) Stop();

            File.Delete(entry.FilePath);
            Recordings.Remove(entry);
            FilteredRecordings.Remove(entry);

            _context.ShowSnackbar("Enregistrement supprimé", SnackbarSeverity.Success);
            _context.Logger.LogInformation("Supprimé: {File}", entry.FileName);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(TotalSize));
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur de suppression: {File}", entry.FilePath);
            _context.ShowSnackbar($"Impossible de supprimer: {ex.Message}", SnackbarSeverity.Error);
        }
    }

    private void Export(RecordingEntry? entry)
    {
        if (entry == null) return;

        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = entry.FileName,
                DefaultExt = Path.GetExtension(entry.FilePath),
                Filter = "Fichiers audio|*.mp3;*.wav|Tous les fichiers|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                File.Copy(entry.FilePath, dialog.FileName, true);
                _context.ShowSnackbar("Enregistrement exporté", SnackbarSeverity.Success);
                _context.Logger.LogInformation("Exporté: {Source} → {Dest}", entry.FileName, dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur d'export: {File}", entry.FilePath);
            _context.ShowSnackbar($"Impossible d'exporter: {ex.Message}", SnackbarSeverity.Error);
        }
    }

    private void SendEmail(RecordingEntry? entry)
    {
        if (entry == null) return;

        try
        {
            // Utiliser MAPI pour ouvrir le client mail avec pièce jointe
            var subject = $"Enregistrement appel {entry.DisplayName}";
            var body = $"Bonjour,\n\nVeuillez trouver ci-joint l'enregistrement de l'appel du {entry.DateDisplay}.\n\nCordialement";

            // Essayer d'abord avec Outlook via COM (plus fiable)
            if (TrySendWithOutlook(entry.FilePath, subject, body))
            {
                _context.Logger.LogInformation("Email ouvert avec Outlook: {File}", entry.FileName);
                return;
            }

            // Fallback: utiliser le protocole mailto avec instruction pour l'utilisateur
            var encodedSubject = Uri.EscapeDataString(subject);
            var encodedBody = Uri.EscapeDataString(body + "\n\n[Pièce jointe à ajouter manuellement]");
            var mailto = $"mailto:?subject={encodedSubject}&body={encodedBody}";

            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });

            // Ouvrir aussi l'explorateur avec le fichier sélectionné
            Process.Start("explorer.exe", $"/select,\"{entry.FilePath}\"");

            _context.ShowSnackbar("Email ouvert. Glissez le fichier sélectionné en pièce jointe.", SnackbarSeverity.Info);
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur email: {File}", entry.FilePath);
            _context.ShowSnackbar($"Erreur: {ex.Message}", SnackbarSeverity.Error);
        }
    }

    /// <summary>
    /// Essaie d'ouvrir Outlook avec le fichier en pièce jointe via COM.
    /// </summary>
    private bool TrySendWithOutlook(string filePath, string subject, string body)
    {
        try
        {
            // Créer l'instance Outlook via COM
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType == null) return false;

            dynamic outlook = Activator.CreateInstance(outlookType)!;
            dynamic mail = outlook.CreateItem(0); // olMailItem = 0

            mail.Subject = subject;
            mail.Body = body;
            mail.Attachments.Add(filePath);
            mail.Display(); // Afficher sans envoyer

            return true;
        }
        catch
        {
            // Outlook non installé ou erreur COM
            return false;
        }
    }

    private void OpenFolder()
    {
        try
        {
            if (!Directory.Exists(_recordingsPath))
            {
                Directory.CreateDirectory(_recordingsPath);
            }
            Process.Start("explorer.exe", _recordingsPath);
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, "Erreur ouverture dossier");
            _context.ShowSnackbar($"Erreur: {ex.Message}", SnackbarSeverity.Error);
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Commande simple sans paramètre.
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Commande avec paramètre.
/// </summary>
internal class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}
