using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FtpAzureLoadTest
{
    public class FtpDirect
    {
        private string _FtpServer;
        private string _Login;
        private string _Passwd;
        private int connectionTimeoutSeconds = 60;
        private int readWriteTimeoutSeconds = 60 * 5;
        private int connectionLimit = 20;
        private string connectionGroupName = "FTPService";
        private bool useKeepAlive = true;


        public FtpDirect(string FtpServer, string login, string password)
        {
            _FtpServer = FtpServer;
            _Login = login;
            _Passwd = password;
        }


        public async Task<Stream> DownloadFileStreamAsync(string FilePath, CancellationToken ctoken)
        {
            if (!this._FtpServer.ToLower().StartsWith("ftp://"))
            {
                this._FtpServer = string.Format("ftp://{0}", this._FtpServer);
            }

            var request = (FtpWebRequest)WebRequest.Create(Combine(_FtpServer, FilePath));
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.ConnectionGroupName = connectionGroupName;
            request.KeepAlive = useKeepAlive;
            request.ServicePoint.ConnectionLimit = connectionLimit;
            request.Credentials = new NetworkCredential(_Login, _Passwd);
            request.Timeout = connectionTimeoutSeconds * 1000;
            request.ReadWriteTimeout = readWriteTimeoutSeconds * 1000;
            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            {
                using (var stream = response.GetResponseStream())
                {
                    var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    response.Close();
                    return ms;
                }
            }
        }
        public static string Combine(string path1, string path2)
        {
            char[] filesep = @"/\".ToCharArray();
            return path1.TrimEnd(filesep) + "/" + path2.TrimStart(filesep);
        }
    }
}
