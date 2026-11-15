using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LoopSort0
{
    // Like WoolMatch's loading operation, owns nested iterators and their cleanup.
    internal static class StartupWork
    {
        internal static IEnumerator Run(IEnumerator root,Action<Exception> failed)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            try
            {
                while(stack.Count>0)
                {
                    object next=null;Exception error=null;
                    try
                    {
                        var current=stack.Peek();
                        if(!current.MoveNext()){stack.Pop();(current as IDisposable)?.Dispose();continue;}
                        next=current.Current;
                    }
                    catch(Exception exception){error=exception;}
                    if(error!=null){failed(error);yield break;}
                    if(next is IEnumerator nested)stack.Push(nested);else yield return next;
                }
            }
            finally {while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }
    }
    internal sealed class StartupFrameBudget
    {
        readonly Stopwatch clock=Stopwatch.StartNew();readonly int maxItems;int items;
        internal StartupFrameBudget(int maxItems=3){this.maxItems=maxItems;}
        internal bool ShouldYield()=>++items>=maxItems||clock.Elapsed.TotalMilliseconds>=3;
        internal void Resume(){items=0;clock.Restart();}
    }
}
