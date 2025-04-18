namespace SolutionBundler
{
    public class FileSettings
    {
        public List<string>? ProjectExtensions { get; set; }
        public List<string>? TextFileExtensions { get; set; }
        public List<string>? BinaryFileExtensions { get; set; }
        public List<string>? ExcludedDirectories { get; set; }
    }
}
