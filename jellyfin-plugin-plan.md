# Jellyfin Audiobook Chapters Plugin - Implementation Plan for Claude Code

## Project Overview

Create a new directory in this repo. Build a Jellyfin plugin that goes with this app but can be used without this app,  that extracts chapter and metadata information from sidecar files (`.cue`, `.opf`, `.json`, `.nfo`, `.ffmetadata`, `.txt`) for audiobooks. The plugin should also detect multi-file audiobooks (where each chapter is a separate audio file) and create chapter entries for them.

## Initial Setup Prompt

```
Create a new Jellyfin plugin project called "Jellyfin.Plugin.AudiobookChapters" targeting .NET 8.0.

1. Clone or reference the Jellyfin plugin template structure
2. Set up the project with these NuGet packages:
   - Jellyfin.Model (version 10.9.*)
   - Jellyfin.Controller (version 10.9.*)
   - Microsoft.Extensions.Logging.Abstractions (version 8.0.0)

3. Create this folder structure:
   ```
Jellyfin.Plugin.AudiobookChapters/
├── Jellyfin.Plugin.AudiobookChapters.csproj
├── Plugin.cs
├── Configuration/
│   ├── PluginConfiguration.cs
│   └── configPage.html
├── Models/
│   └── ParsedAudiobookMetadata.cs
├── Parsers/
│   ├── IMetadataParser.cs
│   ├── CueParser.cs
│   ├── OpfParser.cs
│   ├── JsonMetadataParser.cs
│   ├── NfoParser.cs
│   ├── FfmetadataParser.cs
│   ├── SimpleTextParser.cs
│   ├── MultiFileAudiobookHandler.cs
│   └── MetadataAggregator.cs
└── Providers/
└── AudiobookChapterProvider.cs
   ```

The plugin GUID should be: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

## Step 1: Core Plugin Files

### Prompt 1.1 - Plugin.cs
```
Create Plugin.cs - the main plugin entry point.

Requirements:
- Extend BasePlugin<PluginConfiguration>
- Implement IHasWebPages for the config page
- Plugin name: "Audiobook Chapters"
- GUID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
- Include a static Instance property for accessing configuration
- Return the embedded configPage.html in GetPages()
```

### Prompt 1.2 - PluginConfiguration.cs
```
Create Configuration/PluginConfiguration.cs with these settings:

- EnableCueFiles (bool, default: true)
- EnableOpfFiles (bool, default: true)
- EnableJsonMetadata (bool, default: true)
- EnableNfoFiles (bool, default: true)
- EnableFfmetadata (bool, default: true)
- EnableTextFiles (bool, default: true)
- EnableMultiFileDetection (bool, default: true)
- MultiFileChapterNaming (enum: UseFilename, UseMetadataTitle, UseSequentialNumbering, ParseFilenamePattern)
- MetadataPriority (string, default: "opf,json,nfo,cue,ffmetadata,txt")

Also create the MultiFileNamingStrategy enum in the same file.
```

### Prompt 1.3 - configPage.html
```
Create Configuration/configPage.html - a Jellyfin admin page for plugin settings.

Requirements:
- Use Jellyfin's standard plugin config page structure
- Checkboxes for each EnableX setting
- Dropdown for MultiFileChapterNaming strategy
- Text input for MetadataPriority
- Save button that calls ApiClient.updatePluginConfiguration
- Load existing config on pageshow event
- Use the plugin GUID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
```

---

## Step 2: Data Models

### Prompt 2.1 - ParsedAudiobookMetadata.cs
```
Create Models/ParsedAudiobookMetadata.cs - a unified model for metadata from any parser.

Include these properties:
- SourceFile, SourceType (string) - tracking where data came from
- Title, SortTitle, OriginalTitle, Subtitle (string)
- Description (string)
- Authors, Narrators (List<string>)
- Publisher (string)
- PublishedDate (DateTime?), Year (int?)
- Genres, Tags (List<string>)
- Language (string)
- CommunityRating, CriticRating (float?)
- Abridged (bool?)
- SeriesName (string), SeriesIndex (float?)
- Isbn, Isbn13, Asin, AudibleAsin, GoodreadsId, GoogleBooksId, OpenLibraryId (string)
- ProviderIds (Dictionary<string, string>)
- Chapters (List<ChapterInfo> from MediaBrowser.Model.Entities)
- DurationTicks (long?)
- CoverImagePath (string)
- HasChapters, HasContent (computed bool properties)

Also create an AudiobookFile class for multi-file handling with:
- Path, Name (string)
- SortOrder, TrackNumber (int?)
- Title (string)
- DurationTicks, StartPositionTicks (long)
```

---

## Step 3: Parser Interface and Implementations

### Prompt 3.1 - IMetadataParser.cs
```
Create Parsers/IMetadataParser.cs - the interface all parsers implement.

Properties:
- Name (string) - parser display name
- Priority (int) - higher = more trusted
- SupportedExtensions (string[]) - file extensions this handles
- FilePatterns (string[]) - specific filenames to look for

Methods:
- bool CanParse(string filePath)
- Task<ParsedAudiobookMetadata?> ParseAsync(string filePath, CancellationToken)
- ParsedAudiobookMetadata? ParseContent(string content, string? sourcePath)
```

### Prompt 3.2 - CueParser.cs
```
Create Parsers/CueParser.cs - parser for CUE sheet files.

Requirements:
- Priority: 50
- Parse these CUE commands:
  - REM GENRE, REM DATE, REM COMMENT
  - PERFORMER (album and track level)
  - TITLE (album and track level)
  - SONGWRITER (treat as narrator)
  - TRACK nn AUDIO
  - INDEX 01 MM:SS:FF (frames are 1/75 second)
  - ISRC
- Convert INDEX timestamps to ticks (TimeSpan.TicksPerSecond)
- Handle both album-level and track-level metadata
- Use source-generated regex ([GeneratedRegex]) for .NET 8

Example CUE to handle:
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
```

### Prompt 3.3 - OpfParser.cs
```
Create Parsers/OpfParser.cs - parser for OPF (Calibre/EPUB) metadata files.

Requirements:
- Priority: 100 (highest - most complete metadata)
- Parse Dublin Core namespace (http://purl.org/dc/elements/1.1/):
  - dc:title, dc:creator (with opf:role for aut/nrt), dc:description
  - dc:publisher, dc:date, dc:language, dc:subject, dc:identifier
- Parse opf:scheme attribute for identifier types (ISBN, ASIN, etc.)
- Parse Calibre-specific meta tags:
  - calibre:series, calibre:series_index, calibre:rating, calibre:title_sort
- Strip HTML from description field
- Look for cover images in same directory (cover.jpg, cover.png, folder.jpg)

Use System.Xml.Linq (XDocument) for parsing.
```

### Prompt 3.4 - JsonMetadataParser.cs
```
Create Parsers/JsonMetadataParser.cs - parser for JSON metadata files.

Requirements:
- Priority: 90
- Support these file patterns: metadata.json, audiobook.json, book.json, chapters.json, abs.json, info.json
- Handle multiple JSON structures:

1. Generic format - flexible property names:
   - title/name/bookTitle
   - author/authors/writer/writers (string or array)
   - narrator/narrators/reader/readers (string or array)
   - series as object {name, position} or string
   - chapters array with start times in seconds or milliseconds

2. Audiobookshelf (abs.json) format:
   - Nested structure: libraryItem.media.metadata
   - Authors/narrators as objects with name property
   - Series as array with name and sequence

3. Chapters-only format (just an array of chapters)

Use System.Text.Json for parsing. Handle missing properties gracefully.
Create helper methods: GetStringProperty, GetIntProperty, GetFloatProperty, ParseStringOrArray.
```

### Prompt 3.5 - NfoParser.cs
```
Create Parsers/NfoParser.cs - parser for NFO (Kodi/XBMC) files.

Requirements:
- Priority: 80
- Handle XML that may have non-XML content before it (URLs, text)
- Support root elements: audiobook, book, album, movie
- Parse elements:
  - title, originaltitle, sorttitle
  - plot/outline/description
  - author/artist/writer, narrator/reader/performer
  - actor elements with role="narrator"
  - publisher/studio/label
  - year, releasedate/premiered
  - genre (multiple), tag (multiple)
  - rating/userrating
  - runtime (in minutes)
  - set/series with name child
  - uniqueid elements with type attribute (isbn, asin, goodreads, etc.)
  - thumb/poster/cover for images

Use System.Xml.Linq for parsing.
```

### Prompt 3.6 - FfmetadataParser.cs
```
Create Parsers/FfmetadataParser.cs - parser for FFmpeg metadata format.

Requirements:
- Priority: 60
- File must start with ";FFMETADATA1"
- Parse INI-like format with sections:
  - Global section: title, artist, album_artist, composer (narrator), genre, date, publisher
  - [CHAPTER] sections with:
    - TIMEBASE=1/1000 (or other values)
    - START=<time_in_timebase_units>
    - END=<time>
    - title=<chapter_title>
- Convert chapter times to ticks based on TIMEBASE
- Handle escaped characters: \=, \;, \#, \\, \n

Example:
```
;FFMETADATA1
title=Book Title
artist=Author Name

[CHAPTER]
TIMEBASE=1/1000
START=0
END=323450
title=Chapter 1
```
```

### Prompt 3.7 - SimpleTextParser.cs
```
Create Parsers/SimpleTextParser.cs - parser for simple text metadata files.

Requirements:
- Priority: 30 (lowest - fallback)
- Support files: desc.txt, description.txt, reader.txt, narrator.txt, info.txt, about.txt, chapters.txt, book.txt

Parse strategies based on filename:
1. chapters.txt - Parse timestamp lines: "HH:MM:SS Title" or "MM:SS Title"
2. reader.txt/narrator.txt - Each line is a narrator name
3. desc.txt/description.txt - Entire content is the description
4. info.txt/book.txt - Parse "Key: Value" format for:
   - title, author, narrator, publisher, year, date, genre, series, duration, language, isbn, asin, description, abridged

For chapters.txt, support these timestamp formats:
- 00:00:00 Title
- 0:00:00 Title
- 00:00:00.000 Title
- [00:00:00] Title

Use source-generated regex for patterns.
```

---

## Step 4: Multi-File Audiobook Handler

### Prompt 4.1 - MultiFileAudiobookHandler.cs
```
Create Parsers/MultiFileAudiobookHandler.cs - detects and processes multi-file audiobooks.

Requirements:
- Supported audio extensions: .mp3, .m4a, .m4b, .flac, .ogg, .opus, .wma, .aac, .wav, .aiff

Detection (IsMultiFileAudiobook method):
- Directory has 2+ audio files
- At least 70% of files match a numbering pattern

File naming patterns to recognize (with regex):
- "01 - Title.mp3" or "01. Title.mp3"
- "Chapter 01 - Title.mp3" or "Chapter 1.mp3"
- "Part 1 - Title.mp3"
- "Track 01.mp3"
- "Disc 1 - 01 - Title.mp3" (multi-disc)
- Simple number prefix: "1 Title.mp3"

Methods:
- bool IsMultiFileAudiobook(string directoryPath)
- List<AudiobookFile> GetAudioFilesInOrder(string directoryPath)
  - Parse track numbers from filenames
  - Sort by track number, then filename
- List<ChapterInfo> CreateChaptersFromFiles(List<AudiobookFile> files, MultiFileNamingStrategy strategy)
  - Apply naming strategy to determine chapter titles
- Task<ParsedAudiobookMetadata?> CreateMetadataFromDirectoryAsync(...)

Helper methods:
- (int? trackNumber, string? title) ParseFilename(string filename)
- string CleanupChapterName(string name) - remove track numbers, common prefixes
```

---

## Step 5: Metadata Aggregator

### Prompt 5.1 - MetadataAggregator.cs
```
Create Parsers/MetadataAggregator.cs - orchestrates parsing and merges results.

Requirements:
- Initialize all parser instances in constructor
- Inject ILogger<MetadataAggregator> and ILogger<MultiFileAudiobookHandler>

Main method: GetMetadataAsync(string itemPath, CancellationToken)
1. Get directory from item path
2. Find all metadata files using each parser's CanParse method
3. Parse each file, collect results
4. If EnableMultiFileDetection and directory is multi-file audiobook, add that metadata
5. Merge all metadata using priority order

Helper methods:
- FindMetadataFiles(directory, config) - returns list of (parser, filePath) tuples
- IsParserEnabled(parser, config) - check config flags
- ParsePriorityOrder(string) - parse comma-separated priority string
- MergeMetadata(List<ParsedAudiobookMetadata>, priorityOrder) - merge with priority

Merge rules:
- For scalar fields: first non-null value wins (by priority)
- For lists (Authors, Genres, etc.): combine unique values
- For chapters: prefer explicit chapter files over multi-file detection, then prefer more chapters
```

---

## Step 6: Jellyfin Provider Integration

### Prompt 6.1 - AudiobookChapterProvider.cs
```
Create Providers/AudiobookChapterProvider.cs - the Jellyfin metadata provider.

Requirements:
- Implement ICustomMetadataProvider<Audio> for audio files
- Implement IHasItemChangeMonitor to detect when metadata files change
- Inject: IFileSystem, ILogger, ILibraryManager, ILoggerFactory

FetchAsync method:
1. Skip if item.Path is empty
2. Call MetadataAggregator.GetMetadataAsync()
3. If chapters found, call _libraryManager.SaveChapters(item.Id, chapters)
4. Apply other metadata to item (only if item's field is empty):
   - ProviderIds (ISBN, ASIN, Audible, Goodreads, GoogleBooks)
   - Genres, Tags
   - Overview (description)
   - ProductionYear, PremiereDate
   - CommunityRating
5. Return ItemUpdateType flags

HasChanged method:
- Check if any metadata files in the directory have LastWriteTimeUtc > item.DateLastSaved
- Check extensions: .cue, .opf, .nfo, .json, .txt, .ffmetadata

Also create BookChapterProvider implementing ICustomMetadataProvider<Book> with similar logic for the Books library type.
```

---

## Step 7: Build and Test

### Prompt 7.1 - Build Configuration
```
Create the solution file (Jellyfin.Plugin.AudiobookChapters.sln) and build.yaml manifest.

build.yaml should include:
- name: "Audiobook Chapters"
- guid: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
- version: "1.0.0.0"
- targetAbi: "10.9.0.0"
- framework: "net8.0"
- category: "Metadata"
- Description of all supported formats

Build command: dotnet publish --configuration Release --output bin
```

### Prompt 7.2 - Testing
```
Create test files to verify the plugin works:

1. Test CUE parsing with a sample .cue file
2. Test OPF parsing with a sample metadata.opf
3. Test JSON parsing with sample metadata.json and chapters.json
4. Test multi-file detection with a folder structure simulation
5. Verify chapter times are correctly converted to ticks

Write unit tests if time permits using xUnit.
```

---

## Quick Reference: Key Jellyfin Types

```csharp
// For chapters
using MediaBrowser.Model.Entities;
ChapterInfo { Name, StartPositionTicks, ImagePath, ImageTag, ImageDateModified }

// For providers
using MediaBrowser.Controller.Providers;
ICustomMetadataProvider<T> { Task<ItemUpdateType> FetchAsync(T item, MetadataRefreshOptions, CancellationToken) }
IHasItemChangeMonitor { bool HasChanged(BaseItem item, IDirectoryService) }

// For saving chapters
ILibraryManager.SaveChapters(Guid itemId, List<ChapterInfo> chapters)

// Item types
using MediaBrowser.Controller.Entities.Audio;
Audio - for audio files
Book - for book library items

// Update types
ItemUpdateType.None, ItemUpdateType.MetadataEdit, ItemUpdateType.MetadataImport
```

---

## Common Pitfalls to Avoid

1. **Time Conversion**: CUE frames are 1/75 second, not milliseconds
2. **Null Checking**: Always check if paths/properties are null before using
3. **File Encoding**: Use UTF-8 when reading text files
4. **XML Namespaces**: OPF files use Dublin Core and OPF namespaces - must handle correctly
5. **JSON Flexibility**: Different tools use different property names - support multiple variations
6. **Priority Merging**: Higher priority sources should override lower ones, not append
7. **Chapter Ordering**: Multi-file chapters must maintain correct order
8. **Cancellation Tokens**: Pass through all async operations

---

## Success Criteria

The plugin is complete when:
1. All 6 parsers correctly extract metadata from their respective formats
2. Multi-file audiobooks are detected and chapters created
3. Chapters appear in Jellyfin UI when browsing audiobooks
4. Chapters are accessible via API: `GET /Items/{id}?Fields=Chapters`
5. Configuration page allows enabling/disabling features
6. Plugin builds without errors targeting .NET 8.0