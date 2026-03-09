using System;
using System.Collections.Generic;

namespace WindowsEventLogMonitor
{
    /// <summary>
    /// 内存中去重存储 - 使用HashSet存储已推送的日志ID
    /// 服务重启后数据会丢失，允许少量重复推送
    /// </summary>
    public class InMemoryDeduplicationStore
    {
        private readonly HashSet<string> _pushedLogIds;
        private readonly object _lock;

        public InMemoryDeduplicationStore()
        {
            _pushedLogIds = new HashSet<string>();
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
                return _pushedLogIds.Add(logId);
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
    }
}
