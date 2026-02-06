namespace ZabgcScheduleBot
{
    public class UpdateService
    {
        private static readonly string CurrentDate;
        private static readonly string UpdateDate;
        public static async Task <bool> GetUpdateExpressSchedule()
        {
            string[] dates = new string[2];
            dates = await Parse.GetDates();
            
            string currentDate = dates[1];
            string updateDate = dates[2];

            bool updateCurrentDateSchedule = await UpdateCurrentDate(currentDate);
            if (updateCurrentDateSchedule)
            {
                //await SomeMethod1()q;
                return true;
            }
            else
            {
                bool update = await UpdateDateSchedule(updateDate);
                if (update)
                {
                    //await SomeMethod2();
                    return true;
                }
            }
            return false;
        }

        private static async Task<bool> UpdateCurrentDate(string currentDate)
        {
            return CurrentDate != currentDate;
        }
        private static async Task<bool> UpdateDateSchedule(string updateDate)
        {
            return UpdateDate != updateDate;
        }
    }
}
