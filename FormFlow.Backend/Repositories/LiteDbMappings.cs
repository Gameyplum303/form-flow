using FormFlow.Backend.Auth;
using FormFlow.Data.Models;
using LiteDB;

namespace FormFlow.Backend.Repositories
{
    /// <summary>
    /// LiteDB builds the mapping for a type the first time it stores or queries one, in
    /// <see cref="BsonMapper.Global"/>, which every database in the process shares. A mapping is
    /// visible to other threads before it is finished, so two threads using a type for the first
    /// time can fail with errors such as "Member Username not found". Building every mapping once,
    /// before any collection is used, avoids that.
    /// </summary>
    public static class LiteDbMappings
    {
        static LiteDbMappings()
        {
            // Nested classes get mappings of their own, so they are listed too.
            BsonMapper.Global.Entity<QuestionDefinition>();
            BsonMapper.Global.Entity<Option>();
            BsonMapper.Global.Entity<VisibleIf>();
            BsonMapper.Global.Entity<SurveyDefinition>();
            BsonMapper.Global.Entity<SurveyResponse>();
            BsonMapper.Global.Entity<AdminUser>();
            BsonMapper.Global.Entity<AccountToken>();
        }

        /// <summary>Builds the mappings on first call. Other threads calling it wait until they are done.</summary>
        public static void EnsureBuilt()
        {
        }
    }
}
