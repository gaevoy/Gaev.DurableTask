﻿using System;
using System.Threading.Tasks;

namespace Gaev.DurableTask
{
    public static class ProcessExtensions
    {
        public static Task Do(this IProcess process, Func<Task> act, string id)
        {
            return process.Do(async () =>
            {
                await act();
                return Void.Nothing;
            }, id);
        }

        public static async Task<DelayResult> Delay(this IProcess process, TimeSpan delay, string id)
        {
            var result = await process.Get(new DelayResult { Desired = DateTime.UtcNow + delay }, id);
            if (result.Actual == null)
            {
                delay = result.Desired - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, process.Cancellation);
                    result.OnTime = true;
                }
                result.Actual = DateTime.UtcNow;
                await process.Set(result, id);
            }
            return result;
        }

        public static T As<T>(this IProcess process) where T : IProcess
        {
            return (T)process;
        }

        public static Task<T> Get<T>(this IProcess process, T defaultValue, string id)
        {
            return process.Do(() => Task.FromResult(defaultValue), id);
        }

        public static Task Set<T>(this IProcess process, T value, string id)
        {
            return process.Do(() => Task.FromResult(value), id, redo: true);
        }
    }
}