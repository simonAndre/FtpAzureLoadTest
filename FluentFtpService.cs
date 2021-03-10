using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using FluentFTP;
using System.Collections.Generic;

namespace FtpAzureLoadTest
{
    public struct FtpDirectoryContent
    {
        public DateTime date;
        public long taille;
        public string fichier;
    }
    public class FluentFtpService : IDisposable
    {
        private readonly string _host;
        private readonly string _login;
        private readonly string _pass;
        private bool _connected = false;
        private bool disposedValue;
        private FtpClient _client;
        private FtpDirect _ftpdirect;
        public FluentFtpService(string host, string login, string pass)
        {
            this._host = host;
            this._login = login;
            this._pass = pass;
            _client = new FtpClient(_host, _login, _pass);
            _ftpdirect = new FtpDirect(_host, _login, _pass);
        }

        private void log(string message)
        {
            Console.WriteLine(message);
        }


        public async Task ConnectAsync(CancellationToken token)
        {
            if (!_connected)
            {
                _client.UploadDataType = FtpDataType.Binary;
                _client.DownloadDataType = FtpDataType.Binary;
                _client.DataConnectionType = FtpDataConnectionType.PASV;
                await _client.ConnectAsync(token);
                _connected = true;
            }
        }
        public async Task CreateDirectoryAsync(string directory, CancellationToken token)
        {
            await this.ConnectAsync(token);
            await _client.ConnectAsync(token);
            await _client.CreateDirectoryAsync("testdir", true, token);
        }



        /// <summary>
        /// create a directory. do nothing if exists
        /// </summary>
        /// <param name="directory">may be multi-level</param>
        /// <returns></returns>
        public async Task CreateDirectoryIfNotExistsAsync(string directory, CancellationToken token)
        {
            await this.ConnectAsync(token);
            if (!await _client.DirectoryExistsAsync(directory))
            {
                await _client.CreateDirectoryAsync(directory, true, token);
                log($"directory {directory} created on FTP {_host}");
            }
            else
                log($"directory {directory} already exists");
        }


        public async Task PushDataAsync(Stream str, string remotefilepath, CancellationToken token)
        {
            await this.ConnectAsync(token);

            // open an write-only stream to the file
            using (var ostream = await _client.OpenWriteAsync(remotefilepath, FtpDataType.Binary, token))
            {
                try
                {
                    var tb = new byte[str.Length];
                    str.Position = 0;
                    await str.ReadAsync(tb, token);
                    await ostream.WriteAsync(tb, token);
                    //await str.CopyToAsync(ostream);
                    log($"pushed file {remotefilepath} to FTP {_host}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error pushing file {remotefilepath} to FTP {_host}", e);
                }
                finally
                {
                    ostream.Close();
                }
            }
        }
        public async Task<bool> DownloadFileAsync(string localFile, string remotefilepath, CancellationToken token)
        {
            await this.ConnectAsync(token);
            _client.RetryAttempts = 5;
            var status = await _client.DownloadFileAsync(localFile, remotefilepath, FtpLocalExists.Overwrite, FtpVerify.Retry, null, token);

            return status.IsSuccess();
        }
        public async Task<byte[]> GetDataAsync(string remotefilepath, CancellationToken token)
        {
            await this.ConnectAsync(token);

            //var filesize = await _client.GetFileSizeAsync(remotefilepath, token);//ne marche pas en actif
            //log($"filesize of {remotefilepath} is {filesize}");


            using (var istream = await _client.OpenReadAsync(remotefilepath, FtpDataType.Binary, token))
            {
                try
                {
                    var filesize = istream.Length;
                    var tb = new byte[filesize];
                    await istream.ReadAsync(tb, 0, (int)filesize, token);
                    log($"reading file {remotefilepath} (size:{filesize}) from FTP {_host} done");
                    return tb;
                }
                catch (Exception e)
                {
                    throw new Exception($"Error reading file {remotefilepath} from FTP {_host}", e);
                }
                finally
                {
                    istream.Close();
                }
            }
        }
        public async Task<byte[]> GetDataDirectAsync(string remotefilepath, CancellationToken token)
        {
            var str= await _ftpdirect.DownloadFileStreamAsync(remotefilepath, token);
            var filesize = str.Length;
            var tb = new byte[filesize];
            await str.ReadAsync(tb, token);
            return tb;
        }

        public async Task<List<FtpDirectoryContent>> GetListingAsync(string remotedirectory, CancellationToken token)
        {
            await this.ConnectAsync(token);
            List<FtpDirectoryContent> res = new List<FtpDirectoryContent>();
            // get a recursive listing of the files & folders in a specific folder
            foreach (var item in await _client.GetListingAsync(remotedirectory, FtpListOption.Auto, token))
            {
                switch (item.Type)
                {

                    case FtpFileSystemObjectType.Directory:
                        Console.WriteLine("Directory!  " + item.FullName);
                        Console.WriteLine("Modified date:  " + item.Modified);

                        break;

                    case FtpFileSystemObjectType.File:
                        res.Add(new FtpDirectoryContent() { fichier = item.FullName, date = item.Modified, taille = item.Size });

                        //Console.WriteLine("File!  " + item.FullName);
                        //Console.WriteLine("File size:  " + item.Size);// await _client.GetFileSizeAsync(item.FullName, token));

                        break;

                    case FtpFileSystemObjectType.Link:
                        break;
                }
            }
            return res;
        }




        public async Task DeleteFileAsync(string remotefilepath, CancellationToken token)
        {
            await this.ConnectAsync(token);
            await _client.ConnectAsync(token);
            await _client.DeleteFileAsync(remotefilepath);
            log($"file {remotefilepath} has been deleted from FTP {_host}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _client.Dispose();
                }

                // TODO: libérer les ressources non managées (objets non managés) et substituer le finaliseur
                // TODO: affecter aux grands champs une valeur null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Ne changez pas ce code. Placez le code de nettoyage dans la méthode 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }





    }
}
