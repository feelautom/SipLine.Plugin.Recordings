using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SipLine.Plugin.Sdk;

namespace SipLine.Plugin.Recordings;

public class RecordingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IPluginContext _context;
    private readonly string _recordingsPath;
    private FileSystemWatcher? _watcher;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<RecordingModel> Recordings { get; } = new();

    private RecordingModel? _selectedRecording;
    public RecordingModel? SelectedRecording
    {
        get => _selectedRecording;
        set
        {
            if (_selectedRecording != value)
            {
                _selectedRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    public bool CanPlay => SelectedRecording != null;
    public bool CanDelete => SelectedRecording != null;

    public ICommand RefreshCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public RecordingsViewModel(IPluginContext context)
    {
        _context = context;
        _recordingsPath = Path.Combine(context.PluginDataPath, "calls");
        Directory.CreateDirectory(_recordingsPath);

        RefreshCommand = new RelayCommand(LoadRecordings);
        PlayCommand = new RelayCommand(PlaySelected);
        DeleteCommand = new RelayCommand(DeleteSelected);
        OpenFolderCommand = new RelayCommand(OpenFolder);

        SetupWatcher();
        LoadRecordings();
    }

    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher(_recordingsPath, "*.wav");
        _watcher.Created += (s, e) => Application.Current.Dispatcher.Invoke(LoadRecordings);
        _watcher.Deleted += (s, e) => Application.Current.Dispatcher.Invoke(LoadRecordings);
        _watcher.EnableRaisingEvents = true;
    }

    private void LoadRecordings()
    {
        Recordings.Clear();
        if (!Directory.Exists(_recordingsPath)) return;

        var files = Directory.GetFiles(_recordingsPath, "*.wav")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime);

        foreach (var file in files)
        {
            Recordings.Add(new RecordingModel { FileName = file.Name, FilePath = file.FullName, Date = file.CreationTime, Size = file.Length });
        }
    }

    private void PlaySelected()
    {
        if (SelectedRecording == null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = SelectedRecording.FilePath, UseShellExecute = true }); }
        catch (Exception ex) { _context.ShowSnackbar($"Erreur lecture: {ex.Message}", SnackbarSeverity.Error); }
    }

    private void DeleteSelected()
    {
        if (SelectedRecording == null) return;
        if (MessageBox.Show($"Supprimer {SelectedRecording.FileName} ?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try { if (File.Exists(SelectedRecording.FilePath)) File.Delete(SelectedRecording.FilePath); LoadRecordings(); }
            catch (Exception ex) { _context.ShowSnackbar($"Erreur suppression: {ex.Message}", SnackbarSeverity.Error); }
        }
    }

    private void OpenFolder()
    {
        try { System.Diagnostics.Process.Start("explorer.exe", _recordingsPath); }
        catch (Exception ex) { _context.ShowSnackbar($"Erreur dossier: {ex.Message}", SnackbarSeverity.Error); }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    public void Dispose() => _watcher?.Dispose();
}

public class RecordingModel
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime Date { get; set; }
    public long Size { get; set; }
    public string SizeFormatted => $"{(Size / 1024.0 / 1024.0):N2} Mo";
}

internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}

internal class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    public RelayCommand(Action<T?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}