namespace FtpAzureLoadTest
{
    public class FtpAzureLoadTestSettings
    {
        public string host { get; set; }
        public string login { get; set; }
        public string pass { get; set; }
        public string testdir { get; set; }
        public bool testDL { get; set; } = false;
        public int concurrencyLevel { get; set; } = 1;

    }
}
