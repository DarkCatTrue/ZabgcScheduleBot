namespace ZabgcScheduleBot
{
    public class UpdateService
    {
        public static async Task <bool> GetUpdateExpressSchedule()
        {
            Parse parse = new Parse();
            string[] dates = new string[1];
            dates = await parse.GetDates();
            
            string currentDate = dates[0];
            string updateDate = dates[1];

            bool updateCurrentSchedule = await UpdateAllSchedule(currentDate);
            if (updateCurrentSchedule)
            {
                //await SomeMethod1()q;
                Console.WriteLine("Дата расписания была обновлена.");
                return true;
            }
            else
            {
                bool update = await UpdatePartSchedule(updateDate);
                if (update)
                {
                    //await SomeMethod2();
                    Console.WriteLine("Дата обновления расписания была обновлена.");
                    return true;
                }
            }
            Console.WriteLine("Расписание не изменилось.");
            return false;
        }

        private static async Task<bool> UpdateAllSchedule(string currentDate)
        {
            FileSystem file = new FileSystem();
            string? currentDateFromJson = await file.GetCurrentDate();
            return currentDateFromJson != currentDate;
        }
        private static async Task<bool> UpdatePartSchedule(string updateDate)
        {
            FileSystem file = new FileSystem();
            string? updateDateFromJson = await file.GetUpdateDate();
            return updateDateFromJson != updateDate;
        }
    }
}
