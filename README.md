# Jellyfin Audiobook Chapters Plugin

A Jellyfin plugin that extracts chapter and metadata information from sidecar files for audiobooks. It supports multiple metadata formats and provides a custom API endpoint for retrieving audiobook chapters, since Jellyfin's built-in chapter support is limited to video content only.

Designed to be used with [Inglenook](https://github.com/kf7mxe/inglenook), an audiobook client app for Jellyfin.

## Features

- **6 metadata parsers** with configurable priority:
  - **OPF** (.opf) - Calibre/EPUB metadata with Dublin Core support (priority 100)
  - **JSON** (.json) - Generic, Audiobookshelf, and chapters-only formats (priority 90)
  - **NFO** (.nfo) - Kodi/XBMC metadata files (priority 80)
  - **FFmetadata** (.ffmetadata) - FFmpeg metadata with chapter markers (priority 60)
  - **CUE** (.cue) - CUE sheet files with chapter timestamps (priority 50)
  - **Text** (.txt) - Simple text files for chapters, descriptions, etc. (priority 30)

- **Custom chapters API endpoint** - Exposes `/Inglenook/{itemId}` since Jellyfin only supports chapters on video items, not audio/book items

- **Metadata merging** - Combines metadata from multiple sources using configurable priority ordering

- **Supports both Audio and Book library types**

- **Built-in web client hosting** - Automatically downloads and serves the [Inglenook](https://github.com/kf7mxe/inglenook) web client from GitHub releases at `/Inglenook/App`, with automatic update checks. Can be toggled on/off in plugin settings.

## Supported Metadata

| Field | OPF | JSON | NFO | FFmeta | CUE | TXT |
|-------|-----|------|-----|--------|-----|-----|
| Title | x | x | x | x | x | x |
| Author | x | x | x | x | x | x |
| Narrator | x | x | x | x | x | x |
| Description | x | x | x | x | x | x |
| Chapters | | x | | x | x | x |
| Series | x | x | x | | | x |
| Genres | x | x | x | x | x | x |
| ISBN/ASIN | x | x | x | | | x |
| Publisher | x | x | x | x | | x |
| Year/Date | x | x | x | x | x | x |
| Rating | x | x | x | | | |
| Cover Image | x | | x | | | |

## Installation

### Via Repository URL (Recommended)

This is the easiest way to install and keep the plugin updated.

1.  Go to **Dashboard > Plugins** in your Jellyfin server.
2.  Select the **Repositories** tab and click **Add**.
3.  Enter the following:
    - **Repository Name**: `Inglenook`
    - **Repository URL**: `https://raw.githubusercontent.com/kf7mxe/Inglenook-jellyfin-plugin/master/manifest.json`
4.  Click **Save**.
5.  Switch to the **Catalog** tab. You should now see **Inglenook** listed under the **Metadata** category.
6.  Click on it, select the latest version, and click **Install**.
7.  **Restart Jellyfin**.
8.  Go to **Dashboard > Plugins > Installed** and click on **Inglenook**.
9.  Under **Library Selection**, check the libraries you want the plugin to manage and click **Save**.

*Note: If the plugin doesn't appear in the catalog immediately after adding the repository, perform a hard refresh of your browser (Ctrl+F5).*

### From Release (Manual)

1. Download the latest `Jellyfin.Plugin.Inglenook.dll` from the releases page
2. Create a folder called `AudiobookChapters` in your Jellyfin plugins directory:
   - **Linux**: `~/.local/share/jellyfin/plugins/AudiobookChapters/`
   - **Docker**: `/config/plugins/AudiobookChapters/`
   - **Windows**: `%APPDATA%\jellyfin\plugins\AudiobookChapters\`
3. Place the DLL in that folder
4. Restart Jellyfin
5. Go to **Dashboard > Plugins > Installed** and click on **Inglenook**.
6. Under **Library Selection**, check the libraries you want the plugin to manage and click **Save**.

## Usage

1. **Configure Libraries**: Go to **Dashboard > Plugins > Inglenook** and select which libraries the plugin should scan.
2. **Sidecar Files**: Place sidecar metadata files alongside your audiobook files. For example:
git clone <repo-url>
cd "Jellyfin Audiobook Chapters Plugin"
dotnet publish Jellyfin.Plugin.Inglenook/Jellyfin.Plugin.Inglenook.csproj \
    --configuration Release \
    --output bin/publish
```

Copy `bin/publish/Jellyfin.Plugin.Inglenook.dll` to your Jellyfin plugins directory as described above.

### Deploy (Linux with systemd)

```bash
# Build
dotnet publish Jellyfin.Plugin.Inglenook/Jellyfin.Plugin.Inglenook.csproj \
    --configuration Release \
    --output bin/publish

# Copy to Jellyfin plugins directory
sudo mkdir -p /var/lib/jellyfin/plugins/Inglenook
sudo cp bin/publish/Jellyfin.Plugin.Inglenook.dll /var/lib/jellyfin/plugins/Inglenook/

# Restart Jellyfin to load the plugin
sudo systemctl restart jellyfin
```

## Usage

1. Place sidecar metadata files alongside your audiobook files. For example:

```
My Audiobook/
├── audiobook.m4b
├── metadata.opf        # Calibre metadata
├── chapters.txt        # Chapter timestamps
└── cover.jpg           # Cover image
```

2. Refresh metadata on the audiobook item in Jellyfin (right-click > Refresh Metadata)

3. Chapters and metadata will be extracted and applied automatically

### Custom API Endpoint

Since Jellyfin's built-in API only returns chapters for video items, this plugin provides its own endpoint:

```
GET /Inglenook/{itemId}    - Get chapters for an audiobook
GET /Inglenook/Libraries   - List available libraries
```

These endpoints require authentication. The [Inglenook](https://github.com/kf7mxe/inglenook) client app uses these endpoints to display chapter navigation for audiobooks.

### Inglenook Web Client

The plugin can automatically download and serve the Inglenook web client directly from your Jellyfin server. Once installed, access it at:

```
http://<your-jellyfin-server>/Inglenook/App
```

The web client is downloaded from GitHub releases on first run and checked for updates daily. You can configure the GitHub repository, enable/disable automatic updates, or disable the web client entirely from the plugin settings page.

Additional status endpoint:

```
GET /Inglenook/App/Status   - Get web client install status, version, and settings
```

## Configuration

Access plugin settings at **Dashboard > Plugins > Audiobook Chapters**.

### Options

| Setting | Default | Description |
|---------|---------|-------------|
| Enable CUE Files | true | Parse .cue sheet files |
| Enable OPF Files | true | Parse .opf metadata files |
| Enable JSON Metadata | true | Parse .json metadata files |
| Enable NFO Files | true | Parse .nfo metadata files |
| Enable FFmetadata | true | Parse .ffmetadata files |
| Enable Text Files | true | Parse .txt metadata files |
| Metadata Priority | opf,json,nfo,cue,ffmetadata,txt | Source priority order |
| Enable Web Client | true | Serve the Inglenook web client at /Inglenook/App |
| GitHub Repository | kf7mxe/inglenook | Source repo for web client releases (owner/repo) |
| Enable Automatic Updates | true | Check for and download new web client releases daily |

## Supported File Formats

### CUE Sheets (.cue)

Standard CUE sheet format with chapter markers:

```
REM GENRE Science Fiction
REM DATE 2023
PERFORMER "Author Name"
TITLE "Book Title"
FILE "audiobook.m4b" WAVE
  TRACK 01 AUDIO
    TITLE "Chapter 1"
    INDEX 01 00:00:00
  TRACK 02 AUDIO
    TITLE "Chapter 2"
    INDEX 01 05:30:45
```

### OPF Files (.opf)

Calibre/EPUB metadata with Dublin Core:

```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://www.idpf.org/2007/opf">
  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"
            xmlns:opf="http://www.idpf.org/2007/opf">
    <dc:title>Book Title</dc:title>
    <dc:creator opf:role="aut">Author Name</dc:creator>
    <dc:creator opf:role="nrt">Narrator Name</dc:creator>
    <dc:description>Book description</dc:description>
    <dc:identifier opf:scheme="ISBN">9781234567890</dc:identifier>
    <meta name="calibre:series" content="Series Name"/>
    <meta name="calibre:series_index" content="1"/>
  </metadata>
</package>
```

### JSON Metadata

Supports multiple JSON structures:

**Generic format** (metadata.json, book.json):
```json
{
  "title": "Book Title",
  "author": "Author Name",
  "narrator": "Narrator Name",
  "series": { "name": "Series", "position": 1 },
  "chapters": [
    { "title": "Chapter 1", "start": 0 },
    { "title": "Chapter 2", "start": 300.5 }
  ]
}
```

**Audiobookshelf format** (abs.json):
```json
{
  "libraryItem": {
    "media": {
      "metadata": {
        "title": "Book Title",
        "authors": [{ "name": "Author" }],
        "narrators": [{ "name": "Narrator" }]
      },
      "chapters": [...]
    }
  }
}
```

**Chapters-only** (chapters.json):
```json
[
  { "title": "Chapter 1", "start": 0 },
  { "title": "Chapter 2", "start": 300 }
]
```

### FFmetadata (.ffmetadata)

FFmpeg metadata format:

```
;FFMETADATA1
title=Book Title
artist=Author Name

[CHAPTER]
TIMEBASE=1/1000
START=0
END=323450
title=Chapter 1

[CHAPTER]
TIMEBASE=1/1000
START=323450
END=650000
title=Chapter 2
```

### Text Files (.txt)

**chapters.txt** - Timestamp-based chapters:
```
00:00:00 Introduction
00:05:30 Chapter 1
01:15:00 Chapter 2
```

**info.txt / book.txt** - Key-value metadata:
```
Title: Book Title
Author: Author Name
Narrator: Narrator Name
Genre: Science Fiction, Fantasy
Year: 2023
ISBN: 9781234567890
```

**desc.txt / description.txt** - Full description text

**reader.txt / narrator.txt** - One narrator name per line

## Building

Requirements:
- .NET 9.0 SDK or later

```bash
dotnet publish Jellyfin.Plugin.Inglenook/Jellyfin.Plugin.Inglenook.csproj \
    --configuration Release \
    --output bin/publish
```

## License

This plugin is provided as-is for use with Jellyfin media server.
