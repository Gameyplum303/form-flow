using System;
using FluentAssertions;
using FormFlow.Backend.Repositories;
using FormFlow.Data.Models;
using LiteDB;
using Xunit;

namespace FormFlow.Backend.Tests.Repositories
{
    public class SurveyRepositoryTests
    {
        private ILiteDatabase CreateInMemoryDb()
        {
            return new LiteDatabase("Filename=:memory:");
        }

        [Fact]
        public void Repository_Initializes_Surveys_Collection()
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);

            repo.FindAll().Should().BeEmpty();
            SurveyRepository.CollectionName.Should().Be("surveys");
        }

        [Fact]
        public void Repository_Creates_Unique_Id_Index()
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);

            // Insert a survey
            var survey = new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = "Test Survey",
                Description = "desc",
                QuestionIds = new(),
                CreatedAt = DateTime.UtcNow
            };

            repo.Insert(survey);

            // Attempt duplicate insert
            Action act = () => repo.Insert(survey);

            act.Should().Throw<LiteException>();
        }

        [Fact]
        public void Repository_Can_Insert_And_Retrieve_Survey()
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);

            var survey = new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = "Customer Feedback",
                Description = "Basic survey",
                QuestionIds = [Guid.NewGuid(), Guid.NewGuid()],
                CreatedAt = DateTime.UtcNow
            };

            repo.Insert(survey);

            var retrieved = repo.FindById(survey.Id);

            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(survey.Id);
            retrieved.Title.Should().Be("Customer Feedback");
            retrieved.QuestionIds.Should().HaveCount(2);
        }

        [Fact]
        public void Repository_Returns_Null_For_Missing_Survey()
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);

            var result = repo.FindById(Guid.NewGuid());

            result.Should().BeNull();
        }

        [Theory]
        [InlineData("k7m2p9qa")]
        [InlineData("K7M2P9QA")]
        [InlineData(" k7m2 p9qa ")]
        [InlineData("K7M2-P9QA")]
        public void FindByShareCode_IgnoresCaseSpacesAndDashes(string typed)
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);
            var survey = repo.Insert(new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = "Lecture poll",
                Description = "desc",
                QuestionIds = [],
                CreatedAt = DateTime.UtcNow,
                ShareCode = "k7m2p9qa",
            });

            repo.FindByShareCode(typed)!.Id.Should().Be(survey.Id);
            repo.FindByShareCode("k7m2p9qb").Should().BeNull();
            repo.FindByShareCode(" ").Should().BeNull();
        }

        [Fact]
        public void ShowResultsToRespondents_IsStored_AndOffForOlderSurveys()
        {
            using var db = CreateInMemoryDb();
            var repo = new SurveyRepository(db);
            var shared = repo.Insert(new SurveyDefinition
            {
                Id = Guid.NewGuid(),
                Title = "Shared",
                Description = "desc",
                QuestionIds = [],
                CreatedAt = DateTime.UtcNow,
                ShowResultsToRespondents = true,
            });
            // A survey stored before the setting existed has no field for it.
            var older = Guid.NewGuid();
            db.GetCollection(SurveyRepository.CollectionName).Insert(new BsonDocument
            {
                ["_id"] = older,
                ["Title"] = "Older",
                ["Description"] = "desc",
                ["QuestionIds"] = new BsonArray(),
                ["CreatedAt"] = DateTime.UtcNow,
            });

            repo.FindById(shared.Id)!.ShowResultsToRespondents.Should().BeTrue();
            repo.FindById(older)!.ShowResultsToRespondents.Should().BeFalse();
        }
    }
}
