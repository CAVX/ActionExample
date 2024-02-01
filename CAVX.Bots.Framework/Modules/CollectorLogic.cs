﻿using AsyncKeyedLock;
using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Services;
using Discord;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules
{
    internal interface ICollectorLogic
    {
        ulong MessageId { get; set; }
        ulong? OriginalUserId { get; set; }
        bool OnlyOriginalUserAllowed { get; set; }
        Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> Execute { get; set; }
    }

    public class CollectorLogic : ICollectorLogic
    {
        AsyncNonKeyedLocker _resultLock = new();
        bool _enabled = true;
        Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> _execute;

        public ulong MessageId { get; set; }
        public ulong? OriginalUserId { get; set; }
        public bool OnlyOriginalUserAllowed { get; set; }
        public Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> Execute
        {
            get => _execute;
            set
            {
                if (value == null)
                    _execute = null;
                else
                {
                    _execute = async (userData, messageId, idOptions, selectOptions) =>
                    {
                        using (await _resultLock.LockAsync())
                        {
                            if (_enabled)
                                return await value(userData, messageId, idOptions, selectOptions);

                            return (MessageResultCode.ExecutionFailed, "Sorry! You were just too late!", null);

                        }
                    };
                }
            }
        }

        public async Task ExecuteAndWait(ActionService actionService, int secondsToWait)
        {
            try
            {
                actionService.RegisterCollector(this);
                await Task.Delay(TimeSpan.FromSeconds(secondsToWait));
            }
            finally
            {
                using (await _resultLock.LockAsync())
                {
                    _enabled = false;
                    actionService.UnregisterCollector(this);
                }
            }
        }
    }
}
