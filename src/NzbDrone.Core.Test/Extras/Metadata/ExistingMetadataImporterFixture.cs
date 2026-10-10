using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Metadata
{
    [TestFixture]
    public class ExistingMetadataImporterFixture : CoreTest<ExistingMetadataImporter>
    {
        private Movie _movie;
        private string _folderJpg;
        private List<MetadataFile> _existing;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(m => m.Id = 1)
                                   .With(m => m.MovieFileId = 2)
                                   .With(m => m.Path = @"C:\Test\Movies\Movie Title (2020)".AsOsAgnostic())
                                   .Build();

            _folderJpg = Path.Combine(_movie.Path, "folder.jpg");
            _existing = new List<MetadataFile>();

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Setup(s => s.GetFilesByMovie(_movie.Id))
                  .Returns(() => _existing);

            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.Enabled())
                  .Returns(new List<IMetadata>());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseMinimalPathMovieInfo(It.IsAny<string>()))
                  .Returns(new ParsedMovieInfo());
        }

        private Mock<IMetadata> GivenEnabledConsumer()
        {
            var consumer = new Mock<IMetadata>();

            consumer.Setup(c => c.FindMetadataFile(It.IsAny<Movie>(), It.IsAny<string>()))
                    .Returns<Movie, string>((m, path) => new MetadataFile
                    {
                        MovieId = m.Id,
                        Consumer = consumer.Object.GetType().Name,
                        Type = MetadataType.MovieImage,
                        RelativePath = Path.GetFileName(path)
                    });

            Mocker.GetMock<IMetadataFactory>()
                  .Setup(s => s.Enabled())
                  .Returns(new List<IMetadata> { consumer.Object });

            return consumer;
        }

        private List<ExtraFile> Process()
        {
            return Subject.ProcessFiles(_movie, new List<string> { _folderJpg }, new List<string>(), null).ToList();
        }

        [Test]
        public void should_not_claim_files_when_no_metadata_type_is_enabled()
        {
            Process().Should().BeEmpty();

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Verify(s => s.Upsert(It.Is<List<MetadataFile>>(l => l.Count == 0)), Times.Once());
        }

        [Test]
        public void should_claim_files_for_an_enabled_metadata_type()
        {
            GivenEnabledConsumer();

            Process().Should().HaveCount(1);
        }

        [Test]
        public void should_forget_files_previously_claimed_by_a_type_that_is_not_enabled()
        {
            _existing.Add(new MetadataFile { Id = 7, MovieId = _movie.Id, Consumer = "XbmcMetadata", RelativePath = "folder.jpg" });

            Process().Should().BeEmpty();

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Verify(s => s.DeleteMany(It.Is<IEnumerable<int>>(ids => ids.Contains(7))), Times.Once());
        }

        [Test]
        public void should_keep_files_previously_claimed_by_an_enabled_type()
        {
            var consumer = GivenEnabledConsumer();

            _existing.Add(new MetadataFile { Id = 8, MovieId = _movie.Id, Consumer = consumer.Object.GetType().Name, RelativePath = "folder.jpg" });

            Process().Should().ContainSingle(f => f.Id == 8);

            Mocker.GetMock<IExtraFileService<MetadataFile>>()
                  .Verify(s => s.DeleteMany(It.Is<IEnumerable<int>>(ids => ids.Contains(8))), Times.Never());
        }
    }
}
