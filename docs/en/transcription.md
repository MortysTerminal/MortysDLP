# Transcription

> 🇩🇪 Deutsche Version: [../de/transkription.md](../de/transkription.md)  
> ← [Usage Guide](usage.md) | [Overview →](README.md)

MortysDLP includes a fully **offline** speech-to-text feature powered by [whisper.cpp](https://github.com/ggerganov/whisper.cpp) — an optimized local implementation of OpenAI's Whisper model. No audio is ever sent to any cloud service. No API key, no subscription, no internet connection required during transcription.

---

## Table of Contents

- [What is transcription?](#what-is-transcription)
- [Setup](#setup)
- [Using the Transcribe tab](#using-the-transcribe-tab)
- [Output formats](#output-formats)
- [Language models](#language-models)
- [Managing Whisper](#managing-whisper)
- [Transcription after download](#transcription-after-download)

---

## What is transcription?

Transcription converts spoken language in a video or audio file into text. MortysDLP does this using **Whisper** — an open-source AI model originally released by OpenAI. The version used here (`whisper.cpp`) runs entirely on your CPU/GPU, locally on your PC.

**Supported input formats:** `mp4`, `mkv`, `mov`, `avi`, `mp3`, `wav`, `flac`, `m4a`, `ogg`, `opus`, and more.

**Supported output formats:** `.txt` (plain text), `.srt` (subtitles with timestamps), `.vtt` (WebVTT)

---

## Setup

The **Transcribe** tab guides you through setup with two steps:

### Step 1: Install Whisper

The tab shows:
> *"Step 1: Install Whisper"*

Click **Install Whisper**. The app downloads [whisper.cpp](https://github.com/ggerganov/whisper.cpp) and installs it inside the MortysDLP folder. No Python, no virtual environments, no command line needed.

Once installed, the status changes to:
> *"✔ Step 1: Whisper installed"*

### Step 2: Download a language model

Click **Manage Whisper** to open the model manager. Then click **Download** next to the model you want. Once the status shows **✔ Installed**, you're ready.

> *"✔ Step 2: Model installed"*

---

## Using the Transcribe tab

Once Whisper and a model are installed:

1. **Select file:** — click **Browse…** and choose a video or audio file
2. **Whisper Model** — select the model to use (more details [below](#language-models))
3. **Language** — select the spoken language, or choose **Auto-detect** to let Whisper detect it automatically
4. **Output Format** — check one or more:
   - **Text file (.txt)** — plain text, no timestamps
   - **Subtitles (.srt) – for video editors** — timestamped subtitles, compatible with DaVinci Resolve, Premiere Pro, etc.
   - **WebVTT (.vtt)** — timestamped subtitles for web players
5. **Output folder:** — choose where to save the output files, or click **Browse…**
6. Click **Start Transcription**

Progress is shown in real time. When done, a dialog offers to open the output folder directly. You can also click **Open Folder** at any time.

Click **Cancel** to stop the transcription.

---

## Output formats

| Format | Description | Best for |
|---|---|---|
| `.txt` | Plain text — just the spoken words, no timestamps | Reading, copy-paste, note-taking |
| `.srt` | SubRip subtitles — timestamped lines | DaVinci Resolve, Premiere Pro, VLC, most video editors |
| `.vtt` | WebVTT — timestamped, web-compatible | HTML5 video players, streaming |

You can select multiple formats at once. Each will produce a separate output file.

---

## Language models

The model determines the trade-off between **speed** and **accuracy**. Larger models require more RAM and processing time but produce better results.

| Model | Approx. size | Speed | Accuracy | Notes |
|---|---|---|---|---|
| Tiny | ~75 MB | ⚡⚡⚡⚡ | ★☆☆☆ | Very fast, lower quality |
| Base | ~142 MB | ⚡⚡⚡ | ★★☆☆ | **Good starting point** |
| Small | ~466 MB | ⚡⚡ | ★★★☆ | Good balance |
| Medium | ~1.5 GB | ⚡ | ★★★★ | High accuracy |
| Large-v3-Turbo | ~1.6 GB | ⚡⚡ | ★★★★ | Fast & highly accurate |
| Large-v3 | ~3.1 GB | slow | ★★★★★ | Highest accuracy |

> **Recommendation:** Start with **Base**. If accuracy is not sufficient, try **Small** or **Large-v3-Turbo**.

---

## Managing Whisper

Click **Manage Whisper** (or the **Manage Whisper** button on the Transcribe tab) to open the Whisper model manager. Here you can:

- **Download** additional models
- **Delete** models to free up disk space
- **Install / Update Whisper** — installs or updates the Whisper engine itself
- **Check for Updates** — checks whether a newer version of the Whisper engine is available
- **Uninstall Whisper** — removes the Whisper installation:
  - **Remove tool only** — deletes `whisper.exe` and DLLs, but keeps all downloaded models. Use this to repair a broken installation without re-downloading models.
  - **Delete everything** — removes everything including all models. Frees the most disk space.

The models folder path is shown at the bottom of the manager window.

---

## Transcription after download

The **Download** tab includes an option:
> **Automatically transcribe after download**

When enabled, the downloaded file is automatically passed to Whisper after the download completes. You can select the **Model**, **Language**, and **Format** directly in the download options. Output files are saved in the same folder as the downloaded video.

> This option requires Whisper and at least one model to be installed. If Whisper is missing, a hint will appear:  
> *"Whisper not installed – go to 'Transcribe' in the menu."*

You can also enable:
> **Save chapters as text file (if available)** — extracts chapter information from the video and saves it as a text file alongside the download.

---

← [Usage Guide](usage.md) | [Overview →](README.md)
