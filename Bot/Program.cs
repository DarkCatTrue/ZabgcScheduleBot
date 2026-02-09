using ZabgcScheduleBot;

DotNetEnv.Env.Load();

//await Parse.SaveData("cg.htm", "Jsons\\Groups.json");
//await Parse.SaveData("cp.htm", "Jsons\\Teachers.json");
//await Parse.SaveData("ca.htm", "Jsons\\Audiences.json");

//await Parse.SaveSchedulePages("Jsons\\Groups.json", "ScheduleToday\\GroupsSchedule");
//await Parse.SaveSchedulePages("Jsons\\Teachers.json", "ScheduleToday\\TeachersSchedule");
//await Parse.SaveSchedulePages("Jsons\\Audiences.json", "ScheduleToday\\AudiencesSchedule");


await Parse.GetScheduleFromFile("ScheduleToday\\GroupsSchedule\\cg26.htm");