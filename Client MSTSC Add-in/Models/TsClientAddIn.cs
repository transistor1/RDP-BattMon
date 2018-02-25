﻿using FieldEffect.Classes;
using FieldEffect.Interfaces;
using System;
using System.Windows.Forms;
using Win32.WtsApi32;

/**
 * Credit:
 * https://www.codeproject.com/Articles/16374/How-to-Write-a-Terminal-Services-Add-in-in-Pure-C
 */
namespace FieldEffect.Models
{
    public class TsClientAddIn : ITsClientAddIn
    {
        IntPtr _channel;
        ChannelEntryPoints _entryPoints;
        int _openChannel = 0;
        string _channelName = String.Empty;
        ChannelInitEventDelegate _channelInitEventDelegate = null;
        ChannelOpenEventDelegate _channelOpenEventDelegate = null;
        private string _serverName;

        public TsClientAddIn(string channelName, ChannelEntryPoints entryPoints)
        {
            _channelName = channelName;

            if (channelName.Length > 7)
            {
                throw new ArgumentOutOfRangeException(String.Format("TsClientAddIn ({0}): Please choose a name for channelName that is 7 or fewer characters.", _channelName));
            }
            _channelInitEventDelegate = 
                new ChannelInitEventDelegate(VirtualChannelInitEventProc);
            _channelOpenEventDelegate =
                new ChannelOpenEventDelegate(VirtualChannelOpenEvent);
            _entryPoints = entryPoints;
        }

        public ChannelReturnCodes Initialize()
        {
            ChannelDef[] cd = new ChannelDef[1];
            cd[0] = new ChannelDef
            {
                name = _channelName
            };

            return _entryPoints.VirtualChannelInit(
                ref _channel, cd, 1, 1, _channelInitEventDelegate);
        }

        public void VirtualChannelWrite(byte[] data)
        {
            ChannelReturnCodes ret = _entryPoints.
                VirtualChannelWrite(_openChannel, data, (uint)data.Length, IntPtr.Zero);
            if (ret != ChannelReturnCodes.Ok)
                MessageBox.Show(String.Format("TsClientAddIn ({0}): Couldn't write to communcation channel for battery monitor.", _channelName),
                    "Error", MessageBoxButtons.OK,
                     MessageBoxIcon.Error);
        }

        public void VirtualChannelInitEventProc(IntPtr initHandle,
            ChannelEvents Event, byte[] data, int dataLength)
        {
            switch (Event)
            {
                case ChannelEvents.Initialized:
                    break;
                case ChannelEvents.Connected:
                    ChannelReturnCodes ret = _entryPoints.VirtualChannelOpen(
                        initHandle, ref _openChannel,
                        _channelName, _channelOpenEventDelegate);
                    if (ret != ChannelReturnCodes.Ok)
                        MessageBox.Show(String.Format("TsClientAddIn ({0}): Couldn't open communcation channel for battery monitor.", _channelName),
                            "Error", MessageBoxButtons.OK,
                             MessageBoxIcon.Error);
                    else
                    {
                        string servername = System.Text.Encoding.Unicode.GetString(data);
                        _serverName = servername.Substring(0, servername.IndexOf('\0'));
                    }
                    break;
                case ChannelEvents.V1Connected:
                    MessageBox.Show(String.Format("TsClientAddIn ({0}): Connecting to a Terminal Server that doesn't support data communication.", _channelName),
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case ChannelEvents.Disconnected:
                    break;
                case ChannelEvents.Terminated:
                    GC.KeepAlive(_channelInitEventDelegate);
                    GC.KeepAlive(_channelOpenEventDelegate);
                    GC.KeepAlive(_entryPoints);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    break;
            }
        }

        public event EventHandler<DataChannelEventArgs> DataChannelEvent;

        protected void OnDataChannelEvent(DataChannelEventArgs e)
        {
            DataChannelEvent?.Invoke(this, e);
        }

        public void VirtualChannelOpenEvent(int openHandle,
            ChannelEvents Event, byte[] data,
            int dataLength, uint totalLength, ChannelFlags dataFlags)
        {
            DataChannelEventArgs args = new DataChannelEventArgs()
            {
                Data = data,
                DataFlags = dataFlags,
                DataLength =dataLength,
                Event = Event,
                OpenHandle = openHandle,
                TotalLength = totalLength
            };

            OnDataChannelEvent(args);
            
        }
    }
}
