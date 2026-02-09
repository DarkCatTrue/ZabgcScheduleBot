using ZabgcScheduleBot;

DotNetEnv.Env.Load();

FileSystem file = new FileSystem();
file.InitialCatalogs();

//await Parse.SaveSchedulePages("Jsons\\Groups.json", "ScheduleToday\\GroupsSchedule");
//await Parse.SaveSchedulePages("Jsons\\Teachers.json", "ScheduleToday\\TeachersSchedule");
//await Parse.SaveSchedulePages("Jsons\\Audiences.json", "ScheduleToday\\AudiencesSchedule");

Parse parse = new Parse();
Console.WriteLine(await parse.GetScheduleFromFile("ScheduleToday\\GroupsSchedule\\cg26.htm"));