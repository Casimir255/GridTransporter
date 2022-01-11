using ProtoBuf;
using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GridTransporter.Utilities
{
    class NetworkUtility
    {
        public static byte[] Serialize<T>(T instance)
        {
            if (instance == null)
                return null;



            using (var m = new MemoryStream())
            {
                // m.Seek(0, SeekOrigin.Begin);
                Serializer.Serialize(m, instance);

                return m.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] data)
        {
            if (data == null)
                return default;

            using (var m = new MemoryStream(data))
            {
                return Serializer.Deserialize<T>(m);
            }
        }


        public static Task InvokeAsync(Action action, [CallerMemberName] string caller = "Nexus")
        {


            //Jimm thank you. This is the best
            var ctx = new TaskCompletionSource<object>();
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {

                    action.Invoke();
                    ctx.SetResult(null);
                }
                catch (Exception e)
                {
                    ctx.SetException(e);
                }

            }, caller);
            return ctx.Task;
        }


        public static Task<T> InvokeAsync<T>(Func<T> action, [CallerMemberName] string caller = "Nexus")
        {


            //Jimm thank you. This is the best
            var ctx = new TaskCompletionSource<T>();
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    ctx.SetResult(action.Invoke());
                }
                catch (Exception e)
                {
                    ctx.SetException(e);
                }

            }, caller);
            return ctx.Task;
        }

        public static Task<T2> InvokeAsync<T1, T2>(Func<T1, T2> action, T1 arg, [CallerMemberName] string caller = "Nexus")
        {
            //Jimm thank you. This is the best
            var ctx = new TaskCompletionSource<T2>();
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    ctx.SetResult(action.Invoke(arg));
                }
                catch (Exception e)
                {
                    ctx.SetException(e);
                }

            }, caller);
            return ctx.Task;
        }

        public static Task<T3> InvokeAsync<T1, T2, T3>(Func<T1, T2, T3> action, T1 arg, T2 arg2, [CallerMemberName] string caller = "Nexus")
        {
            //Jimm thank you. This is the best
            var ctx = new TaskCompletionSource<T3>();
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    ctx.SetResult(action.Invoke(arg, arg2));
                }
                catch (Exception e)
                {
                    ctx.SetException(e);
                }

            }, caller);
            return ctx.Task;
        }

        public static Task<T4> InvokeAsync<T1, T2, T3, T4>(Func<T1, T2, T3, T4> action, T1 arg, T2 arg2, T3 arg3, [CallerMemberName] string caller = "Nexus")
        {
            //Jimm thank you. This is the best
            var ctx = new TaskCompletionSource<T4>();
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    ctx.SetResult(action.Invoke(arg, arg2, arg3));
                }
                catch (Exception e)
                {
                    ctx.SetException(e);
                }

            }, caller);
            return ctx.Task;
        }

        public static void EnQueueOnGameThread(Action Action)
        {
            MySandboxGame.Static.Invoke(Action, "Nexus");
        }

    }
}
