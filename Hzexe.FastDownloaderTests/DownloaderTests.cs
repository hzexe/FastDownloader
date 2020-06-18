using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hzexe.FastDownloader;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hzexe.FastDownloader.Tests
{
    [TestClass()]
    public class DownloaderTests
    {
        const uint CRC32 = 0xADD94858;
        Downloader downloader;

        [TestInitialize]
        public void init()
        {

            downloader = new Downloader();
        }

        [TestCleanup]
        [TestMethod()]
        public void Dispose()
        {
            downloader.Dispose();
        }

        [TestMethod()]
        public void DownloadAsyncTest()
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            var ordata = wc.DownloadData("http://mirrors.163.com/kernel/v5.x/ChangeLog-5.0");
            wc.Dispose();
            uint ocrc = Crc32C.Crc32CAlgorithm.Compute(ordata);
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            downloader.Download(4,
                "http://mirrors.163.com/kernel/v5.x/ChangeLog-5.0",
                "https://mirrors.tuna.tsinghua.edu.cn/kernel/v5.x/ChangeLog-5.0",
                 "http://linux-kernel.uio.no/pub/linux/kernel/v5.x/ChangeLog-5.0",
                 "https://mirrors.aliyun.com/linux-kernel/v5.x/ChangeLog-5.0"
                );
            sw.Stop();
            var time1 = sw.ElapsedMilliseconds;
            sw.Restart();
            downloader.Download(4,
                "http://mirrors.163.com/kernel/v5.x/ChangeLog-5.0",
                "https://mirrors.tuna.tsinghua.edu.cn/kernel/v5.x/ChangeLog-5.0",
                 "http://linux-kernel.uio.no/pub/linux/kernel/v5.x/ChangeLog-5.0",
                 "https://mirrors.aliyun.com/linux-kernel/v5.x/ChangeLog-5.0"
                );
            sw.Stop();
            var time2 = sw.ElapsedMilliseconds;
            var data = downloader.GetDownloadData().ToArray();
            uint crc = Crc32C.Crc32CAlgorithm.Compute(data);
            Assert.AreEqual(ocrc, crc);

            IntPtr ptr = downloader.GetDownloadData(out var length);
            Assert.IsTrue(length > 0);
        }

       
    }
}