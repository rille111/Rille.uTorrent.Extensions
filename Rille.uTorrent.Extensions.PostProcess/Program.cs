﻿using System.Linq;
using NLog;
using Rille.uTorrent.Extensions.PostProcess.Model;
using Rille.uTorrent.Extensions.PostProcess.Services;

namespace Rille.uTorrent.Extensions.PostProcess
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static string[] _arguments;
        private static Config _config;
        private static ITorrentManager _torrentManager;

        static void Main(string[] args)
        {
            Logger.Debug("Application starting.");

            _arguments = args;
            _config = new Config();
            var fileManager = new FileManager(_config);
            _torrentManager = new UTorrentManager(_config, fileManager);

            VerifyConfig(_config);

            if (ShouldProcessAllTorrents())
                ProcessAllTorrents();

            if (ShouldProcessOneTorrent())
                ProcessOneTorrent(new Torrent(_arguments[0]));
            

        }

        private static void VerifyConfig(Config config)
        {
            
        }

        private static bool ShouldProcessAllTorrents()
        {
            return _arguments.Any() && _arguments[0] == "-all";
        }

        private static void ProcessAllTorrents()
        {

        }

        private static bool ShouldProcessOneTorrent()
        {
            // Anything other than - will be interpreted as a Hash.
            return _arguments.Any() && _arguments[0] != "-";
        }

        private static void ProcessOneTorrent(Torrent torrent)
        {
            
            Logger.Debug($"Torrent hash is: {torrent.Hash}. I will now load all torrents.");

            var torrents = _torrentManager.GetTorrentList();
            torrent = torrents.Single(p => p.Hash == torrent.Hash);
            Logger.Info($"Found torrent. Name: {torrent.Name}, NumericStatus: {torrent.NumericStatus}, Status: {torrent.TorrentStatus}");

            if (!_torrentManager.TorrentHasBeenPostProcessed(torrent))
            {
                // Execute PostProcess
                // TODO: Create method and move logging here
                Logger.Info("Post processes starting.");
            }

            if (_torrentManager.TorrentHasBeenPostProcessed(torrent) && _torrentManager.TorrentGoalsReached(torrent))
            {
                // Delete
                // TODO: Create method and move logging here
                Logger.Info("Deleting torrent, goals has been reached.");
            }
            else
            {
                Logger.Warn($"Torrent has either not been post processed yet, or the goals haven't been reached. Not doing anything.");
            }
        }
    }
}