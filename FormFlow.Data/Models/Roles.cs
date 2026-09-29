namespace FormFlow.Data.Models
{
    /// <summary>
    /// The roles on the site. Administrators run the site and can change anything. Professors and
    /// scientists build surveys and see the answers to the surveys they created. Students take surveys
    /// without an account; their role exists so administrators can preview the site as a student.
    /// </summary>
    public static class Roles
    {
        public const string Admin = "admin";
        public const string Professor = "professor";
        public const string Student = "student";

        public static readonly IReadOnlyList<string> All = [Admin, Professor, Student];

        public static bool IsKnown(string? role) => role is Admin or Professor or Student;

        /// <summary>Roles an account can have. Administrators are added by hand; professors sign up.</summary>
        public static readonly IReadOnlyList<string> ForAccounts = [Admin, Professor];

        public static bool CanHaveAccount(string? role) => role is Admin or Professor;

        /// <summary>Roles that build surveys and questions.</summary>
        public static bool CanBuild(string? role) => role is Admin or Professor;

        public static string DisplayName(string? role) => role switch
        {
            Admin => "Administrator",
            Professor => "Professor/Scientist",
            Student => "Student",
            _ => "Unknown role",
        };
    }
}
