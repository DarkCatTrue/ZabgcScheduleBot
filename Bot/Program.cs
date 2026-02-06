using ZabgcScheduleBot;

DotNetEnv.Env.Load();

await Parse.GetData("cg.htm", "Groups.json");