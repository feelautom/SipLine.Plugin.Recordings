using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace SipLine.Plugin.Recordings.Models;

/// <summary>
/// Représente un enregistrement d'appel.
/// </summary>
public class RecordingEntry : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isPlaying;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Chemin complet du fichier.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// Nom du fichier (sans chemin).
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Date et heure de l'enregistrement.
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Date formatée pour affichage.
    /// </summary>
    public string DateDisplay => DateTime.ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Numéro de téléphone.
    /// </summary>
    public string PhoneNumber { get; set; } = "";

    /// <summary>
    /// Nom du contact (si trouvé).
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// Nom affiché (contact ou numéro).
    /// </summary>
    public string DisplayName => !string.IsNullOrEmpty(ContactName) ? ContactName : PhoneNumber;

    /// <summary>
    /// Initiale pour l'avatar.
    /// </summary>
    public string Initial => !string.IsNullOrEmpty(DisplayName) ? DisplayName[0].ToString().ToUpper() : "?";

    /// <summary>
    /// Direction de l'appel (entrant/sortant).
    /// </summary>
    public string Direction { get; set; } = "outgoing";

    /// <summary>
    /// Durée de l'enregistrement.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Durée formatée (mm:ss).
    /// </summary>
    public string DurationDisplay => Duration.ToString(@"mm\:ss");

    /// <summary>
    /// Taille du fichier en octets.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Taille formatée (KB/MB).
    /// </summary>
    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>
    /// Sélectionné pour action en lot.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// En cours de lecture.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
