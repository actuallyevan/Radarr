using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Extras.Others
{
    public class ExistingOtherExtraImporter : ImportExistingExtraFilesBase<OtherExtraFile>
    {
        private readonly IExtraFileService<OtherExtraFile> _otherExtraFileService;
        private readonly IParsingService _parsingService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ExistingOtherExtraImporter(IExtraFileService<OtherExtraFile> otherExtraFileService,
                                          IParsingService parsingService,
                                          IConfigService configService,
                                          Logger logger)
            : base(otherExtraFileService)
        {
            _otherExtraFileService = otherExtraFileService;
            _parsingService = parsingService;
            _configService = configService;
            _logger = logger;
        }

        public override int Order => 2;

        public override IEnumerable<ExtraFile> ProcessFiles(Movie movie, List<string> filesOnDisk, List<string> importedFiles, string fileNameBeforeRename)
        {
            _logger.Debug("Looking for existing extra files in {0}", movie.Path);

            var extraFiles = new List<OtherExtraFile>();
            var filterResult = FilterAndClean(movie, filesOnDisk, importedFiles, fileNameBeforeRename is not null);

            // Same rule as importing extra files with a download: only when Import Extra Files
            // is on, and only the extensions listed there. Without it every file in the movie
            // folder that parses (logo.png, clearart.png, ...) was claimed and then deleted
            // with the movie file on an upgrade.
            var wantedExtensions = WantedExtensions();

            // Forget entries an earlier scan made for files that no longer qualify.
            // Only the database rows go, the files stay on disk.
            var stale = filterResult.PreviouslyImported.Where(f => !wantedExtensions.Contains(f.Extension)).ToList();

            if (stale.Any())
            {
                _logger.Debug("Forgetting {0} extra files that Import Extra Files does not cover", stale.Count);
                _otherExtraFileService.DeleteMany(stale.Select(f => f.Id));
            }

            var previouslyImported = filterResult.PreviouslyImported.Except(stale).ToList();

            foreach (var possibleExtraFile in filterResult.FilesOnDisk)
            {
                var extension = Path.GetExtension(possibleExtraFile);

                if (extension.IsNullOrWhiteSpace())
                {
                    _logger.Debug("No extension for file: {0}", possibleExtraFile);
                    continue;
                }

                if (!wantedExtensions.Contains(extension))
                {
                    _logger.Debug("Extension not in Import Extra Files: {0}", possibleExtraFile);
                    continue;
                }

                var minimalInfo = _parsingService.ParseMinimalPathMovieInfo(possibleExtraFile);

                if (minimalInfo == null)
                {
                    _logger.Debug("Unable to parse extra file: {0}", possibleExtraFile);
                    continue;
                }

                var extraFile = new OtherExtraFile
                {
                    MovieId = movie.Id,
                    MovieFileId = movie.MovieFileId,
                    RelativePath = movie.Path.GetRelativePath(possibleExtraFile),
                    Extension = extension
                };

                extraFiles.Add(extraFile);
            }

            _logger.Info("Found {0} existing other extra files", extraFiles.Count);
            _otherExtraFileService.Upsert(extraFiles);

            // Return files that were just imported along with files that were
            // previously imported so previously imported files aren't imported twice
            return extraFiles.Concat(previouslyImported);
        }

        private HashSet<string> WantedExtensions()
        {
            if (!_configService.ImportExtraFiles)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return _configService.ExtraFileExtensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                     .Select(e => e.Trim(' ', '.').Insert(0, "."))
                                                     .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
