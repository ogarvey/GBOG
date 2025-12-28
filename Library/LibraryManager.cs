using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GBOG.Utils;
using IGDB;
using IGDB.Models;

namespace GBOG.Library
{
    public class LibraryEntry
    {
        public string RomPath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? CoverPath { get; set; }
        public uint? CoverTextureId { get; set; }
    }

    public class LibraryManager
    {
        private readonly AppSettings _settings;
        private IGDBClient? _igdbClient;
        private readonly List<LibraryEntry> _entries = new();
        private readonly string _coversDirectory;

        public IReadOnlyList<LibraryEntry> Entries => _entries;

        public LibraryManager(AppSettings settings)
        {
            _settings = settings;
            _coversDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Covers");
            if (!Directory.Exists(_coversDirectory))
            {
                Directory.CreateDirectory(_coversDirectory);
            }
        }

        public void ResetClient()
        {
            _igdbClient = null;
        }

        private void EnsureClient()
        {
            if (_igdbClient == null && !string.IsNullOrEmpty(_settings.IgdbClientId) && !string.IsNullOrEmpty(_settings.IgdbClientSecret))
            {
                _igdbClient = new IGDBClient(_settings.IgdbClientId, _settings.IgdbClientSecret);
            }
        }

        public void ScanLibrary()
        {
            _entries.Clear();
            if (string.IsNullOrEmpty(_settings.GameFolderPath) || !Directory.Exists(_settings.GameFolderPath))
            {
                return;
            }

            var files = Directory.GetFiles(_settings.GameFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".gb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gbc", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var entry = new LibraryEntry
                {
                    RomPath = file,
                    Title = Path.GetFileNameWithoutExtension(file),
                };

                var coverPath = Path.Combine(_coversDirectory, entry.Title + ".jpg");
                if (File.Exists(coverPath))
                {
                    entry.CoverPath = coverPath;
                }

                _entries.Add(entry);
            }
        }

        public async Task DownloadCoverAsync(LibraryEntry entry)
        {
            EnsureClient();
            if (_igdbClient == null) return;

            try
            {
                // Search for the game
                var games = await _igdbClient.QueryAsync<Game>(IGDBClient.Endpoints.Games, 
                    $"search \"{entry.Title}\"; fields name,cover.url; where platforms = (33,22); limit 1;");

                if (games != null && games.Length > 0)
                {
                    var game = games[0];
                    if (game.Cover != null && game.Cover.Value.Url != null)
                    {
                        var url = game.Cover.Value.Url;
                        if (url.StartsWith("//")) url = "https:" + url;
                        
                        // IGDB returns thumb by default, we want big cover
                        url = url.Replace("t_thumb", "t_cover_big");

                        using var httpClient = new HttpClient();
                        var bytes = await httpClient.GetByteArrayAsync(url);
                        
                        var coverPath = Path.Combine(_coversDirectory, entry.Title + ".jpg");
                        await File.WriteAllBytesAsync(coverPath, bytes);
                        entry.CoverPath = coverPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download cover for {entry.Title}: {ex.Message}");
            }
        }

        public async Task DownloadAllCoversAsync(Action<int, int>? progressCallback = null)
        {
            int count = 0;
            foreach (var entry in _entries)
            {
                if (string.IsNullOrEmpty(entry.CoverPath))
                {
                    await DownloadCoverAsync(entry);
                }
                count++;
                progressCallback?.Invoke(count, _entries.Count);
            }
        }
    }
}
