using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Others
{
    [TestFixture]
    public class ExistingOtherExtraImporterFixture : CoreTest<ExistingOtherExtraImporter>
    {
        private Movie _movie;
        private List<OtherExtraFile> _existing;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(m => m.Id = 1)
                                   .With(m => m.MovieFileId = 2)
                                   .With(m => m.Path = @"C:\Test\Movies\Movie Title (2020)".AsOsAgnostic())
                                   .Build();

            _existing = new List<OtherExtraFile>();

            Mocker.GetMock<IExtraFileService<OtherExtraFile>>()
                  .Setup(s => s.GetFilesByMovie(_movie.Id))
                  .Returns(() => _existing);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseMinimalPathMovieInfo(It.IsAny<string>()))
                  .Returns(new ParsedMovieInfo());
        }

        private void GivenImportExtraFiles(bool enabled, string extensions)
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.ImportExtraFiles).Returns(enabled);
            Mocker.GetMock<IConfigService>().SetupGet(s => s.ExtraFileExtensions).Returns(extensions);
        }

        private List<ExtraFile> Process(params string[] fileNames)
        {
            var files = fileNames.Select(f => Path.Combine(_movie.Path, f)).ToList();

            return Subject.ProcessFiles(_movie, files, new List<string>(), null).ToList();
        }

        [Test]
        public void should_not_claim_files_when_import_extra_files_is_off()
        {
            GivenImportExtraFiles(false, "srt,png");

            Process("logo.png", "Movie Title (2020).srt").Should().BeEmpty();
        }

        [Test]
        public void should_only_claim_listed_extensions()
        {
            GivenImportExtraFiles(true, "srt, .STRM");

            var result = Process("logo.png", "clearart.png", "Movie Title (2020).strm");

            result.Should().ContainSingle();
            result.Single().Extension.Should().Be(".strm");
        }

        [Test]
        public void should_forget_files_previously_claimed_that_are_not_listed()
        {
            GivenImportExtraFiles(true, "srt");

            _existing.Add(new OtherExtraFile { Id = 5, MovieId = _movie.Id, RelativePath = "logo.png", Extension = ".png" });
            _existing.Add(new OtherExtraFile { Id = 6, MovieId = _movie.Id, RelativePath = "Movie Title (2020).srt", Extension = ".srt" });

            var result = Process("logo.png", "Movie Title (2020).srt");

            result.Should().ContainSingle(f => f.Id == 6);

            Mocker.GetMock<IExtraFileService<OtherExtraFile>>()
                  .Verify(s => s.DeleteMany(It.Is<IEnumerable<int>>(ids => ids.Contains(5) && !ids.Contains(6))), Times.Once());
        }
    }
}
