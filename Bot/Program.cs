using ZabgcScheduleBot;

DotNetEnv.Env.Load();

FileSystem file = new FileSystem();
file.InitialCatalogs();

Parse parse = new Parse();

//await parse.SaveAllData();

//await parse.SaveSchedulePages("Jsons\\Groups.json", "CurrentSchedule\\GroupsSchedule");
//await parse.SaveSchedulePages("Jsons\\Teachers.json", "CurrentSchedule\\TeachersSchedule");
//await parse.SaveSchedulePages("Jsons\\Audiences.json", "CurrentSchedule\\AudiencesSchedule");

//Console.WriteLine(await parse.GetScheduleFromFile("CurrentSchedule\\GroupsSchedule\\cg26.htm"));
//Console.WriteLine(await parse.GetScheduleFromWeb("cg26.htm"));