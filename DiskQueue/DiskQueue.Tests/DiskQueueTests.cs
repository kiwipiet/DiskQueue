using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DiskQueue.Tests
{
    public class DiskQueueTests : IDisposable
    {
        private const string Dir = "dir";
        private const string DeepDir = "secondDir\\test";
        private DiskQueue _queue;

        public DiskQueueTests()
        {
            EmptyDirs(Dir, DeepDir);
            _queue = new DiskQueue(Dir);
        }

        [Fact]
        public void Should_create_base_directory()
        {
            Assert.True(Directory.Exists(Dir));
        }

        [Fact]
        public void Should_enqueue()
        {
            _queue.Enqueue("test");
            var filename = GetFirstDirFile(Dir);
            var text = File.ReadAllText(Path.Combine(Dir, filename));
            Assert.Equal("\"test\"", text);
        }

        [Fact]
        public void Mutex_names_dont_allow_backslash()
        {
            _queue = new DiskQueue(DeepDir);
            _queue.Enqueue("test");
            var filename = GetFirstDirFile(DeepDir);
            var text = File.ReadAllText(Path.Combine(DeepDir, filename));
            Assert.Equal("\"test\"", text);
        }

        [Fact]
        public void Should_dequeue()
        {
            _queue.Enqueue("test");
            var item = _queue.Dequeue<string>();
            Assert.Equal("test", item);
        }

        [Fact]
        public void Should_count_queue_length()
        {
            _queue.Enqueue("teste");
            _queue.Enqueue("teste");
            _queue.Enqueue("teste");
            Assert.Equal(3, _queue.GetQueueInfo().Enqueued);
        }

        [Fact]
        public void When_queue_is_empty__returns_null()
        {
            _queue.Enqueue("teste");
            _queue.Dequeue<string>();
            var item = _queue.Dequeue<string>();
            Assert.Null(item);
        }

        private static void EmptyDirs(params string[] dirs)
        {
            foreach (var d in dirs)
            {
                var dir = new DirectoryInfo(d);
                if (!dir.Exists)
                {
                    continue;
                }
                dir.EnumerateFiles().ToList().ForEach(f => f.Delete());
                dir.Delete();
            }
        }

        private static string GetFirstDirFile(string dir)
        {
            var d = new DirectoryInfo(dir);
            return d.EnumerateFiles().First().Name;
        }

        public void Dispose()
        {
            EmptyDirs(Dir, DeepDir);
            _queue.Dispose();
        }
    }
}
