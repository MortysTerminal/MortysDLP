using MortysDLP.Services.Tools;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MortysDLP.Models
{
    /// <summary>Anzeigezustand einer Werkzeug-Zeile auf der Seite „Werkzeuge" — abgeleitet aus
    /// <see cref="ToolCheckOutcome"/>, aber bewusst ein eigener, kleinerer Begriff: Die Seite
    /// braucht nicht die volle Unterscheidung aus <see cref="ToolHealth"/>, nur die vier Fälle,
    /// die zu unterschiedlichen Aktionen führen.</summary>
    internal enum ToolRowState
    {
        /// <summary>Keine der Zieldateien vorhanden.</summary>
        Missing,

        /// <summary>Datei(en) vorhanden, aber nicht brauchbar (antwortet nicht oder als
        /// fremdes Programm) — entspricht „unvollständig" in der Aufgabenbeschreibung.</summary>
        Broken,

        /// <summary>Installiert und brauchbar, keine neuere Version bekannt.</summary>
        Ok,

        /// <summary>Installiert und brauchbar, eine neuere Version wird angeboten.</summary>
        UpdateAvailable,
    }

    /// <summary>
    /// Welche Aktionen in welchem <see cref="ToolRowState"/> erlaubt sind — reine Logik, ohne
    /// Oberfläche und ohne Werkzeug, damit sich die Bedingungen nicht in XAML-Sichtbarkeiten
    /// verstreuen und sich für sich allein testen lassen.
    /// </summary>
    /// <param name="CanRepair">Neu laden und ersetzen — bei <see cref="ToolRowState.Missing"/>
    /// zugleich die Installation, sonst die Reparatur einer beschädigten Datei. Immer erlaubt:
    /// „Reparieren" ist der einzige Weg, ein Werkzeug mit richtiger Versionsnummer, aber
    /// kaputter Datei, ohne manuelles Löschen wieder in Ordnung zu bringen.</param>
    /// <param name="CanUpdate">Nur wenn tatsächlich etwas Neueres bekannt ist — sonst gäbe es
    /// nichts anzubieten.</param>
    /// <param name="CanUninstall">Nur wenn überhaupt eine Datei da ist, die sich entfernen
    /// ließe.</param>
    /// <param name="CanOpenFolder">Der Werkzeugordner existiert unabhängig davon, ob das
    /// Werkzeug selbst installiert ist — deshalb immer erlaubt.</param>
    internal readonly record struct ToolRowActions(bool CanRepair, bool CanUpdate, bool CanUninstall, bool CanOpenFolder)
    {
        public static ToolRowActions For(ToolRowState state) => state switch
        {
            ToolRowState.Missing => new ToolRowActions(CanRepair: true, CanUpdate: false, CanUninstall: false, CanOpenFolder: true),
            ToolRowState.Broken => new ToolRowActions(CanRepair: true, CanUpdate: false, CanUninstall: true, CanOpenFolder: true),
            ToolRowState.Ok => new ToolRowActions(CanRepair: true, CanUpdate: false, CanUninstall: true, CanOpenFolder: true),
            ToolRowState.UpdateAvailable => new ToolRowActions(CanRepair: true, CanUpdate: true, CanUninstall: true, CanOpenFolder: true),
            _ => new ToolRowActions(false, false, false, false),
        };

        /// <summary>Leitet den Anzeigezustand aus einer Werkzeugprüfung ab. Reine Logik, kein
        /// Netzzugriff — <paramref name="outcome"/> muss bereits vorliegen.</summary>
        public static ToolRowState StateFor(ToolCheckOutcome outcome)
        {
            if (!outcome.Status.Installed)
                return ToolRowState.Missing;

            if (!outcome.Usable)
                return ToolRowState.Broken;

            return outcome.Verdict.Offer ? ToolRowState.UpdateAvailable : ToolRowState.Ok;
        }
    }

    /// <summary>
    /// Anzeigemodell einer Werkzeug-Zeile auf der Seite „Werkzeuge". Eine Instanz pro Werkzeug,
    /// über die gesamte Lebensdauer der Seite wiederverwendet — Eigenschaften ändern sich über
    /// <see cref="INotifyPropertyChanged"/>, damit sich eine Zeile während eines laufenden
    /// Downloads aktualisiert, ohne die Liste neu aufzubauen (<c>Items.Refresh()</c> baut die
    /// gesamte Ansicht neu auf und verliert dabei Auswahl und Scrollposition).
    /// </summary>
    internal sealed class ToolRowItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary><see cref="IManagedTool.Id"/> — zur Zuordnung zwischen Zeile und Werkzeug,
        /// nicht zur Anzeige.</summary>
        public string ToolId { get; }

        public ToolRowItem(string toolId) => ToolId = toolId;

        private string _displayName = "";
        public string DisplayName { get => _displayName; set => SetField(ref _displayName, value); }

        private bool _requiredForOperation;
        public bool RequiredForOperation { get => _requiredForOperation; set => SetField(ref _requiredForOperation, value); }

        private ToolRowState _state;
        public ToolRowState State { get => _state; set => SetField(ref _state, value); }

        private string _stateText = "";
        public string StateText { get => _stateText; set => SetField(ref _stateText, value); }

        private string _stateColor = "#94A3B8";
        public string StateColor { get => _stateColor; set => SetField(ref _stateColor, value); }

        private string _versionText = "";
        public string VersionText { get => _versionText; set => SetField(ref _versionText, value); }

        private string _locationText = "";
        public string LocationText { get => _locationText; set => SetField(ref _locationText, value); }

        private string _sizeText = "";
        public string SizeText { get => _sizeText; set => SetField(ref _sizeText, value); }

        private bool _canRepair;
        public bool CanRepair { get => _canRepair; set => SetField(ref _canRepair, value); }

        /// <summary>„Installieren" bei <see cref="ToolRowState.Missing"/>, sonst
        /// „Reparieren" — derselbe Aufruf (<see cref="IManagedTool.InstallAsync"/>) dahinter,
        /// nur die Beschriftung passt sich dem Zustand an.</summary>
        private string _repairButtonText = "";
        public string RepairButtonText { get => _repairButtonText; set => SetField(ref _repairButtonText, value); }

        private string _updateButtonText = "";
        public string UpdateButtonText { get => _updateButtonText; set => SetField(ref _updateButtonText, value); }

        private string _uninstallButtonText = "";
        public string UninstallButtonText { get => _uninstallButtonText; set => SetField(ref _uninstallButtonText, value); }

        private string _openFolderButtonText = "";
        public string OpenFolderButtonText { get => _openFolderButtonText; set => SetField(ref _openFolderButtonText, value); }

        // Eigene Sichtbarkeit statt nur IsEnabled: "Aktualisieren" soll ausdrücklich
        // verschwinden, wenn es nichts anzubieten gibt, nicht nur ausgegraut daliegen.
        private Visibility _updateVisibility = Visibility.Collapsed;
        public Visibility UpdateVisibility { get => _updateVisibility; set => SetField(ref _updateVisibility, value); }

        private bool _canUninstall;
        public bool CanUninstall { get => _canUninstall; set => SetField(ref _canUninstall, value); }

        private bool _canOpenFolder = true;
        public bool CanOpenFolder { get => _canOpenFolder; set => SetField(ref _canOpenFolder, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }

        private double _progressFraction;
        public double ProgressFraction { get => _progressFraction; set => SetField(ref _progressFraction, value); }

        private bool _progressVisible;
        public bool ProgressVisible { get => _progressVisible; set => SetField(ref _progressVisible, value); }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
