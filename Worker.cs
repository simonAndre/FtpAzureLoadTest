using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace FtpAzureLoadTest
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly IOptions<FtpAzureLoadTestSettings> _options;
        private Dictionary<string, byte[]> _workingdata;
        public Worker(ILogger<Worker> logger, TelemetryClient tc, IOptions<FtpAzureLoadTestSettings> options)
        {
            _logger = logger;
            _telemetryClient = tc;
            this._options = options;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _workingdata = await loaddatafromdisk();
            _logger.LogInformation($"Starting Ftp Azure Test with concurrency level of {_options.Value.concurrencyLevel}");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    await FtpExplorationRunAsync(stoppingToken);
                    Parallel.For(0, _options.Value.concurrencyLevel, async i => await FtpExplorationRunAsync(stoppingToken));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    throw;
                }

                await Task.Delay(1000, stoppingToken);
            }
        }

        public enum typetest
        {
            variabletext = 1,
            fixtext = 2,
            fixbinary = 3
        }

        private async Task<Dictionary<string, byte[]>> loaddatafromdisk()
        {
            string[] files = new string[] { "test.txt", "test.xml", "test.gbx" };
            var res = new Dictionary<string, byte[]>();
            var cd = Path.GetDirectoryName(this.GetType().Assembly.Location);
            foreach (var sfile in files)
            {
                using (var fs1 = new FileStream(Path.Combine(cd, "testdata", sfile), FileMode.Open))
                {
                    var tb = new byte[fs1.Length];
                    await fs1.ReadAsync(tb);
                    res.Add(sfile, tb);
                }
            }
            return res;
        }

        private (byte[] content, string filename, typetest typetest) getDataToUploadAsync()
        {
            typetest ttest = (typetest)new Random().Next(1, 3);
            switch (ttest)
            {
                case typetest.variabletext:
                    int filesize1 = new Random().Next(1000, 20000);
                    var tb1 = new byte[filesize1];
                    int start = new Random().Next(0, (int)(_workingdata["test.txt"].Length - filesize1));
                    for (int i = 0; i < filesize1; i++)
                    {
                        tb1[i] = _workingdata["test.txt"][i + start];
                    }
                    return (tb1, $"test_{filesize1}.csv", ttest);
                case typetest.fixtext:
                    return (_workingdata["test.xml"], $"test_{new Random().Next(50000, 70000)}.xml", ttest);
                case typetest.fixbinary:
                    return (_workingdata["test.xml"], $"test_{new Random().Next(100000, 120000)}.xml", ttest);
            }
            throw new NotImplementedException();
        }


        public async Task FtpExplorationRunAsync(CancellationToken token)
        {
            string testdir = this._options.Value.testdir;
            using (var ftp = new FluentFtpService(this._options.Value.host, this._options.Value.login, this._options.Value.pass))
            {
                Stopwatch sw = Stopwatch.StartNew();

                await ftp.CreateDirectoryIfNotExistsAsync(testdir, token);
                _telemetryClient.TrackMetric("createTestDirTime", sw.ElapsedMilliseconds);
                _logger.LogInformation("CreateDirectoryIfNotExistsAsync done");
                sw.Restart();

                var listfiles = await ftp.GetListingAsync(testdir, token);
                _telemetryClient.TrackMetric("rootListingTime", sw.ElapsedMilliseconds);
                _telemetryClient.TrackMetric("testdirSize", listfiles.Count());
                _logger.LogInformation("GetListingAsync done");




                //string ftpfile1 = $"/{testdir}/test3.gbx";
                //var cd1 = Path.GetDirectoryName(this.GetType().Assembly.Location);
                //var newlocalfile1 = Path.Combine(cd1, "testdata", "test3.gbx");


                ////var res=await ftp.DownloadFileAsync(newlocalfile1, ftpfile1,token);

                ////var tb3 = await ftp.GetDataAsync(ftpfile1, token);
                //var tb3 = await ftp.GetDataDirectAsync(ftpfile1, token);

                //var fs3 = new FileStream(newlocalfile1, FileMode.Create);
                //fs3.Write(tb3, 0, tb3.Length);


                var tb1 = getDataToUploadAsync();
                try
                {
                    var ms = new MemoryStream(tb1.content);

                    string ftpfile = $"{testdir}/{tb1.filename}";

                    sw.Restart();
                    await ftp.PushDataAsync(ms, ftpfile, token);
                    _telemetryClient.TrackMetric("PushDataTime", sw.ElapsedMilliseconds);
                    _logger.LogInformation($"PushDataAsync done for file {ftpfile}");

                    Thread.Sleep(1000);
                    if (_options.Value.testDL)
                    {
                        sw.Restart();
                        //var tb2 = await ftp.GetDataAsync(ftpfile, token);
                        var tb2 = await ftp.GetDataAsync(ftpfile, token);

                        if (tb1.content.Length != tb2.Length)
                        {
                            _logger.LogError($"file size error for file {ftpfile}");
                        }
                        else
                            for (int i = 0; i < tb1.content.Length; i++)
                            {
                                if (tb1.content[i] != tb2[i])
                                {
                                    _logger.LogError($"file content error for file {ftpfile} as index {i}/{tb1.content.Length}");
                                    break;
                                }
                            }
                        _telemetryClient.TrackMetric("GetDataTime", sw.ElapsedMilliseconds);
                        _logger.LogInformation("GetDataAsync done");

                        var cd = Path.GetDirectoryName(this.GetType().Assembly.Location);
                        var newlocalfile = Path.Combine(cd, "testdata", tb1.filename);
                        var fs2 = new FileStream(newlocalfile, FileMode.Create);
                        fs2.Write(tb2, 0, tb2.Length);
                    }
                    if (new Random().Next(0, 10) == 5)
                    {
                        //on en supprime 1 sur 10 pour tester la suppresion de temps en temps (laisser grossier le reperotire de travail)
                        sw.Restart();
                        await ftp.DeleteFileAsync(ftpfile, token);
                        _telemetryClient.TrackMetric("DeleteFileAsync", sw.ElapsedMilliseconds);
                        _logger.LogInformation($"DeleteFileAsync done for file {ftpfile}");
                    }
                    _telemetryClient.TrackEvent("FTPtestSuccess", new Dictionary<string, string>() { { "typetest", tb1.typetest.ToString() } });
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    _telemetryClient.TrackEvent("FTPtestError", new Dictionary<string, string>() { { "message", e.Message }, { "typetest", tb1.typetest.ToString() } });
                }
            }
            _telemetryClient.TrackEvent("FtpExplorationRun completed");
            Console.WriteLine("End");
        }
    }
}
