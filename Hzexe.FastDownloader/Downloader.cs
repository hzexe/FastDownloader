using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hzexe.FastDownloader
{
    public class Downloader : IDisposable
    {
        IntPtr ptr;
        bool isDownloadSuccess;
        int fileSize = 0;

        static SocketsHttpHandler socketsHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 4,
            AllowAutoRedirect = true,
            //UseProxy =true,
            //Proxy= new WebProxy("127.0.0.1", 8888),
        };

        public void Dispose()
        {
            if (IntPtr.Zero != ptr)
                Marshal.FreeHGlobal(ptr);
        }

        private async Task<int> GetFileLengthAsync(string url)
        {
            using (var client = new HttpClient(socketsHandler, false))
            {
                client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 2);
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                    return -1;
                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    return -2;
                }
                this.fileSize = int.Parse(response.Content.Headers.GetValues("Content-Range").First().Split('/').Last());
                return fileSize;
            }
        }



        private int GetFileLength(string[] mirrors)
        {
            var tasks = mirrors.Select(x => GetFileLengthAsync(x)).ToArray();
            int waitCount = 0;
            while ((waitCount=Task.WaitAny(tasks)) >= 0)
            {
                var completedTasks = tasks.Where(x => x.IsCompleted).ToArray();
                var t = completedTasks.FirstOrDefault(x => x.Result > 0);
                if (t != null)
                    return t.Result;
                else
                {
                    tasks = tasks.Except(completedTasks).ToArray();
                    if (tasks.Length == 0)
                        break;
                }
            }
            throw new Exception("can't get file length");
        }



        /// <summary>
        /// download
        /// </summary>
        /// <param name="maxConnectionsPerHost">mac connections per host</param>
        /// <param name="mirrors">link of mirrors</param>
        public void Download(int maxConnectionsPerHost, params string[] mirrors)
        {
            fileSize = GetFileLength(mirrors);

            //output.SetLength(fileSize);
            if (IntPtr.Zero != ptr)
                Marshal.FreeHGlobal(ptr);
            this.ptr = Marshal.AllocHGlobal(fileSize);

            int perBlockSize = fileSize / maxConnectionsPerHost;
            int blockCount = (int)Math.Ceiling(((double)fileSize) / perBlockSize);
            var option = new ParallelOptions { MaxDegreeOfParallelism = maxConnectionsPerHost };
            CancellationTokenSource[] sources = Enumerable.Repeat(0, blockCount).Select(x => new CancellationTokenSource()).ToArray();
            AutoResetEvent[] events = Enumerable.Repeat(0, blockCount).Select(x => new AutoResetEvent(false)).ToArray();
            List<Task> taskList = new List<Task>();
            for (int i = 0; i < blockCount; i++)
            {
                int startByte = i * perBlockSize;
                int endByte = Math.Min((i + 1) * perBlockSize - 1, fileSize);  //结尾的１字节也要算
                IntPtr fSpan = ptr + startByte;

                CancellationTokenSource source = sources[i];
                AutoResetEvent evt = events[i];
                TaskFactory factory = new TaskFactory(source.Token);
                foreach (var url in mirrors)
                {
                    taskList.Add(factory.StartNew(() =>
                    {
                        try
                        {
                            PartDownloadAsync(url, startByte, endByte, fSpan, socketsHandler, source, evt);
                        }
                        catch (Exception exxx)
                        {

                        }

                    }, source.Token));
                }
            }
            WaitHandle.WaitAll(events);
            Array.ForEach(sources, x => x.Dispose());
        }
        /// <summary>
        /// get download result of ptr
        /// </summary>
        /// <param name="length">data length</param>
        /// <returns>The ptr of data begin</returns>
        public IntPtr GetDownloadData(out int length)
        {
            length = fileSize;
            return ptr;
        }
        /// <summary>
        /// get download data of span
        /// </summary>
        /// <returns></returns>
        public Span<byte> GetDownloadData()
        {
            unsafe
            {
                return new Span<byte>(ptr.ToPointer(), fileSize);
            }
        }


        static void PartDownloadAsync(string url, int startByte, int endByte,
            IntPtr dest, SocketsHttpHandler handler,
            CancellationTokenSource cancellationTokenSource,
            AutoResetEvent evt
            )
        {
            var client = new HttpClient(handler, false);
            try
            {
                client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(startByte, endByte);
                var response = client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationTokenSource.Token).Result;
                if (!response.IsSuccessStatusCode)
                    throw new System.Net.Http.HttpRequestException("First mirrors return " + response.StatusCode);
                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    client.Dispose();
                    cancellationTokenSource.Token.WaitHandle.WaitOne();
                }
                var data = response.Content.ReadAsByteArrayAsync().Result;
                Marshal.Copy(data, 0, dest, data.Length);
                //有成功的了，取消
                cancellationTokenSource.Cancel(true);
                evt.Set();
            }
            catch (AggregateException ae)
            {
                //todo:
            }
            catch (Exception ex)
            {
                //todo:
            }

        }

    }
}
