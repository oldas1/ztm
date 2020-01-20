using System;
using Ztm.Threading;

namespace Ztm.WebApi.Watchers.TokenBalance
{
    public sealed class WatchingInfo
    {
        public WatchingInfo(Rule rule, Timer timer)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            Rule = rule;
            Timer = timer;
        }

        public Rule Rule { get; }

        public Timer Timer { get; }

        public void Deconstruct(out Rule rule, out Timer timer)
        {
            rule = Rule;
            timer = Timer;
        }
    }
}
