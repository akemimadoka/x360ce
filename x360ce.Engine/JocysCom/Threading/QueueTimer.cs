﻿using JocysCom.ClassLibrary.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;

namespace JocysCom.ClassLibrary.Threading
{

    public partial class QueueTimer : QueueTimer<object>
    {
        public QueueTimer(int delayInterval = 500, int sleepInterval = 5000, ISynchronizeInvoke listSynchronizingObject = null) : base(delayInterval, sleepInterval, listSynchronizingObject)
        {
        }

        public override string DoActionNow(object item = null)
        {
            return base.DoActionNow(item ?? new object());
        }

    }

    /// <summary>
    /// Queue tasks for execution on a single thread in a synchronized order.
    /// </summary>
    public partial class QueueTimer<T> : IDisposable where T : class
    {

        /// <summary>
        /// Initialize new QueryTimer object. Default delay interval = 500 milliseconds. Default sleep interval = 5 seconds.
        /// </summary>
        /// <param name="delayInterval">Delay time between each run. If this value is set then some items won't be added to the queue, in order to avoid clogging.</param>
        /// <param name="sleepInterval">If set then action will auto-run automatically after specified amount of milliseconds.</param>
        public QueueTimer(int delayInterval = 500, int sleepInterval = 5000, ISynchronizeInvoke listSynchronizingObject = null)
        {
            // Create main properties.
            _Queue = new BindingListInvoked<T>();
            _Queue.SynchronizingObject = listSynchronizingObject;
            _LastActionDoneTime = new Stopwatch();
            _LastActionDoneTime.Start();
            queueLock = new object();
            ChangeDelayInterval(delayInterval);
            ChangeSleepInterval(sleepInterval);
            SleepTimerStart();
        }

        /// <summary>If delay timer is set then queue can contain only one item.</summary>
        public BindingListInvoked<T> Queue { get { return _Queue; } }
        BindingListInvoked<T> _Queue;
        object queueLock;

        public bool ProcessImmediately = false;

        /// <summary>Last added item.</summary>
        string lastException;
        DateTime lastExceptionDate;
        long exceptionCount;

        /// <summary>
        /// If SynchronizingObject is set then make sure that handle is created.
        /// var handle = control.Handle; // Creates handle if missing.
        /// var isCreated = control.IsHandleCreated;
        /// You can use 'HandleCreated' event.
        /// </summary>
        public EventHandler<QueueTimerEventArgs> DoWork;

        public event EventHandler<QueueTimerEventArgs> BeforeRemove;

        public ISynchronizeInvoke SynchronizingObject { get; set; }

        #region Status

        public long AddCount { get { return _AddCount; } }
        long _AddCount;

        public long StartCount { get { return _StartCount; } }
        long _StartCount;

        public long ThreadCount { get { return _ThreadCount; } }
        long _ThreadCount;

        public long ActionCount { get { return _ActionCount; } }
        long _ActionCount;

        public long ActionNoneCount { get { return _ActionNoneCount; } }
        long _ActionNoneCount;

        public TimeSpan LastActionDoneTime { get { return _LastActionDoneTime.Elapsed; } }
        Stopwatch _LastActionDoneTime = new Stopwatch();

        /// <summary>Thread action is running.</summary>
        public bool IsRunning { get { return _IsRunning; } }

        bool _IsRunning;

        #endregion

        /// <summary>
        /// Next run by sleep timer.
        /// </summary>
        public DateTime NextRunTime
        {
            get
            {
                return (delayTimerNextRunTime.Ticks == 0 || (sleepTimerNextRunTime.Ticks > 0 && sleepTimerNextRunTime < delayTimerNextRunTime))
                    ? sleepTimerNextRunTime
                    : delayTimerNextRunTime;
            }
        }

        #region Delay Timer

        /// <summary>
        /// Controls how long application must wait between actions.
        /// </summary>
        Timer delayTimer;
        DateTime delayTimerNextRunTime;
        object delayTimerLock = new object();

        public void ChangeDelayInterval(int interval)
        {
            lock (delayTimerLock)
            {
                if (delayTimer != null)
                {
                    delayTimer.Elapsed -= DelayTimer_Elapsed;
                    delayTimer.Dispose();
                    delayTimerNextRunTime = default(DateTime);
                    delayTimer = null;
                }
                if (interval > 0)
                {
                    // Create delay timer.
                    delayTimer = new Timer();
                    delayTimer.AutoReset = false;
                    delayTimer.Interval = interval;
                    delayTimer.Elapsed += DelayTimer_Elapsed;
                }
            }
        }

        public void DelayTimerStop()
        {
            lock (delayTimerLock)
            {
                if (delayTimer != null)
                {
                    delayTimer.Stop();
                    delayTimerNextRunTime = default(DateTime);
                }
            }
        }

        public void DelayTimerStart()
        {
            lock (delayTimerLock)
            {
                if (delayTimer != null)
                {
                    delayTimerNextRunTime = DateTime.Now.AddMilliseconds(delayTimer.Interval);
                    delayTimer.Start();
                }
            }
        }

        void DelayTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (delayTimerLock)
            {
                delayTimerNextRunTime = default(DateTime);
            }
            if (IsDisposing) return;
            lock (queueLock)
            {
                _StarThread();
            }
        }

        #endregion

        #region Sleep Timer

        /// <summary>
        /// Controls how long application must sleep if last action finished without doing anything.
        /// </summary>
        Timer sleepTimer;
        DateTime sleepTimerNextRunTime;
        object sleepTimerLock = new object();

        public void ChangeSleepInterval(int interval)
        {
            lock (queueLock)
            {
                if (sleepTimer != null)
                {
                    sleepTimer.Elapsed -= SleepTimer_Elapsed;
                    sleepTimer.Dispose();
                    sleepTimerNextRunTime = default(DateTime);
                    sleepTimer = null;
                }
                if (interval > 0)
                {
                    // Create delay timer.
                    sleepTimer = new Timer();
                    sleepTimer.AutoReset = false;
                    sleepTimer.Interval = interval;
                    sleepTimer.Elapsed += SleepTimer_Elapsed;
                }
            }
        }

        void SleepTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (sleepTimerLock)
            {
                sleepTimerNextRunTime = default(DateTime);
            }
            if (IsDisposing) return;
            lock (queueLock)
            {
                _StarThread();
            }
        }


        public void SleepTimerStop()
        {
            lock (sleepTimerLock)
            {
                if (sleepTimer != null)
                {
                    sleepTimer.Stop();
                    sleepTimerNextRunTime = default(DateTime);
                }
            }
        }

        public void SleepTimerStart()
        {
            lock (sleepTimerLock)
            {
                if (sleepTimer != null)
                {
                    sleepTimerNextRunTime = DateTime.Now.AddMilliseconds(sleepTimer.Interval);
                    sleepTimer.Start();
                }
            }
        }

        #endregion

        delegate void InvokeDelegate();

        public virtual string DoActionNow(T item = null)
        {
            var so = Queue.SynchronizingObject;
            if (so != null)
            {
                return (string)so.Invoke((InvokeDelegate)delegate ()
                {
                    _DoActionNow(item);
                }, new object[0]);
            }
            else
            {
                return _DoActionNow(item);
            }
        }

        /// <summary>
        /// Trigger execution of DoAction as soon as possible.
        /// </summary>
        public string _DoActionNow(T item = null)
        {
            var data = new List<string>();
            lock (queueLock)
            {
                _AddCount++;
                if (IsDisposing) return string.Empty;
                double delayTimerInterval = 0;
                lock (delayTimerLock)
                {
                    delayTimerInterval = delayTimer == null ? 0 : delayTimer.Interval;
                }
                // If there is no delay between actions then...
                if (delayTimerInterval == 0)
                {
                    if (item != null)
                        // Simply add all job items to the queue.
                        _Queue.Add(item);
                    _StarThread();
                    data.Add("Queue item added");
                }
                else
                {
                    if (item != null)
                    {
                        // If job queue is empty or contains one processing item then...
                        if (_Queue.Count == 0 || (_Queue.Count == 1 && processingFirstItem))
                        {
                            // Add new job item.
                            _Queue.Add(item);
                            data.Add("Queue item added");
                        }
                        else
                        {
                            // Update available item in the queue.
                            _Queue[_Queue.Count - 1] = item;
                            data.Add("Queue item updated");
                        }
                    }
                    // If must process first job immediately and enought time passed from last execution then...
                    if (ProcessImmediately && delayTimerInterval < _LastActionDoneTime.ElapsedMilliseconds)
                    {
                        _StarThread();
                    }
                    // If thread is not running and queue have items. then...
                    // Note: If thread is still running then queue item will be processed on running thread.
                    else if (!_IsRunning && _Queue.Count > 0)
                    {
                        double sleepTimerInterval = 0;
                        lock (sleepTimerLock)
                        {
                            sleepTimerInterval = sleepTimer == null ? 0 : sleepTimer.Interval;
                        }
                        // Check if sleep timer expired.
                        if (sleepTimerInterval <= _LastActionDoneTime.ElapsedMilliseconds)
                        {
                            DelayTimerStop();
                        }
                        // Restart delay.
                        DelayTimerStart();
                        data.Add("Delay timer started");
                        data.Add(string.Format("DelayTime = {0}", delayTimerInterval));
                    }
                }
            }
            data.Add(string.Format("DoActionCount = {0}", _ThreadCount));
            data.Add(string.Format("QueueCount = {0}", _Queue.Count));
            data.Add(string.Format("IsRunning = {0}", _IsRunning));
            if (exceptionCount > 0)
            {
                data.Add(string.Format("ExceptionCount = {0}", exceptionCount));
                if (lastExceptionDate.Ticks > 0)
                {
                    data.Add(string.Format("LastException = {0}", lastException));
                    if (DateTime.Now.Subtract(lastExceptionDate).TotalSeconds > 10) lastExceptionDate = new DateTime();
                }
            }
            return string.Join(", ", data.ToArray());
        }

        /// <summary>
        /// This function will be called inside 'queueLock' lock.
        /// </summary>
        /// <remarks>http://blogs.msdn.com/b/jaredpar/archive/2008/01/07/isynchronizeinvoke-now.aspx</remarks>
        void _StarThread()
        {
            if (IsDisposing) return;
            _StartCount++;
            // If thread is not running and queue contains items to process then...
            if (!_IsRunning)
            {
                SleepTimerStop();
                // Put into another variable for thread safety.
                ISynchronizeInvoke so = SynchronizingObject;
                if (so == null)
                {
                    // Mark thread as running.
                    _IsRunning = true;
                    // Start new thread.
                    // The thread pool job is to share and recycle threads.
                    // It allows to avoid losing a few millisecond every time we need to create a new thread.
                    System.Threading.ThreadPool.QueueUserWorkItem(ThreadAction, null);
                }
                else
                {
                    var process = Process.GetCurrentProcess();
                    // If handle is missing then...
                    if (process != null && process.Handle == IntPtr.Zero)
                    {
                        // BeginInvoke will fail. Silently clear the queue.
                        _Queue.Clear();
                    }
                    else
                    {
                        try
                        {
                            // Mark thread as running.
                            _IsRunning = true;
                            // Use asynchronous call to avoid 'queueLock' deadlock.
                            var action = (System.Threading.WaitCallback)ThreadAction;
                            // If handle exception then, maybe you forgot to dispose QueueTimer before 'so'.
                            var ar = so.BeginInvoke(action, new object[] { null });
                        }
                        catch (Exception)
                        {
                            // Silently clear the queue.
                            _Queue.Clear();
                            throw;
                        }
                    }
                }
            }

        }

        bool processingFirstItem = false;

        void ThreadAction(object state)
        {
            _ThreadCount++;
            T item = null;
            var firstRun = true;
            var cancelExecution = false;
            while (true)
            {
                lock (queueLock)
                {
                    // If no arguments left then leave the loop (except if this is firs run.
                    if (!firstRun && (_Queue.Count == 0 || IsDisposing || cancelExecution))
                    {
                        SleepTimerStart();
                        // Start sleep timer.
                        _LastActionDoneTime.Reset();
                        _LastActionDoneTime.Start();
                        // Mark thread as not running;
                        _IsRunning = false;
                        return;
                    }
                    if (_Queue.Count > 0)
                    {
                        item = _Queue[0];
                        processingFirstItem = true;
                        _ActionCount++;
                    }
                    else
                    {
                        _ActionNoneCount++;
                    }
                    firstRun = false;
                }
                var e = new QueueTimerEventArgs();
                e.Item = item;
                // Do synchronous action.
                ExecuteAction(e);
                cancelExecution = e.Cancel;
                // Remove item after job complete.
                lock (queueLock)
                {
                    // If thread item was taken then...
                    if (item != null && !e.Keep)
                    {
                        // Fire event before removing.
                        var br = BeforeRemove;
                        if (br != null)
                            br(this, e);
                        // Remove job from the queue.
                        _Queue.RemoveAt(0);
                        processingFirstItem = false;
                    }
                }
            }
        }

        void ExecuteAction(QueueTimerEventArgs e)
        {
            var dw = DoWork;
            if (dw != null)
            {
                try
                {
                    // New jobs can be added to the queue during execution.
                    dw(this, e);
                }
                catch (Exception ex)
                {
                    lastException = ex.ToString();
                    lastExceptionDate = DateTime.Now;
                    exceptionCount++;
                }
            }
        }

        #region IDisposable

        public bool IsDisposing;

        // Dispose() calls Dispose(true)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing)
        {
            IsDisposing = true;
            if (disposing)
            {
                ChangeDelayInterval(0);
                ChangeSleepInterval(0);
                // Dispose timers first
                lock (queueLock)
                {
                    _Queue.Clear();
                }
                // Make sure that outside objects are not holding this timer from disposal. 
                SynchronizingObject = null;
                DoWork = null;
            }
        }

        #endregion

    }
}
