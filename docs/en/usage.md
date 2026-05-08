# Usage Guide

> 🇩🇪 Deutsche Version: [../de/benutzung.md](../de/benutzung.md)  
> ← [Installation](installation.md) | [Transcription →](transcription.md)

---

## Table of Contents

- [Download tab](#download-tab)
- [Convert tab](#convert-tab)
- [Transcribe tab](#transcribe-tab)
- [Settings tab](#settings-tab)
- [Updates](#updates)

---

## Download tab

The **Download** tab is the main feature of MortysDLP.

### Basic workflow

1. Paste the video or audio URL into the **Enter URL:** field
2. Configure the options below (see [Options](#options))
3. Click **Start Download** — progress and status are shown in real time
4. After the download completes, the file is saved to your configured download path

### Options

| Option | Description |
|---|---|
| **Download Path** / **Audio-Only Path** | Where downloaded files are saved. Set in **Settings** or via **Change Download Path**. |
| **Audio ONLY** | Downloads only the audio stream. Enables the **Bitrate** selector. |
| **Bitrate** | Audio quality when *Audio ONLY* is active: Highest, 320k, 256k, 192k, 160k, 128k, 96k, 64k |
| **Video Quality** | Target resolution: Best, 1440p, 1080p, 720p, 480p, 360p, 240p, 144p |
| **Video Format** | Output container: `mp4`, `mkv`, `mov`, `avi` |
| **Video format for editing software (x264)** | Re-encodes the video to H.264 — use this for best compatibility with DaVinci Resolve, Premiere Pro, etc. Cannot be combined with *Audio ONLY*. |
| **Time span from … to …** | Downloads only a section of the video. Format: `hh:mm:ss` or `mm:ss`. Use the **Timeline** button for a visual picker. |
| **From start to:** | Download only the first N **seconds** of a video. |
| **Use custom video title** | Overrides the output filename. Invalid characters are cleaned automatically. |
| **Transcription after download** | Automatically transcribes the downloaded file with Whisper. Requires Whisper and at least one model to be installed. |

### History

Click **History** to open the download history. Select any previous entry and click **Download Again** to reuse a URL with its original settings. Use **Clear History** to delete all entries.

### Playlist detection

When you paste a YouTube playlist URL, MortysDLP will ask whether you want to download the **entire playlist** or just the **single video**. Progress is tracked per video (*"Video 1/12"* etc.).

### Canceling a download

Click **Cancel Download** at any time. The current download will stop and the status will show *"Canceled"*.

### Save Settings

Click **Save Settings** to persist the current option selections as defaults. A confirmation dialog will appear.

---

## Convert tab

The **Convert** tab lets you convert local media files to a different format using ffmpeg.

### Workflow

1. Click **Add Files** and select one or more media files (mp4, mkv, mov, avi, mp3, aac, wav, flac, opus, …)
2. Set the **Target Format** (e.g. `mp4`, `mp3`, `wav`, …)
3. Set the **Target Folder** — or use the **Download Path** / **Audio-Only Path** quick buttons, or **Browse…**
4. Optionally adjust **Video Quality** and **Audio Quality** (default: `Original (lossless)`)
5. Click **Start Conversion** — each file shows its own progress bar and status

### File list management

| Button | Action |
|---|---|
| **Add Files** | Add media files to the list |
| **Remove** | Remove the selected file from the list |
| **Clear List** | Remove all files from the list |

### After conversion

- Each file shows status: *Converting…* → *Finished* / *Error* / *Canceled* / *Already converted*
- Click **Open Folder** to open the target folder in Windows Explorer
- Click **Cancel Conversion** to stop all running conversions

> **Note:** Files that are already in the target format may be reported as *"Already converted"* and skipped.

---

## Transcribe tab

See the dedicated [Transcription guide](transcription.md) for full details.

**Quick steps:**

1. Open **Manage Whisper** — install the Whisper engine and download at least one model
2. Select your input file via **Browse…** (next to **Select file:**)
3. Choose the **Whisper Model**, **Language**, and output format(s) (`.txt`, `.srt`, `.vtt`)
4. Set the **Output folder:** or use **Browse…**
5. Click **Start Transcription** — progress is shown in real time
6. When done, click **Open Folder** to access the output files

---

## Settings tab

| Setting | Description |
|---|---|
| **Change Download Path** | Opens a dialog to set the default download folder and an optional separate **Audio-Only Path** |
| **Select Language** | Switch between *Automatic (System Language)*, *Deutsch*, and *English*. Saved automatically. |
| **Enable Debug Mode (shows download details)** | Shows raw yt-dlp / ffmpeg output in a debug panel on each tab |
| **Open GitHub** | Opens the MortysDLP GitHub page in your browser |
| **Close Application** | Exits MortysDLP |

---

## Updates

When a new version of MortysDLP is available, a banner appears at the top of the main window:

> *"New version X.Y available — Click for details & update"*

Clicking the banner opens a changelog window showing what's new. From there you can choose:

- **Update now** — downloads and applies the update automatically
- **Not now** — closes the dialog; the banner can be dismissed with the **Dismiss** button

The update process runs in the background. MortysDLP will restart automatically after the update.

---

← [Installation](installation.md) | [Transcription →](transcription.md)
