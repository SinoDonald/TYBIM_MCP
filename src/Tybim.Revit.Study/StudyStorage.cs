using System.IO;
namespace Tybim.Revit.Study
{
    public static class StudyStorage
    {
#if REVIT2027
        public static string RootName => Path.Combine("TYBIM", "McpStudy", "2027");
#elif REVIT2026
        public static string RootName => Path.Combine("TYBIM", "McpStudy", "2026");
#elif REVIT2025
        public static string RootName => Path.Combine("TYBIM", "McpStudy", "2025");
#else
        public static string RootName => Path.Combine("TYBIM", "McpStudy", "2024");
#endif
    }
}
