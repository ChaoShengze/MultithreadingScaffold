﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MultithreadingScaffold
{
    /// <summary>
    /// Multi-threading work scaffolding, used to quickly create multi-threaded work code.
    /// 多线程工作脚手架，用于快速的创建多线程工作的代码
    /// </summary>
    public class MTScaffold
    {
        #region 参数设置

        /// <summary>
        /// The multi-threading delegate used.
        /// 用于多线程工作执行的委托
        /// </summary>
        public delegate void ThreadWorker(long counter);
        /// <summary>
        /// Final action after multi-threading work end.
        /// 用于多线程工作完毕后的最终操作，超时重试结束时也会触发此函数
        /// </summary>
        public delegate void ThreadFinal();

        /// <summary>
        /// Work thread
        /// 工作的线程
        /// </summary>
        public ThreadWorker Worker = null;
        /// <summary>
        /// Action after multi-threading work end.
        /// 工作结束时调用
        /// </summary>
        public ThreadFinal Final = null;
        /// <summary>
        /// Used to determine how many threads are counted as the end of the total task after work.
        /// 工作量，用于判断多少个线程工作完毕后算作总任务结束
        /// </summary>
        public int Workload = 0;

        /// <summary>
        /// If use a new thread to start work, or block the current thread.
        /// 是否开启新线程作为调度线程，若为false则会阻塞当前线程
        /// </summary>
        public bool InNewThread = false;
        /// <summary>
        /// Whether to enable the planning mode, slice up tasks and start a fixed number of threads to execute the task, reducing thread switching.
        /// 是否开启规划模式，即对任务进行分块，然后启动固定数量的线程来进行操作，减少线程切换
        /// </summary>
        public bool IsPlanningMode = false;
        /// <summary>
        /// Whether to print information related to thread work, closed by default.
        /// 是否打印和线程工作相关的信息，默认关闭
        /// </summary>
        public bool WriteConsole = false;
        /// <summary>
        /// Maximum number of threads, if not specified or specified as 0, it will be automatically adjusted according to the number of CPU cores in the system.
        /// 最大线程数，若不指定或指定为0则会根据系统CPU核心数自动调整
        /// </summary>
        public int ThreadLimit = 0;
        /// <summary>
        /// The sleep time of the startup thread, which affects the interval of starting a new thread each time, defaults to 100.
        /// 启动线程的睡眠时间，影响每次启动新线程的间隔，默认100
        /// </summary>
        public int SleepTime = 100;
        /// <summary>
        /// The survival time of the entire MTScaffold object, beyond this time will stop all threads, in seconds, the default value is -1 that does not open.
        /// 整个MTScaffold对象的存活时间，超出这个时间将停止所有线程，单位秒，默认值为-1即不开启
        /// </summary>
        public int TTL = -1;

        /// <summary>
        /// Thread counter, used to determine whether a new thread can be started.
        /// 线程计数器，用于判断是否可以启动新线程
        /// </summary>
        private long ThreadCount = 0;
        /// <summary>
        /// Thread counter of started threading, used to judge whether all tasks can be ended.
        /// 已启动线程计数器，用于判断是否可以结束全部任务
        /// </summary>
        public long Counter = 0;
        /// <summary>
        /// Start time of the entire MTScaffold object.
        /// 整个MTScaffold对象的启动时间
        /// </summary>
        private int StartTime = 0;
        /// <summary>
        /// Store a list of all thread objects.
        /// 存储所有线程对象的List
        /// </summary>
        private List<Thread> ls_thread;

        #endregion

        /// <summary>
        /// Actual working thread.
        /// 实际工作线程
        /// </summary>
        private void ThreadWorking()
        {
            if (TTL != -1)
            {
                StartTime = DateTime.Now.Second;
                ls_thread = new List<Thread>();
            }

            while (Interlocked.Read(ref Counter) < Workload || Interlocked.Read(ref ThreadCount) > 0)
            {
                if (Interlocked.Read(ref Counter) >= Workload)
                    continue;

                if (TTL != -1)
                    if (DateTime.Now.Second - StartTime >= TTL)
                        return;

                if (Interlocked.Read(ref ThreadCount) < ThreadLimit)
                {
                    Thread thread = new Thread(() =>
                    {
                        if (WriteConsole)
                            LogOut($"Starting new Thread, Curr Thread Count:{Interlocked.Read(ref ThreadCount)}, " +
                                $"{Interlocked.Read(ref Counter) + 1} / {Workload}.");

                        try
                        {
                            Worker(Interlocked.Increment(ref Counter) - 1);
                        }
                        catch (ThreadInterruptedException)
                        {

                        }

                        Interlocked.Decrement(ref ThreadCount);
                    });

                    if (TTL != -1)
                        ls_thread.Add(thread);

                    thread.IsBackground = true;
                    thread.Start();
                    Interlocked.Increment(ref ThreadCount);
                }

                Thread.Sleep(SleepTime);
            }

            Final?.Invoke();
        }

        /// <summary>
        /// Calling thread
        /// 调用线程
        /// </summary>
        private void CallThreadWorking()
        {
            if (InNewThread)
                Task.Run(new Action(() => { ThreadWorking(); }));
            else
                ThreadWorking();
        }

        /// <summary>
        /// Actual working thread in plan mode.
        /// 实际工作线程，计划模式
        /// </summary>
        private void ThreadWorkingInPlanMode(List<List<int>> plan)
        {
            long PlanTaskCounter = 0;

            if (TTL != -1)
            {
                StartTime = DateTime.Now.Second;
                ls_thread = new List<Thread>();
            }

            for (int i = 0; i < ThreadLimit; i++)
            {
                if (TTL != -1)
                    if (DateTime.Now.Second - StartTime >= TTL)
                        return;

                Debug.WriteLine($"PlanCounter: {i}");
                var indexArr = plan[i];

                Thread thread = new Thread(() =>
                {
                    if (WriteConsole)
                        LogOut($"Starting new Thread, Curr Thread Count:{Interlocked.Read(ref ThreadCount)}, " +
                            $"{Interlocked.Read(ref Counter) + 1} / {ThreadLimit}.");

                    try
                    {
                        foreach (int index in indexArr)
                        {
                            Worker(index);
                            Interlocked.Increment(ref PlanTaskCounter);
                        }
                    }
                    catch (ThreadInterruptedException)
                    {

                    }

                    Interlocked.Decrement(ref ThreadCount);
                });

                if (TTL != -1)
                    ls_thread.Add(thread);

                thread.IsBackground = true;
                thread.Start();
                Interlocked.Increment(ref ThreadCount);

                Thread.Sleep(SleepTime);
            }

            SpinWait spinWait = new SpinWait();
            while (PlanTaskCounter < Workload)
                spinWait.SpinOnce();

            Final?.Invoke();
        }

        /// <summary>
        /// Calling thread in Plan mode
        /// 调用计划模式的线程
        /// </summary>
        private void CallPlanThreadWorking()
        {
            var plan = GetPlanArr(Workload);
            if (InNewThread)
                Task.Run(new Action(() => { ThreadWorkingInPlanMode(plan); }));
            else
                ThreadWorkingInPlanMode(plan);
        }

        /// <summary>
        /// 调用线程，启动所有的多线程工作
        /// </summary>
        public void Start()
        {
            if (Workload == 0)
                throw new Exception("Workload must be greater than 0.");

            if (TTL != -1)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(TTL * 1000);

                    if (ls_thread != null)
                        foreach (var t in ls_thread)
                            t.Interrupt();
                });
            }

            if (ThreadLimit == 0)
                ThreadLimit = Environment.ProcessorCount;

            if (!IsPlanningMode)
                CallThreadWorking();
            else
                CallPlanThreadWorking();
        }

        /// <summary>
        /// Output log
        /// 输出LOG
        /// </summary>
        /// <param name="str">log string</param>
        private void LogOut(string str)
        {
            var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($@"{time} || {str}");
        }

        /// <summary>
        /// Get arr of task index.
        /// 获取包含任务索引的计划数组
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private List<List<int>> GetPlanArr(int num)
        {
            var list = new List<List<int>>();
            for (int i = 0; i < ThreadLimit; i++)
                list.Add(new List<int>());

            var index = -1;
            for (int i = 0; i < num; i++)
            {
                if (++index >= ThreadLimit)
                    index = 0;

                var ls = list[index];
                ls.Add(i);
            }
            return list;
        }
    }
}
