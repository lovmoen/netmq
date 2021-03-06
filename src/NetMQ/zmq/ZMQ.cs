/*
    Copyright (c) 2007-2012 iMatix Corporation
    Copyright (c) 2009-2011 250bpm s.r.o.
    Copyright (c) 2007-2011 Other contributors as noted in the AUTHORS file

    This file is part of 0MQ.

    0MQ is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    0MQ is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;

namespace NetMQ.zmq
{
    public class ZMQ
    {

        /******************************************************************************/
        /*  0MQ versioning support.                                                   */
        /******************************************************************************/

        /*  Version macros for compile-time API version detection                     */
        public const int ZmqVersionMajor = 3;
        public const int ZmqVersionMinor = 2;
        public const int ZmqVersionPatch = 2;

        /*  Default for new contexts                                                  */
        public const int ZmqIOThreadsDflt = 1;
        public const int ZmqMaxSocketsDflt = 1024;

        public const int ZmqPollin = 1;
        public const int ZmqPollout = 2;
        public const int ZmqPollerr = 4;

        public const int ZmqStreamer = 1;
        public const int ZmqForwarder = 2;
        public const int ZmqQueue = 3;


        //  New context API
        public static Ctx CtxNew()
        {
            //  Create 0MQ context.
            Ctx ctx = new Ctx();
            return ctx;
        }

        private static void CtxDestroy(Ctx ctx)
        {
            if (ctx == null || !ctx.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            ctx.Terminate();
        }


        public static void CtxSet(Ctx ctx, ContextOption option, int optval)
        {
            if (ctx == null || !ctx.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            ctx.Set(option, optval);
        }

        public static int CtxGet(Ctx ctx, ContextOption option)
        {
            if (ctx == null || !ctx.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            return ctx.Get(option);
        }


        //  Stable/legacy context API
        public static Ctx Init(int ioThreads)
        {
            if (ioThreads >= 0)
            {
                Ctx ctx = CtxNew();
                CtxSet(ctx, ContextOption.IOThreads, ioThreads);
                return ctx;
            }
            throw InvalidException.Create();
        }

        public static void Term(Ctx ctx)
        {
            CtxDestroy(ctx);
        }

        // Sockets
        public static SocketBase Socket(Ctx ctx, ZmqSocketType type)
        {
            if (ctx == null || !ctx.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            SocketBase s = ctx.CreateSocket(type);
            return s;
        }

        public static void Close(SocketBase s)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            s.Close();
        }

        public static void SetSocketOption(SocketBase s, ZmqSocketOptions option, Object optval)
        {

            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            s.SetSocketOption(option, optval);

        }

        public static Object GetSocketOptionX(SocketBase s, ZmqSocketOptions option)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            return s.GetSocketOptionX(option);
        }

        public static int GetSocketOption(SocketBase s, ZmqSocketOptions opt)
        {

            return s.GetSocketOption(opt);
        }

        public static void SocketMonitor(SocketBase s, String addr, SocketEvent events)
        {

            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            s.Monitor(addr, events);
        }


        public static void Bind(SocketBase s, String addr)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            s.Bind(addr);
        }

        public static int BindRandomPort(SocketBase s, String addr)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            return s.BindRandomPort(addr);
        }

        public static void Connect(SocketBase s, String addr)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }

            s.Connect(addr);
        }

        public static void Unbind(SocketBase s, String addr)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            s.TermEndpoint(addr);
        }

        public static void Disconnect(SocketBase s, String addr)
        {
            if (s == null || !s.CheckTag())
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            s.TermEndpoint(addr);
        }      
    
        //  The proxy functionality
        public static bool Proxy(SocketBase frontend_, SocketBase backend_, SocketBase control_)
        {
            if (frontend_ == null || backend_ == null)
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            return NetMQ.zmq.Proxy.CreateProxy(
                    frontend_,
                    backend_,
                    control_);
        }       

        public static int Poll(PollItem[] items, int timeout)
        {
            return Poll(items, items.Length, timeout);
        }

        public static int Poll(PollItem[] items, int itemsCount, int timeout)
        {
            if (items == null)
            {
                throw NetMQException.Create(ErrorCode.EFAULT);
            }
            if (itemsCount == 0)
            {
                if (timeout <= 0)
                    return 0;
                Thread.Sleep(timeout);
                return 0;
            }

            bool firstPass = true;
            int nevents = 0;

            List<Socket> writeList = new List<Socket>();
            List<Socket> readList = new List<Socket>();
            List<Socket> errorList = new List<Socket>();

            for (int i = 0; i < itemsCount; i++)
            {
                var pollItem = items[i];

                if (pollItem.Socket != null)
                {
                    if (pollItem.Events != PollEvents.None)
                    {
                        readList.Add(pollItem.Socket.FD);
                    }
                }
                else
                {
                    if ((pollItem.Events & PollEvents.PollIn) == PollEvents.PollIn)
                    {
                        readList.Add(pollItem.FileDescriptor);
                    }

                    if ((pollItem.Events & PollEvents.PollOut) == PollEvents.PollOut)
                    {
                        writeList.Add(pollItem.FileDescriptor);
                    }

                    if ((pollItem.Events & PollEvents.PollError) == PollEvents.PollError)
                    {
                        errorList.Add(pollItem.FileDescriptor);
                    }
                }
            }

            List<Socket> inset = new List<Socket>(readList.Count);
            List<Socket> outset = new List<Socket>(writeList.Count);
            List<Socket> errorset = new List<Socket>(errorList.Count);

            Stopwatch stopwatch = null;

            while (true)
            {
                int currentTimeoutMicroSeconds;

                if (firstPass)
                {
                    currentTimeoutMicroSeconds = 0;
                }
                else if (timeout == -1)
                {
                    currentTimeoutMicroSeconds = -1;
                }
                else
                {
                    currentTimeoutMicroSeconds = (int)((timeout - stopwatch.ElapsedMilliseconds) * 1000);

                    if (currentTimeoutMicroSeconds < 0)
                    {
                        currentTimeoutMicroSeconds = 0;
                    }
                }

                inset.AddRange(readList.Where(x => x.Connected || x.ProtocolType != ProtocolType.Tcp));
                outset.AddRange(writeList.Where(x => x.Connected || x.ProtocolType != ProtocolType.Tcp));
                errorset.AddRange(errorList.Where(x => x.Connected || x.ProtocolType != ProtocolType.Tcp));

                try
                {
                    System.Net.Sockets.Socket.Select(inset, outset, errorset, currentTimeoutMicroSeconds);
                }
                catch (SocketException ex)
                {
                    throw NetMQException.Create(ErrorCode.ESOCKET, ex);
                }

                for (int i = 0; i < itemsCount; i++)
                {
                    var pollItem = items[i];

                    pollItem.ResultEvent = PollEvents.None;

                    if (pollItem.Socket != null)
                    {
                        PollEvents events = (PollEvents)GetSocketOption(pollItem.Socket, ZmqSocketOptions.Events);

                        if ((pollItem.Events & PollEvents.PollIn) == PollEvents.PollIn &&
                            (events & PollEvents.PollIn) == PollEvents.PollIn)
                        {
                            pollItem.ResultEvent |= PollEvents.PollIn;
                        }

                        if ((pollItem.Events & PollEvents.PollOut) == PollEvents.PollOut &&
                            (events & PollEvents.PollOut) == PollEvents.PollOut)
                        {
                            pollItem.ResultEvent |= PollEvents.PollOut;
                        }
                    }
                    else
                    {
                        if (inset.Contains(pollItem.FileDescriptor))
                        {
                            pollItem.ResultEvent |= PollEvents.PollIn;
                        }

                        if (outset.Contains(pollItem.FileDescriptor))
                        {
                            pollItem.ResultEvent |= PollEvents.PollOut;
                        }

                        if (errorset.Contains(pollItem.FileDescriptor))
                        {
                            pollItem.ResultEvent |= PollEvents.PollError;
                        }
                    }

                    if (pollItem.ResultEvent != PollEvents.None)
                    {
                        nevents++;
                    }
                }

                inset.Clear();
                outset.Clear();
                errorset.Clear();

                if (timeout == 0)
                {
                    break;
                }

                if (nevents > 0)
                {
                    break;
                }

                if (timeout < 0)
                {
                    if (firstPass)
                    {
                        firstPass = false;
                    }

                    continue;
                }

                if (firstPass)
                {
                    stopwatch = Stopwatch.StartNew();
                    firstPass = false;
                    continue;
                }

                if (stopwatch.ElapsedMilliseconds > timeout)
                {
                    break;
                }
            }

            return nevents;
        }

        public static int ZmqMakeVersion(int major, int minor, int patch)
        {
            return ((major) * 10000 + (minor) * 100 + (patch));
        }

        public static String ErrorText(ErrorCode errno)
        {
            return "Errno = " + errno.ToString();
        }

        [Obsolete]
        public static void Send(SocketBase sender, string message, SendReceiveOptions options)
        {
            Msg msg = new Msg();
            msg.InitPool(Encoding.ASCII.GetByteCount(message));

            Encoding.ASCII.GetBytes(message, 0, message.Length, msg.Data, 0);

            sender.Send(ref msg, options);
        }

        [Obsolete]
        public static Msg Recv(SocketBase receiver, SendReceiveOptions options)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            receiver.Recv(ref msg, options);

            return msg;
        }
    }
}
