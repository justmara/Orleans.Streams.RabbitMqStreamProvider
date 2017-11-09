﻿using System;

namespace Orleans.Streams.Cache
{
    public class ConcurrentQueueCacheCursor : IQueueCacheCursor
    {
        private readonly Func<RabbitMqBatchContainer> _moveNext;
        private readonly Action<RabbitMqBatchContainer> _purgeItem;

        private RabbitMqBatchContainer _current;

        public ConcurrentQueueCacheCursor(Func<RabbitMqBatchContainer> moveNext, Action<RabbitMqBatchContainer> purgeItem)
        {
            _moveNext = moveNext ?? throw new ArgumentNullException(nameof(moveNext));
            _purgeItem = purgeItem ?? throw new ArgumentNullException(nameof(purgeItem));
        }

        public void Dispose()
        {
            _purgeItem(_current);
            _current = null;
        }

        public IBatchContainer GetCurrent(out Exception exception)
        {
            exception = null;
            return _current;
        }

        public bool MoveNext()
        {
            _purgeItem(_current);
            _current = _moveNext();
            return _current != null;
        }

        public void Refresh(StreamSequenceToken token)
        {
            // do nothing
        }

        public void RecordDeliveryFailure()
        {
            if (_current != null)
            {
                _current.DeliveryFailure = true;
            }
        }
    }
}