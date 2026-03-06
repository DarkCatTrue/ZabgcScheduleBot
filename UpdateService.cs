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
                Console.WriteLine("Дата расписания была обновлена.");
                //await SomeMethod1()q;
                return true;
            }
            else
            {
                bool update = await UpdatePartSchedule(updateDate);
                if (update)
                {
                    Console.WriteLine("Дата обновления расписания была обновлена.");
                    //await SomeMethod2();
                    return true;
                }
            }
            Console.WriteLine("Расписание не изменилось.");
            return false;
        }

        private static async Task<bool> UpdateAllSchedule(string currentDate)
        {
            FileSystem file = new FileSystem();
            string? currentDateFromJson = await file.GetDate("currentDate");
            return currentDateFromJson != currentDate;
        }
        private static async Task<bool> UpdatePartSchedule(string updateDate)
        {
            FileSystem file = new FileSystem();
            string? updateDateFromJson = await file.GetDate("updateDate");
            return updateDateFromJson != updateDate;
        }
    }
}
