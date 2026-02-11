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

### From Release

1. Download the latest `Jellyfin.Plugin.AudiobookChapters.dll` from the releases page
2. Create a folder called `AudiobookChapters` in your Jellyfin plugins directory:
   - **Linux**: `~/.local/share/jellyfin/plugins/AudiobookChapters/`
   - **Docker**: `/config/plugins/AudiobookChapters/`
   - **Windows**: `%APPDATA%\jellyfin\plugins\AudiobookChapters\`
3. Place the DLL in that folder
4. Restart Jellyfin

### From Source

```bash
git clone <repo-url>
cd "Jellyfin Audiobook Chapters Plugin"
dotnet publish Jellyfin.Plugin.AudiobookChapters/Jellyfin.Plugin.AudiobookChapters.csproj \
    --configuration Release \
    --output bin/publish
```

Copy `bin/publish/Jellyfin.Plugin.AudiobookChapters.dll` to your Jellyfin plugins directory as described above.

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
dotnet publish Jellyfin.Plugin.AudiobookChapters/Jellyfin.Plugin.AudiobookChapters.csproj \
    --configuration Release \
    --output bin/publish
```

## License

This plugin is provided as-is for use with Jellyfin media server.
