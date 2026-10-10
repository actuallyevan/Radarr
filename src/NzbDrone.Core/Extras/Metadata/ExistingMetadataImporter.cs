using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Extras.Subtitles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Extras.Metadata
{
    public class ExistingMetadataImporter : ImportExistingExtraFilesBase<MetadataFile>
    {
        private readonly IExtraFileService<MetadataFile> _metadataFileService;
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;
        private readonly IMetadataFactory _metadataFactory;

        public ExistingMetadataImporter(IExtraFileService<MetadataFile> metadataFileService,
                                        IMetadataFactory metadataFactory,
                                        IParsingService parsingService,
                                        Logger logger)
        : base(metadataFileService)
        {
            _metadataFileService = metadataFileService;
            _metadataFactory = metadataFactory;
            _parsingService = parsingService;
            _logger = logger;
        }

        public override int Order => 0;

        public override IEnumerable<ExtraFile> ProcessFiles(Movie movie, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename)
        {
            _logger.Debug("Looking for existing metadata in {0}", movie.Path);

            var metadataFiles = new List<MetadataFile>();
            var filterResult = FilterAndClean(movie, filesOnDisk, importedFiles, fileNameBeforeRename is not null);

            // Only metadata types that are enabled may claim files. A disabled type would
            // otherwise register files that other software wrote (folder.jpg and the like)
            // as Radarr's own, and they would be deleted with the movie file on an upgrade.
            var consumers = _metadataFactory.Enabled();
            var enabledConsumers = consumers.Select(c => c.GetType().Name).ToHashSet();

            // Forget entries an earlier scan made for a type that is not enabled now.
            // Only the database rows go, the files stay on disk.
            var stale = filterResult.PreviouslyImported.Where(f => !enabledConsumers.Contains(f.Consumer)).ToList();

            if (stale.Any())
            {
                _logger.Debug("Forgetting {0} metadata files from metadata types that are not enabled", stale.Count);
                _metadataFileService.DeleteMany(stale.Select(f => f.Id));
            }

            var previouslyImported = filterResult.PreviouslyImported.Except(stale).ToList();

            foreach (var possibleMetadataFile in filterResult.FilesOnDisk)
            {
                // Don't process files that have known Subtitle file extensions (saves a bit of unnecessary processing)

                if (SubtitleFileExtensions.Extensions.Contains(Path.GetExtension(possibleMetadataFile)))
                {
                    continue;
                }

                foreach (var consumer in consumers)
                {
                    var metadata = consumer.FindMetadataFile(movie, possibleMetadataFile);

                    if (metadata == null)
                    {
                        continue;
                    }

                    if (metadata.Type == MetadataType.MovieImage ||
                        metadata.Type == MetadataType.MovieMetadata)
                    {
                        var minimalInfo = _parsingService.ParseMinimalPathMovieInfo(possibleMetadataFile);

                        if (minimalInfo == null)
                        {
                            _logger.Debug("Unable to parse extra file: {0}", possibleMetadataFile);
                            continue;
                        }

                        metadata.MovieFileId = movie.MovieFileId;
                    }

                    metadata.Extension = Path.GetExtension(possibleMetadataFile);

                    metadataFiles.Add(metadata);
                }
            }

            _logger.Info("Found {0} existing metadata files", metadataFiles.Count);
            _metadataFileService.Upsert(metadataFiles);

            // Return files that were just imported along with files that were
            // previously imported so previously imported files aren't imported twice
            return metadataFiles.Concat(previouslyImported);
        }
    }
}
