namespace Armor.NetflowExporter
{
    using System;

    public static class DateHelpers
    {
        public static uint GetEpoch()
        {
            return (uint)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public static uint GetUpTimeMS()
        {
            return (uint)Environment.TickCount;
        }
    }
}
