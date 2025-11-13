# Omega Music Player

[![en](https://img.shields.io/badge/lang-en-red.svg)](README.md)
[![pt-br](https://img.shields.io/badge/lang-pt--br-green.svg)](README.pt-BR.md)

A local music player for Windows with customizable themes that combines intuitive functionality and modern design. Built with Avalonia UI.

## Key Features

- **Playback Controls**: Play, pause, previous, skip, advance, rewind, shuffle, repeat
- **Playlist Management**: Create, edit, and organize playlists
- **Favorites System**: Mark tracks as favorites for quick access in favorite playlist
- **Search**: Search any songs in your library
- **Drag & Drop**: Reorder tracks and playlists intuitively
- **Queue Management**: Add tracks to queue, play next, or playlist
- **Library Statistics**: View detailed properties about your music

## 🎵 What Makes Omega Music Player Different

### Multi-Profile System
Create up to 20 profiles with shared library but individual settings and preferences. 
- Library, language, and Artist artwork fetch options are shared across all profiles
- All other settings are unique to each profile

Perfect for:
- Shared computers with multiple users
- Separating work and personal music
- Creating themed profiles for different moods

### Flexible UI Customization
- **Dark/Light Modes**: 2 pre-made dark and light mode preferences
- **Custom Themes**: Create personalized themes with gradient support

### Four Unique View Modes
Switch between viewing styles to match your preference:
- **List View**: Classic, information-dense layout with all information visible
- **Card View**: Album-style cards with cover art and essential info
- **Image View**: Cover art focus with minimal text overlay
- **Round Image View**: Distinctive circular thumbnails

### Smart Library Management
- **Blacklist System**: Hide folders or files from your library without deleting their information
- **Artist Images**: Fetch artist artwork from online sources (requires internet, can be disabled)
- **Background Indexing**: Automatically Scan your library upon changes without interrupting playback

### Enhanced Listening Features
- **Sleep Timer**: Set automatic playback shutdown after a specified duration
- **Lyrics Display**: View lyrics while listening (when available in metadata)
- **Dynamic Pause**: Automatically pause playback when other audio plays (e.g., videos, calls), then resume when finished (optional, disabled by default)

### Performance Optimized
- Handles large libraries (10,000+ tracks) with ease
- Fast library loading

### ⚠️ Supported Formats
- MP3, AAC, M4A

## 🚀 Getting Started

### Requirements
- Windows 10/11 (64-bit)
- Visual C++ Redistributable

### Installation
1. **Install Prerequisites**: Download and run **VC_redist.x64.exe** from [latest release](https://github.com/erick-panse/OmegaMusicPlayer/releases/latest) or directly from [Microsoft](https://aka.ms/vc14/vc_redist.x64.exe)
2. **Download**: Get **OmegaMusicPlayer.zip** from [latest release](https://github.com/erick-panse/OmegaMusicPlayer/releases/latest)
3. **Extract**: Unzip to your preferred location
4. **Run**: Launch **OmegaMusicPlayer.exe**

## Technology Stack

- **Framework**: Avalonia UI (C#, .NET 8.0)
- **Database**: PostgreSQL (Embedded via [MysticMind.PostgresEmbed](https://github.com/mysticmind/mysticmind-postgresembed))
- **ORM**: Entity Framework Core
- **Audio Engine**: NAudio
- **Metadata**: TagLibSharp
- **Architecture**: MVVM with CommunityToolkit.Mvvm

## License

Omega Music Player is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

Copyright © 2025 Erick Panse

## Bugs and Issues

Found a bug? Please report it on [GitHub Issues](https://github.com/erick-panse/OmegaMusicPlayer/issues) page.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

For major changes, please open an issue first to discuss what you would like to change.