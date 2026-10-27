# Installation & First-time Setup

> 🇩🇪 Deutsche Version: [../de/installation.md](../de/installation.md)  
> ← [Overview](README.md) | [Usage Guide →](usage.md)

Setting up MortysDLP is straightforward. The app guides you through everything on first launch.

---

## Table of Contents

1. [Requirements](#1-requirements)
2. [Download the latest release](#2-download-the-latest-release)
3. [Extract & run](#3-extract--run)
4. [.NET 10 Runtime check](#4-net-10-runtime-check)
5. [Tool check (yt-dlp & ffmpeg)](#5-tool-check-yt-dlp--ffmpeg)
6. [Whisper setup (optional)](#6-whisper-setup-optional--for-transcription)
7. [You're ready](#7-youre-ready)

---

## 1. Requirements

| Requirement | Notes |
|---|---|
| Windows 10 or 11 | 64-bit |
| [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) | Prompted automatically if missing |

> **What is .NET 10?**  
> .NET is Microsoft's official, free runtime — the same kind of system component that countless Windows applications rely on. It is published, signed, and maintained by Microsoft and can be downloaded from [dotnet.microsoft.com](https://dotnet.microsoft.com/). It contains no MortysDLP-specific code; it is simply the engine that runs the app.  
> Installing it is a one-time step and requires no developer knowledge.  
> When prompted, make sure to install the **".NET Desktop Runtime"** — not the SDK and not ASP.NET Core Runtime.

---

## 2. Download the latest release

Grab the latest ZIP from the [Releases page](https://github.com/MortysTerminal/MortysDLP/releases).

---

## 3. Extract & run

### First: unblock the ZIP file (important)

Windows remembers that a file came from the internet and passes this mark on to every file you
extract from it. Windows only runs marked programs with restrictions — **MortysDLP may then not
start at all, or close without any message.** One tick before extracting fixes this:

1. Open your **Downloads** folder in File Explorer.
2. **Right-click** `MortysDLP.zip` and choose **Properties**.
3. At the bottom of the **General** tab, next to **Security**, you see:
   *"This file came from another computer and might be blocked to help protect this computer."*
   Tick **Unblock**.
4. Click **OK**.
5. Only **now** extract the ZIP file (right-click → **Extract All…**).

If you don't see the **Security** line, the file is already unblocked — just carry on.

> **Prefer a command?** Open PowerShell (Start menu → type "PowerShell") and enter:
> ```powershell
> Unblock-File "$HOME\Downloads\MortysDLP.zip"
> ```
> This does exactly the same as ticking **Unblock**.

### Extract and run

Unzip the archive into any folder you like, for example:

```
C:\Tools\MortysDLP\
```

No installer, no admin rights required. Then double-click **`MortysDLP.exe`**.

On first launch, the app shows a short preparation screen (*"Preparing MortysDLP…"*) and runs a few checks before opening the main window.

### "Windows protected your PC"

On the first start, a blue **Microsoft Defender SmartScreen** window may appear. Windows shows it
for programs it doesn't know yet — MortysDLP is a free open-source project without a paid
publisher certificate. To start it anyway:

1. Click **More info** in the blue window.
2. Click **Run anyway**.

You only need to do this once. Always download MortysDLP from the official
[Releases page](https://github.com/MortysTerminal/MortysDLP/releases) — that way you know where
the file comes from.

### MortysDLP doesn't start?

Usually the ZIP file was extracted **before** it was unblocked. To fix it:

1. Delete the extracted MortysDLP folder. Your settings, download history and downloaded tools
   are stored elsewhere and are kept.
2. Unblock the ZIP file as described above under *"First: unblock the ZIP file"*.
3. Extract it again and start `MortysDLP.exe`.

> **Already set up and want to keep the folder?** You can unblock all files in it at once. In
> PowerShell (adjust the path to your folder):
> ```powershell
> Get-ChildItem "C:\Tools\MortysDLP" -Recurse | Unblock-File
> ```

Updates that MortysDLP downloads by itself later don't need this step — it only affects the first
file from your browser.

---

## 4. .NET 10 Runtime check

If .NET 10 is already installed on your system, you'll move straight to the next step.

If not, Windows will prompt you to install it. After installing, restart `MortysDLP.exe`.

---

## 5. Tool check (yt-dlp & ffmpeg)

MortysDLP checks whether **yt-dlp** and **ffmpeg** are present in the application folder. On a fresh install they won't be, and the app will ask:

> *"yt-dlp is required to download videos and audio from various platforms. […] Would you like to download yt-dlp now?"*

Click **Yes**. MortysDLP fetches the official current versions automatically and stores them inside its own directory. Nothing is installed system-wide; nothing is touched outside the MortysDLP folder.

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — the actual downloader, supporting hundreds of platforms
- [ffmpeg](https://ffmpeg.org/) — handles audio/video processing and conversion

From here on, MortysDLP also keeps yt-dlp updated for you in the background.

> **What if I decline?**  
> yt-dlp is required for the app to work. If you decline, MortysDLP will remind you that the tool is mandatory and offer to install it again on the next launch. You can also place the tools manually in the application folder — see the [yt-dlp documentation](https://github.com/yt-dlp/yt-dlp) for details.

---

## 6. Whisper setup *(optional — for transcription)*

If you want to use the built-in **Transcribe** feature, a two-step setup is required the first time you open that tab. All other features (Download, Convert) work without it.

### Step 1: Install Whisper

In the **Transcribe** tab you will see:

> *"Step 1: Install Whisper"*

Click **Install Whisper**. MortysDLP downloads and installs [whisper.cpp](https://github.com/ggml-org/whisper.cpp) into its own application folder. No Python, no virtual environments, no command line — the app handles everything.

> **What is Whisper?**  
> Whisper is an open-source speech-recognition engine originally released by OpenAI. MortysDLP runs it **entirely on your local machine** — no audio is sent to any cloud service, no API key is needed, and there are no usage costs.

### Step 2: Download a language model

After Whisper is installed, click **Manage Whisper** to open the model manager. The available models are listed with their approximate file size:

| Model | Size | Notes |
|---|---|---|
| Tiny | ~75 MB | Fastest, lowest accuracy |
| Base | ~142 MB | Good starting point ✔ |
| Small | ~466 MB | Better accuracy |
| Medium | ~1.5 GB | High accuracy |
| Large-v3-Turbo | ~1.6 GB | Fast & highly accurate |
| Large-v3 | ~3.1 GB | Highest accuracy |

Click **Download** next to the model you want. Once it shows **✔ Installed**, transcription is ready to use. You can add or remove models at any time.

---

## 7. You're ready

All set — every feature is now available:

- **Download tab** — paste a URL and start downloading
- **Convert tab** — convert local media files
- **Transcribe tab** — transcribe video or audio to text
- **Settings tab** — configure paths and preferences

If anything goes wrong during setup, please open an [Issue](https://github.com/MortysTerminal/MortysDLP/issues) and include as much detail as possible.

---

➡️ Continue with the [Usage Guide](usage.md)
