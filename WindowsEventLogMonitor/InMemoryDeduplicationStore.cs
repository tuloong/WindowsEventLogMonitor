using System;
using System.Collections.Generic;

namespace WindowsEventLogMonitor
{
    /// <summary>
    /// 内存中去重存储 - 使用HashSet存储已推送的日志ID
    /// 服务重启后数据会丢失，允许少量重复推送
    /// 带容量上限的 FIFO 淘汰策略，避免长期运行内存无限增长
    /// </summary>
    public class InMemoryDeduplicationStore
    {
        private const int DefaultCapacity = 10000;
        private readonly HashSet<string> _pushedLogIds;
        private readonly Queue<string> _insertionOrder;
        private readonly object _lock;
        private readonly int _capacity;

        public InMemoryDeduplicationStore(int capacity = DefaultCapacity)
        {
            _capacity = Math.Max(1, capacity);
            _pushedLogIds = new HashSet<string>();
            _insertionOrder = new Queue<string>();
            _lock = new object();
        }

        /// <summary>
        /// 尝试添加日志ID
        /// </summary>
        /// <param name="logId">日志唯一ID</param>
        /// <returns>true=新ID已添加, false=ID已存在</returns>
        public bool TryAdd(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId))
                return false;

            lock (_lock)
            {
                if (!_pushedLogIds.Add(logId))
                    return false;

                _insertionOrder.Enqueue(logId);

                // 超过容量时按 FIFO 淘汰最早的记录
                while (_pushedLogIds.Count > _capacity && _insertionOrder.Count > 0)
                {
                    var oldest = _insertionOrder.Dequeue();
                    _pushedLogIds.Remove(oldest);
                }
                return true;
            }
        }

        /// <summary>
        /// 检查日志ID是否已存在
        /// </summary>
        public bool Contains(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId))
                return false;

            lock (_lock)
            {
                return _pushedLogIds.Contains(logId);
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _pushedLogIds.Clear();
                _insertionOrder.Clear();
            }
        }

        /// <summary>
        /// 当前存储的ID数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _pushedLogIds.Count;
                }
            }
        }

        /// <summary>
        /// 容量上限
        /// </summary>
        public int Capacity => _capacity;
    }
}
