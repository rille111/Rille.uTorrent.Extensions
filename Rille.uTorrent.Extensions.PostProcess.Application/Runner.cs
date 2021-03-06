﻿using System;
using System.Linq;
using NLog;
using Rille.uTorrent.Extensions.PostProcess.Model;
using Rille.uTorrent.Extensions.PostProcess.Services;

namespace Rille.uTorrent.Extensions.PostProcess
{
    public class Runner
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static Config _config = Config.Create();
        private static ITorrentManager _torrentManager;
        private static FileManager _fileManager = new FileManager(_config);
        private static Unpacker unpacker = new Unpacker(_config, _fileManager);
        private static int processedTorrentsCount = 0;

        /// <summary>
        /// Exceptions will be handled here. Just care about the exit code.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public int Run(string[] args)
        {
            try
            {
                _logger.Info("Application starting.");

                ValidateConfig();
                CreateTorrentManager();

                // All torrents or only one?
                var onlyThisTorrent = args?.Length > 0 ? args[0] : "";
                LogTorrentHash(onlyThisTorrent);
                var torrents = _torrentManager.GetTorrents(onlyThisTorrent);

                if (torrents == null || torrents.Count == 0)
                {
                    _logger.Info("No torrents found!");
                    return 666;
                }

                // Restart failed torrents
                if (_config.RestartErrorTorrents)
                {
                    foreach (var torrent in torrents.Where(p => p.TorrentStatus.HasFlag(TorrentStatus.Error)))
                    {
                        _torrentManager.Start(torrent);
                    }
                }

                _logger.Info($"LOOPING TORRENTS");
                foreach (var torrent in torrents)
                {
                    // TODO: TESTING
                    //if (torrent.Hash != "2D8043305E789EDBBA5537E569D3DF56A6E6E3E8")
                    //  continue;

                    LogStartProcessTorrent(torrent);
                    HandleAlreadyProcessedTorrent(torrent);
                    HandleUnprocessedTorrent(torrent);

                    if (processedTorrentsCount >= _config.MaxProcessTorrentsInBatch)
                    {
                        // Enough in this batch already. Exit.
                        _logger.Debug($"- Already Processed {processedTorrentsCount} in this batch which was the configured max, exiting..");
                        break;
                    }
                }
                _logger.Info($"FINISHED");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Unexpected error occurred.");
                return 666;
            }
            return 0;
        }

        private void LogTorrentHash(string onlyThisTorrent)
        {
            if (!string.IsNullOrEmpty(onlyThisTorrent))
                _logger.Debug("Argument torrent hash received: " + onlyThisTorrent);
        }

        private void HandleUnprocessedTorrent(Torrent torrent)
        {
            if (torrent.IsDownloaded && !_torrentManager.HasTorrentBeenProcessed(torrent))
            {
                if (processedTorrentsCount >= _config.MaxProcessTorrentsInBatch)
                {
                    // Enough in this batch already. Exit.
                    _logger.Debug($"- Already Processed {processedTorrentsCount} in this batch which was the configured max, exiting..");
                    return;
                }

                // Otherwise, lets unpack/process!
                processedTorrentsCount++;

                // Mark as in progress
                _torrentManager.MarkTorrentAsProcessing(torrent);

                var unpackedOk = unpacker.CopyAndUnpack(torrent);
                if (unpackedOk)
                {
                    // Mark torrent as finished
                    _torrentManager.MarkTorrentAsProcessed(torrent);
                    _logger.Info($"- Torrent process OK!");

                    if (_torrentManager.HasTorrentBeenProcessed(torrent) && _torrentManager.HasTorrentGoalsBeenReached(torrent))
                    {
                        // Delete if goals reached and torrent processed ok, if configured as such.
                        if (_config.DeleteFromTorrentsFolderWhenUnpacked)
                        {
                            _logger.Info("- Deleting (torrent is processed and goals has been reached.");
                            _torrentManager.DeleteTorrent(torrent);
                        }
                    }
                }
                else
                {
                    // Unpack error!! Quit!
                    _logger.Error($"- Failed to process torrent! Investigate logs. {nameof(unpacker.LastUnpackCommand)}: {unpacker.LastUnpackCommand}");
                    _torrentManager.MarkTorrentAsProcessFailed(torrent);
                }
            }
        }

        private void HandleAlreadyProcessedTorrent(Torrent torrent)
        {
            if (_torrentManager.HasTorrentBeenProcessed(torrent))
            {
                if (_torrentManager.HasTorrentGoalsBeenReached(torrent) && _config.DeleteAlreadyProcessedTorrents)
                {
                    // Torrents goal reached, and configured to deleted finished torrents, so delete it.
                    _logger.Info("- Deleting (torrent is processed and goals has been reached.");
                    _torrentManager.DeleteTorrent(torrent);
                }
            }
        }

        private void LogStartProcessTorrent(Torrent torrent)
        {
            _logger.Info($"Torrent: {torrent.Name} -");
            _logger.Debug($"- Status: {torrent.TorrentStatus}");
            _logger.Debug($"- ProcessingStatus: {torrent.ProcessingStatus}");
            _logger.Debug($"- IsDownloaded: {torrent.IsDownloaded}");
            _logger.Debug($"- HasTorrentGoalsBeenReached: {_torrentManager.HasTorrentGoalsBeenReached(torrent)}");
            _logger.Debug($"- Ratio: {torrent.ActualSeedRatioPercent}");
            _logger.Debug($"- Path: {torrent.Path}");
            _logger.Debug($"- IsFolder: {torrent.IsFolder}");
            _logger.Debug($"- IsSingleFileAndArchive: {torrent.IsSingleFileAndArchive}");
            _logger.Debug($"- IsSingleFileButNotArchive: {torrent.IsSingleFileButNotArchive}");
            _logger.Debug($"- DoesFolderContainAnyArchive: {_fileManager.DoesFolderContainAnyArchive(torrent.Path)}");
            
        }

        private void CreateTorrentManager()
        {
            if (_config.OperatingMode == OperatingMode.UnpackTorrentsFolderOnly)
                _torrentManager = new FolderBasedTorrentManager(_config, _fileManager);
            else
                _torrentManager = new UTorrentManager(_config);
        }

        private void ValidateConfig()
        {
            var result = new ConfigValidator().Validate(_config);

            if (result.IsValid)
            {
                _logger.Info("Config is valid.");
            }
            else
            {
                var errors = string.Join("\n", result.Errors.Select(p => p.PropertyName + ": " + p.ErrorMessage + " Value: " + p.AttemptedValue));
                throw new InvalidProgramException("Invalid configuration!\n" + errors);
            }
        }

        public void ExitApp(int exitCode)
        {
            Console.WriteLine("-- Finished, press any key to exit --");
            //Console.ReadKey();
            Environment.Exit(exitCode);
        }

    }
}
