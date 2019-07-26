namespace AppVeyor.POCOs
{
    public class GetJobID
    {
        public Build Build { get; set; }
    }
    public class Build
    {
        public Jobs[] Jobs { get; set; }
        public string Message { get; set; }
        public string AuthorUserName { get; set; }
        public string CommitId { get; set; }
    }
    public class Jobs
    {
        public string JobId { get; set; }
    }

    public class GetFilesFromJob
    {
        public string FileName { get; set; }
    }
}
