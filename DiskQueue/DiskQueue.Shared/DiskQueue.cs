﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiskQueue.Core;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Splat;

namespace DiskQueue
{
    public class DiskQueue : IDiskQueue, IEnableLogger
    {
        private readonly string _directoryPath;
        private const string CreatingExtension = ".init";
        private const string OnQueueExtension = ".item";
        private const string WorkingExtension = ".working";
        private const string ErrorExtension = ".error";
        private readonly Mutex _mutex;
        private readonly RetryPolicy _retryPolicy;

        public DiskQueue(string directoryPath)
        {
            _retryPolicy = Policy
              .Handle<Exception>()
              .Retry(3, (exception, retryCount, context) =>
              {
                  var methodThatRaisedException = context["methodName"];
                  this.Log().DebugException(methodThatRaisedException?.ToString(), exception);
              });

            _directoryPath = directoryPath;
            Directory.CreateDirectory(directoryPath);
            _mutex = new Mutex(false, @"Local\" + _directoryPath.Replace(Path.DirectorySeparatorChar, '_'));
        }

        public void Enqueue<T>(T item) where T : class
        {
            var path = Path.Combine(_directoryPath,
                $"{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid()}") + CreatingExtension;
            var text = JsonConvert.SerializeObject(item);
            try
            {
                _retryPolicy.Execute(() => File.WriteAllText(path, text, Encoding.UTF8));
                _retryPolicy.Execute(() => File.Move(path, Path.ChangeExtension(path, OnQueueExtension)));
            }
            catch (Exception ex)
            {
                throw new DiskQueueException($"Could not enqueue item: {item}", ex);
            }
        }

        public T Dequeue<T>() where T : class
        {
            string item;
            try
            {
                _mutex.WaitOne();
                item = LockItem();
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
            item = item ?? "";
            if (!File.Exists(item))
            {
                return null;
            }
            try
            {
                var text = ReadTheFile(item);
                return JsonConvert.DeserializeObject<T>(text);
            }
            catch (Exception ex)
            {
                _retryPolicy.Execute(() => File.Move(item, Path.ChangeExtension(item, ErrorExtension)));
                throw new DiskQueueException($"Could not dequeue item: {item}", ex);
            }
            finally
            {
                _retryPolicy.Execute(() => File.Delete(item));
            }
        }
        public T Peek<T>() where T : class
        {
            string item = GetNextInQueue();
            item = item ?? "";
            if (!File.Exists(item))
            {
                return null;
            }
            try
            {
                var text = ReadTheFile(item);
                return JsonConvert.DeserializeObject<T>(text);
            }
            catch (Exception ex)
            {
                _retryPolicy.Execute(() => File.Move(item, Path.ChangeExtension(item, ErrorExtension)));
                throw new DiskQueueException($"Could not dequeue item: {item}", ex);
            }
        }

        private static string ReadTheFile(string fullpath)
        {
            while (true)
            {
                try
                {
                    return File.ReadAllText(fullpath, Encoding.UTF8);
                }
                catch (Exception)
                {
                    Task.Delay(100).Wait();
                }
            }
        }

        public QueueInfo GetQueueInfo()
        {
            var info = new DirectoryInfo(_directoryPath);
            return new QueueInfo(
                info.GetFiles("*" + OnQueueExtension).Length,
                info.GetFiles("*" + ErrorExtension).Length);
        }

        public void CleanQueue()
        {
            try
            {
                _mutex.WaitOne();
                DeleteFiles("*" + OnQueueExtension);
                DeleteFiles("*" + ErrorExtension);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        private void DeleteFiles(string match)
        {
            var info = new DirectoryInfo(_directoryPath);
            foreach (var file in info.EnumerateFiles(match))
            {
                file.Delete();
            }
        }

        private string LockItem()
        {
            while (true)
            {
                try
                {
                    var filename = TryRenameNextOnQueue();
                    return filename;
                }
                catch (Exception)
                {
                    Task.Delay(100).Wait();
                }
            }
        }

        private string GetNextInQueue()
        {
            var info = new DirectoryInfo(_directoryPath);
            var fileInfo = info.GetFiles("*" + OnQueueExtension).OrderBy(p => p.CreationTime).ThenBy(p => p.Name).FirstOrDefault();
            return fileInfo?.FullName;
        }
        private string TryRenameNextOnQueue()
        {
            var info = new DirectoryInfo(_directoryPath);
            var fileInfo = info.GetFiles("*" + OnQueueExtension).OrderBy(p => p.CreationTime).ThenBy(p => p.Name).FirstOrDefault();
            if (fileInfo == null)
            {
                return null;
            }
            var newFilename = Path.Combine(_directoryPath, Path.ChangeExtension(fileInfo.Name, WorkingExtension));
            fileInfo.MoveTo(newFilename);
            return newFilename;
        }

        public void Dispose()
        {
            _mutex.Dispose();
        }
    }
}
