using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodeChallenge.Helpers
{
    public class HtmlHelpers
    {
        public static DateTime ConvertUnixTime(long date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(date).ToLocalTime();
        }
    }
}
