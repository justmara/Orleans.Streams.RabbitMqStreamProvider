﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orleans.Providers.Streams.Common;
using Orleans.Streams;
using Orleans.Streams.Cache;

namespace RabbitMqStreamTests
{
    [TestClass]
    public class CacheTests
    {
        [TestMethod]
        public async Task RunCacheReadHeavyUsageTestMultipleTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                await TestOrleansCacheReadHeavyUsage();
            }
        }

        [TestMethod]
        public async Task RunCacheWriteHeavyUsageTestMultipleTimes()
        {
            for (int i = 0; i < 10; i++)
            {
                await TestOrleansCacheWriteHeavyUsage();
            }
        }

        private async Task TestOrleansCacheReadHeavyUsage()
        {
            var queueCache = new ConcurrentQueueCache(CacheSize);

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                queueCache.TryPurgeFromCache(out var purgedItems);
                
                var multiBatch = GetQueueMessages(new Random().Next(queueCache.GetMaxAddCount()));
                queueCache.AddToCache(multiBatch);

                if (multiBatch.Count > 0)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using (var cursor = queueCache.GetCacheCursor(StreamIdentity, multiBatch.First().SequenceToken))
                        {
                            while (cursor.MoveNext())
                            {
                                var batch = cursor.GetCurrent(out var ignore);
                                await Task.Delay(TimeSpan.FromMilliseconds(50 + new Random().Next(100)));
                            }
                        }
                    }));
                }
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            await Task.WhenAll(tasks);
            queueCache.TryPurgeFromCache(out var finalPurgedItems);
            
            Assert.AreEqual(CacheSize, queueCache.GetMaxAddCount());
        }

        private async Task TestOrleansCacheWriteHeavyUsage()
        {
            var queueCache = new ConcurrentQueueCache(CacheSize);

            var tasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                queueCache.TryPurgeFromCache(out var purgedItems);

                var multiBatch = GetQueueMessages(queueCache.GetMaxAddCount());
                queueCache.AddToCache(multiBatch);

                if (multiBatch.Count > 0)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using (var cursor = queueCache.GetCacheCursor(StreamIdentity, multiBatch.First().SequenceToken))
                        {
                            while (cursor.MoveNext())
                            {
                                var batch = cursor.GetCurrent(out var ignore);
                                await Task.Delay(TimeSpan.FromMilliseconds(50 + new Random().Next(100)));
                            }
                        }
                    }));
                }
            }

            await Task.WhenAll(tasks);
            queueCache.TryPurgeFromCache(out var finalPurgedItems);

            Assert.AreEqual(CacheSize, queueCache.GetMaxAddCount());
        }

        private const int CacheSize = 50;
        
        private static readonly StreamIdentity StreamIdentity = new StreamIdentity(Guid.Empty, "test");
        private static ulong _deliveryTag = 0;

        private static IList<IBatchContainer> GetQueueMessages(int count)
        {
            return Enumerable.Range(0, count)
                .Select(i =>
                    new RabbitMqBatchContainer(
                        StreamIdentity.Guid, StreamIdentity.Namespace,
                        new[] {"value"}.ToList<object>(),
                        new Dictionary<string, object>())
                    {
                        DeliveryTag = _deliveryTag++,
                        EventSequenceToken = new EventSequenceToken((long) _deliveryTag)
                    })
                .ToList<IBatchContainer>();
        }
    }
}